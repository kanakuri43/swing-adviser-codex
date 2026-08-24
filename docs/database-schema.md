# Database Schema

本書は、Swing Adviser の SQLite 業務スキーマに関する正本である。
テーブル・列・制約の論理設計を定め、EF Core の最初の業務マイグレーションは本書に従って別途実装する。
Non-negotiable rules と矛盾する場合は [`AGENT.md`](../AGENT.md) を優先する。

## 1. Design goals

- 利用者入力の元約定を監査原票として残し、外部取得は正規化した事実、取得観測、content hash、保持可能なsource artifactを追跡する。
- 日足、企業アクション、銘柄マスタ、信用情報の訂正を上書きせず、分析時に見えていた版を再現する。
- 分析日時点より後の情報を混入させず、point-in-time を保証できない入力を区別する。
- 戦略設定、入力manifest、企業アクション調整、指標値、判定理由を凍結する。
- 候補、ポジション、約定、MarginLot、信用コスト、AI 結果を別の事実として扱う。
- 現在状態は履歴から再構築できる投影とし、履歴を現在値で置き換えない。

主要な関係は次のとおりである。

```text
instrument
  |-- daily_price_revision
  |-- corporate_action_revision
  |-- margin_eligibility_revision
  |-- analysis_input_manifest -- technical_analysis_result -- candidate_result
  |                                                               |
  |                                                               +-- ai_check_job -- ai_attempt -- ai_result
  |
  +-- position -- trade_execution -- trade_execution_revision
          |            |
          |            +-- closing execution -- lot_allocation_revision -- margin_lot
          |                                                              |
          +--------------------------------------------------------------+
                                                                         |-- margin_lot_contract_revision
                                                                         |-- margin_cost_item/observation
                                                                         +-- risk_plan_revision
```

## 2. Storage conventions

### 2.0 Runtime database location

実運用DBのファイル名は `swing-adviser.db` とし、実行中EXEと同じディレクトリに保存する。

```text
Path.Combine(AppContext.BaseDirectory, "swing-adviser.db")
```

- カレントディレクトリ、ユーザープロファイル、`LocalAppData`、一時ディレクトリからは解決しない。
- SQLiteの `swing-adviser.db-wal` と `swing-adviser.db-shm` も同じディレクトリに作成される。
- 起動時にディレクトリの作成・書込・DB open可否を確認する。書き込み不可の場合は処理を継続せず、保存先と原因を示すエラーにする。別ディレクトリへの暗黙フォールバックやメモリDBへの切替は行わない。
- EXEを `Program Files` 等の通常ユーザーが書き込めない場所へ配置しない。配布・更新処理は既存のDB、WAL、SHM、バックアップを上書きまたは削除しない。
- `SwingAdviserDbContextFactory` の `swing-adviser.design.db` はEF Coreツール専用であり、実運用DBとして使用しない。
- テストは一時DBまたはin-memory SQLiteを使い、EXE配置ディレクトリの実運用DBへ接続しない。

### 2.1 Names and primitive representations

| Logical type | SQLite representation | Rule |
|---|---|---|
| ID | `TEXT NOT NULL` | Application-generated UUID in lowercase canonical form. Primary keys are not business keys. |
| Instant | `TEXT` | UTC ISO-8601 with exactly seven fractional digits (`yyyy-MM-ddTHH:mm:ss.fffffffZ`). A known instant is never stored without its offset meaning. |
| Market date | `TEXT` | `yyyy-MM-dd`, interpreted as a Tokyo Stock Exchange session date. |
| Decimal | `TEXT` | Canonical non-exponent decimal parsed as C# `decimal`. `REAL` is not used for prices, rates, factors, or money. |
| Whole shares/volume/days | `INTEGER` | Non-negative 64-bit integer. Executed share quantities must be positive. |
| Boolean | `INTEGER` | `0` or `1` with a `CHECK` constraint. |
| Currency | `TEXT` | ISO 4217 uppercase code with length 3. |
| Enum | `TEXT` | Stable English identifier with a `CHECK` constraint. Unknown and missing states are explicit where required. |
| JSON | `TEXT` | Schema-versioned, normalized UTF-8 JSON. Hashes are calculated over the normalized text. |
| SHA-256 | `TEXT` | 64 lowercase hexadecimal characters with a length/character `CHECK`. |

`decimal` values are intentionally stored as canonical text to avoid IEEE 754 rounding. Canonicalization identifier `decimal-c14n-v1` is `value.ToString("0.############################", CultureInfo.InvariantCulture)`: no exponent, plus sign, leading/trailing zero padding, or negative zero; zero is exactly `"0"`. Parsing permits only an optional minus sign, ASCII digits, and one decimal point, then requires formatting the parsed C# `decimal` back to the identical string. Numeric comparisons and aggregation are performed after parsing; SQL lexical ordering must not be used as numeric ordering.

SQLite has no built-in regular-expression `CHECK`, so the complete canonical-decimal grammar is enforced by the value converter/repository and round-trip tests; row-local sign/range relationships are also validated in the same transaction. EF must not use its default SQLite `decimal` mapping.

### 2.2 Time semantics

- `evaluation_bar_date` is a market date; `analyzed_at_utc` is an instant. They are never substituted for one another.
- UI-entered JST times are converted to UTC at the application boundary. Display converts UTC back to `Asia/Tokyo`.
- Domain/Application use `DateTimeOffset`. Infrastructure uses an explicit value converter that normalizes every value to UTC and writes the exact fixed-width format above; EF's default SQLite `DateTimeOffset` mapping is not used.
- Because all persisted instants have the same UTC suffix and fixed width, ordinal SQL comparison is chronological. Repository tests must exercise translated `<=` cutoff and `ORDER BY` queries; client-side filtering is not accepted for point-in-time selection.
- `announced_at_utc`, `available_at_utc`, `first_observed_at_utc`, and `recorded_at_utc` have different meanings and are stored separately.
- An unknown source availability time remains `NULL`; it is not filled with the retrieval time. `availability_status` records `Known`, `Estimated`, or `Unknown`.

### 2.3 Revision contract

Tables ending in `_revisions` or `_observations` are append-only after insertion. Every table row described below as `Common revision columns` contains every column in this list; `supersedes_id`, `source_artifact_id`, and `available_at_utc` may be null as specified. Tables that use a different user/operational revision contract list all revision columns explicitly instead.

| Column | Type | Meaning |
|---|---|---|
| `id` | TEXT PK | Immutable revision/observation ID. |
| `revision_no` | INTEGER NOT NULL | Monotonically increasing number within the logical parent. Starts at 1. |
| `supersedes_id` | TEXT NULL FK self | Direct predecessor when this is a correction. |
| `content_sha256` | TEXT NOT NULL | Hash of normalized business content, excluding IDs and ingestion timestamps. |
| `available_at_utc` | TEXT NULL | When the information was available from the source. |
| `availability_status` | TEXT NOT NULL | `Known`, `Estimated`, or `Unknown`. |
| `first_observed_at_utc` | TEXT NOT NULL | First time this application observed this content. |
| `recorded_at_utc` | TEXT NOT NULL | Transaction time when the row was persisted. |
| `source_artifact_id` | TEXT NULL FK | Exact downloaded file/API response/evidence when retained. |

Each logical parent has `UNIQUE(parent_key, revision_no)`, and `supersedes_id` has a filtered unique index so a revision chain cannot branch. This branch-prevention rule also applies to the user/operational revision tables that list their columns explicitly instead of using the Common revision columns. The application verifies that a predecessor belongs to the same logical parent and that the chain is acyclic. Re-fetching identical content records ingestion activity but does not create a new business revision.

EF mappings use explicit snake_case names, enum-to-string conversion, and `DeleteBehavior.Restrict`. Audit tables are protected from `UPDATE` and `DELETE` by SQLite triggers generated in the business migration. Operational state machines are the exception described in section 9.

### 2.4 JSON and hashes

Every JSON value uses an envelope `{ "schemaVersion": "...", "value": ... }` unless its row has an explicit `schema_version`, `algorithm_version`, `configuration_sha256`, or equivalent column that defines the JSON contract. Thus columns described as an “array” store that array under `value`; result payloads whose owning row has `schema_version` may store the schema-defined object directly. The application canonicalizer identifier is `json-c14n-v1`. SHA-256 input includes natural keys and content hashes, not random database IDs alone. Secrets, API keys, and unsanitized stderr are never persisted.

Every foreign-key column has an index unless it is already the leftmost prefix of a primary/unique index. Names are explicit snake_case: `ix_<table>_<columns>` for non-unique indexes and `ux_<table>_<columns>` for unique indexes.

## 3. Reference and ingestion tables

### 3.1 `instruments`

Stable internal identity. A JPX code is not used as a primary key because provider symbols and code rules can change.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

### 3.2 `instrument_identifiers`

Stable logical identity for one instrument/provider identifier. It avoids pre-empting the still-pending normalization rules in TODO section 5.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | References `instruments`. |
| `scheme` | TEXT NOT NULL | Examples: `JPXLocalCode`, `YahooSymbol`, `ISIN`. |
| `created_at_utc` | TEXT NOT NULL | Logical identifier creation. |

### 3.3 `instrument_identifier_revisions`

Identifier values and validity are append-only so an initially open-ended identifier can later be ended or corrected without update/delete.

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Logical parent is `instrument_identifier_id`. |
| `instrument_identifier_id` | TEXT NOT NULL FK | Stable identifier. |
| `value` | TEXT NOT NULL | Case-preserving source value. |
| `valid_from_date` | TEXT NULL | Inclusive. |
| `valid_to_date` | TEXT NULL | Inclusive; `NULL` means open-ended. |
| `record_disposition` | TEXT NOT NULL | `Effective`, `Voided`. |
| `change_kind` | TEXT NOT NULL | `Initial`, `ValidityChange`, `Correction`, `Void`. |

Unique: `(instrument_identifier_id, revision_no)`. Index `ix_instrument_identifier_revisions_value_instrument_identifier_id` covers exact-value lookup before joining the parent `instrument_identifiers` row to filter `scheme`. The application selects unsuperseded `Effective` leaves and rejects overlapping current validity ranges or two instruments claiming the same `(scheme, value)` at the same date. This cross-revision temporal uniqueness cannot be represented by a nullable SQLite UNIQUE index without a mutable `is_current` flag, which is intentionally avoided.

### 3.4 `instrument_master_revisions`

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Section 2.3. Logical parent is `(instrument_id, provider)`. |
| `instrument_id` | TEXT NOT NULL FK | References `instruments`. |
| `provider` | TEXT NOT NULL | Master source, initially `JPX`. |
| `effective_from_date` | TEXT NOT NULL | Source effective date. |
| `effective_to_date` | TEXT NULL | Inclusive. |
| `name` | TEXT NOT NULL | Display name at this revision. |
| `exchange_code` | TEXT NOT NULL | Exchange/venue identifier. |
| `market_segment` | TEXT NOT NULL | Prime/Standard/Growth etc. Source value is retained. |
| `security_type` | TEXT NOT NULL | `DomesticCommonStock`, `ETF`, `ETN`, `REIT`, `Preferred`, `Foreign`, `Other`, `Unknown`. |
| `trading_unit` | INTEGER NULL | Positive when known. |
| `currency` | TEXT NOT NULL | Normally `JPY`. |
| `listing_date` | TEXT NULL | Known listing date. |
| `delisting_date` | TEXT NULL | Known delisting date. |
| `listing_status` | TEXT NOT NULL | `Listed`, `DelistingScheduled`, `Delisted`, `Unknown`. |
| `scan_eligibility` | TEXT NOT NULL | `Eligible`, `Excluded`, `Unknown`. |
| `exclusion_reason` | TEXT NULL | Required when excluded. |
| `change_kind` | TEXT NOT NULL | `EffectiveSnapshot`, `Correction`, `Cancellation`. |

For a market date/information cutoff, selection first filters effective/available/recorded times, then chooses the greatest `effective_from_date` and the applicable revision leaf. `change_kind` distinguishes a normal later master snapshot from correction of previously observed content.

### 3.5 `market_calendar_versions`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID used by FKs. |
| `market_code` | TEXT NOT NULL | Exchange/market. |
| `provider` | TEXT NOT NULL | Calendar source. |
| `version_name` | TEXT NOT NULL | Provider/application version label. |
| `time_zone_id` | TEXT NOT NULL | Initially `Asia/Tokyo`. |
| `algorithm_version` | TEXT NOT NULL | Calendar interpretation algorithm. |
| `content_sha256` | TEXT NOT NULL | Ordered day-set hash. |
| `source_artifact_id` | TEXT NULL FK | Source evidence. |
| `recorded_at_utc` | TEXT NOT NULL | Version creation. |

Unique: `(market_code, content_sha256)` and `(market_code, version_name)`.

### 3.6 `market_calendar_days`

| Column | Type | Constraints/notes |
|---|---|---|
| `trading_date` | TEXT NOT NULL | Part of PK. |
| `session_status` | TEXT NOT NULL | `Open`, `Closed`, `HalfDay`, `UnscheduledClosure`, `Unknown`. |
| `reason` | TEXT NULL | Holiday/closure reason. |
| `market_calendar_version_id` | TEXT NOT NULL FK | References `market_calendar_versions(id)`. |
| `source_artifact_id` | TEXT NULL FK | Calendar evidence. |
| `recorded_at_utc` | TEXT NOT NULL | Insert time. |

Primary key: `(market_calendar_version_id, trading_date)`. The market is obtained from the referenced version, so a child row cannot disagree with it. A stored analysis or maturity calculation records the calendar version it used.

### 3.7 `source_artifacts`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `provider` | TEXT NOT NULL | Data provider. |
| `dataset_kind` | TEXT NOT NULL | Price, master, margin, action, fundamental, statement, etc. |
| `source_uri` | TEXT NULL | Sanitized URI or document identifier. |
| `retrieved_at_utc` | TEXT NOT NULL | Retrieval instant. |
| `source_published_at_utc` | TEXT NULL | Provider publication instant. |
| `available_at_utc` | TEXT NULL | Provider availability instant. |
| `availability_status` | TEXT NOT NULL | `Known`, `Estimated`, `Unknown`. |
| `content_sha256` | TEXT NOT NULL | Exact response/file hash. |
| `media_type` | TEXT NULL | MIME type. |
| `retention_status` | TEXT NOT NULL | `RetainedInline`, `RetainedExternal`, `HashOnly`. Sensitive broker evidence may be hash-only. |
| `content_blob` | BLOB NULL | Optional compressed, non-secret retained payload. |
| `external_location` | TEXT NULL | Optional controlled local reference; never a secret-bearing URI. |
| `content_encoding` | TEXT NULL | Compression/character encoding when retained. |
| `metadata_json` | TEXT NOT NULL | Schema-versioned non-secret metadata. |

Unique: `(provider, dataset_kind, content_sha256)`.

Checks require `content_blob` for `RetainedInline`, `external_location` for `RetainedExternal`, and neither for `HashOnly`. Retained payloads must be free of secrets; sensitive broker evidence defaults to hash-only plus a user-readable evidence description.

### 3.8 `data_update_runs`

Operational import history; completed rows are not deleted.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `dataset_kind` | TEXT NOT NULL | Requested dataset. |
| `provider` | TEXT NOT NULL | Requested provider. |
| `status` | TEXT NOT NULL | `Queued`, `Running`, `Succeeded`, `PartiallySucceeded`, `Failed`, `Cancelled`. |
| `requested_at_utc` | TEXT NOT NULL | Request instant. |
| `started_at_utc` | TEXT NULL | Start instant. |
| `completed_at_utc` | TEXT NULL | Terminal instant. |
| `requested_count` | INTEGER NULL | Items planned. |
| `success_count` | INTEGER NOT NULL DEFAULT 0 | Non-negative. |
| `failure_count` | INTEGER NOT NULL DEFAULT 0 | Non-negative. |
| `unchanged_count` | INTEGER NOT NULL DEFAULT 0 | Identical re-fetches. |
| `configuration_snapshot_json` | TEXT NOT NULL | Secret-free input settings. |
| `configuration_sha256` | TEXT NOT NULL | Snapshot hash. |
| `summary` | TEXT NULL | Sanitized summary. |

### 3.9 `data_update_items`

Records individual retrieval/parse outcomes, including unchanged re-observations that do not create a new business revision.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `data_update_run_id` | TEXT NOT NULL FK | Parent run. |
| `source_artifact_id` | TEXT NULL FK | Exact artifact. |
| `instrument_id` | TEXT NULL FK | Subject when applicable. |
| `item_key` | TEXT NOT NULL | Dataset natural key (date/event/source row). |
| `item_attempt_no` | INTEGER NOT NULL | Starts at 1; records retries within one update run. |
| `outcome` | TEXT NOT NULL | `Inserted`, `Corrected`, `Unchanged`, `Skipped`, `Failed`. |
| `resolved_entity_type` | TEXT NULL | Revision table/entity type. |
| `resolved_revision_id` | TEXT NULL | Exact revision ID for inserted/corrected/unchanged outcome. |
| `observed_at_utc` | TEXT NOT NULL | Observation instant. |

Unique: `(data_update_run_id, item_key, item_attempt_no)`. The application validates the polymorphic resolved revision reference against the declared entity type.

### 3.10 `data_update_failures`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `data_update_run_id` | TEXT NOT NULL FK | Parent run. |
| `data_update_item_id` | TEXT NULL FK | Exact failed item attempt when available. |
| `instrument_id` | TEXT NULL FK | Null for run-wide failure. |
| `item_key` | TEXT NULL | Date/event/source row identifier. |
| `error_kind` | TEXT NOT NULL | `Http`, `RateLimit`, `Timeout`, `InvalidData`, `MissingData`, `ProviderChanged`, `Cancelled`, `DatabaseLocked`, `Unknown`. |
| `message` | TEXT NOT NULL | Sanitized, bounded diagnostic. |
| `occurred_at_utc` | TEXT NOT NULL | Failure instant. |

## 4. Market facts

### 4.1 `daily_prices`

Stable logical identity for one provider/session bar.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `bar_date` | TEXT NOT NULL | Market session date. |
| `provider` | TEXT NOT NULL | Price provider. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(instrument_id, bar_date, provider)`.

### 4.2 `daily_price_revisions`

Unadjusted provider data is the source of truth. A physical uniqueness rule includes the revision so corrections do not conflict with the logical instrument/date uniqueness.

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Logical parent is `daily_price_id`. |
| `daily_price_id` | TEXT NOT NULL FK | References `daily_prices`. |
| `provider_symbol` | TEXT NOT NULL | Symbol used in the request. |
| `open` | TEXT NOT NULL | Positive canonical decimal. |
| `high` | TEXT NOT NULL | Positive; application checks `high >= open/close/low`. |
| `low` | TEXT NOT NULL | Positive. |
| `close` | TEXT NOT NULL | Positive. |
| `volume` | INTEGER NOT NULL | Non-negative. |
| `provider_adjclose` | TEXT NULL | Reference/diagnostics only; prohibited as analysis input. |
| `currency` | TEXT NOT NULL | ISO 4217. |
| `bar_status` | TEXT NOT NULL | `Provisional`, `Confirmed`, `Corrected`, `Invalid`. |
| `provider_event_id` | TEXT NULL | Provider revision/event identity when available. |

Unique: `(daily_price_id, revision_no)`. Idempotency additionally checks the content hash of the current leaf revision.

### 4.3 `price_history_assessments`

Append-only evidence for history completeness; a missing record is not treated as complete.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `provider` | TEXT NOT NULL | Price provider. |
| `first_valid_bar_date` | TEXT NULL | Earliest verified valid bar. |
| `last_valid_bar_date` | TEXT NULL | Latest verified valid bar. |
| `valid_bar_count` | INTEGER NOT NULL | Non-negative. |
| `completeness_status` | TEXT NOT NULL | `CompleteFromListing`, `Incomplete`, `Unverified`, `Invalid`. |
| `listing_date_evidence` | TEXT NULL | Source/reason supporting the start. |
| `reason` | TEXT NULL | Required unless complete. |
| `assessed_at_utc` | TEXT NOT NULL | Assessment instant. |
| `algorithm_version` | TEXT NOT NULL | Coverage checker version. |
| `source_artifact_id` | TEXT NULL FK | Evidence. |

### 4.4 `price_revision_sets`

Content-addressed exact price-input sets. They prevent each daily scan from copying the complete listing-history member list while avoiding dependence on re-running an old selector implementation.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `provider` | TEXT NOT NULL | Price provider. |
| `parent_set_id` | TEXT NULL FK self | Prior exact set; null for the initial checkpoint. |
| `first_bar_date` | TEXT NULL | First member date. |
| `last_bar_date` | TEXT NULL | Last member date. |
| `bar_count` | INTEGER NOT NULL | Final member count. |
| `set_sha256` | TEXT NOT NULL | Hash of the final ordered natural-key/revision-content-hash set. |
| `selector_version` | TEXT NOT NULL | Version that created, but is not needed to identify, the exact set. |
| `selected_available_cutoff_at_utc` | TEXT NOT NULL | Information cutoff used to create the set. |
| `selected_recorded_cutoff_at_utc` | TEXT NOT NULL | Transaction cutoff used to create the set. |
| `point_in_time_status` | TEXT NOT NULL | `Verified`, `Unverified`. |
| `created_at_utc` | TEXT NOT NULL | Set creation. |

Unique: `(instrument_id, provider, set_sha256)`. The initial checkpoint records every selected bar as an `Add`; later sets normally contain only new or corrected members. Branches are allowed for historical replay, but cycles are rejected.

### 4.5 `price_revision_set_changes`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `price_revision_set_id` | TEXT NOT NULL FK | Child set. |
| `operation` | TEXT NOT NULL | `Add`, `Replace`, `Remove`. |
| `daily_price_revision_id` | TEXT NULL FK | New exact revision for Add/Replace. |
| `replaced_daily_price_revision_id` | TEXT NULL FK | Old exact revision for Replace/Remove. |
| `bar_date` | TEXT NOT NULL | Member key used for deterministic reconstruction. |
| `ordinal` | INTEGER NOT NULL | Stable change order. |

Unique: `(price_revision_set_id, ordinal)` and `(price_revision_set_id, bar_date)`. Checks enforce the required new/old IDs for each operation. Reconstructing from the nearest checkpoint through the parent chain must yield `set_sha256`; checkpoints may be added for performance without changing existing sets.

### 4.6 `corporate_actions`

Stable logical event identity. Events from different providers are not silently merged.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `provider` | TEXT NOT NULL | Source provider. |
| `source_event_id` | TEXT NULL | Provider ID when present. |
| `derived_event_key` | TEXT NOT NULL | Deterministic fallback key. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Partial unique indexes are explicit: `UNIQUE(instrument_id, provider, source_event_id) WHERE source_event_id IS NOT NULL` and `UNIQUE(instrument_id, provider, derived_event_key) WHERE source_event_id IS NULL`.

### 4.7 `corporate_action_revisions`

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Logical parent is `corporate_action_id`. |
| `corporate_action_id` | TEXT NOT NULL FK | Stable event. |
| `action_type` | TEXT NOT NULL | `Split`, `Consolidation`, `CashDividend`, `Unsupported`. |
| `status` | TEXT NOT NULL | `Announced`, `Confirmed`, `Corrected`, `Cancelled`. |
| `effective_date` | TEXT NOT NULL | Effective market date. |
| `announced_at_utc` | TEXT NULL | Source announcement instant. |
| `ratio_numerator` | INTEGER NULL | New-share numerator; positive for split/consolidation. |
| `ratio_denominator` | INTEGER NULL | Old-share denominator; positive for split/consolidation. |
| `cash_amount_per_share` | TEXT NULL | Gross cash dividend per share. |
| `currency` | TEXT NULL | Required with cash amount. |
| `point_in_time_status` | TEXT NOT NULL | `Verified`, `Unverified`. |
| `notes` | TEXT NULL | Bounded source note. |

Checks require both ratio integers only for split/consolidation and cash amount/currency only for cash dividend. Cancellation is represented by a new revision, never deletion.

### 4.8 `margin_eligibility_records`

Stable source record for an eligibility/regulation period. A corrected effective date remains in the same revision chain.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `provider` | TEXT NOT NULL | Source provider. |
| `source_record_key` | TEXT NOT NULL | Provider key or deterministic fallback. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(instrument_id, provider, source_record_key)`.

### 4.9 `margin_eligibility_revisions`

Technical Short candidacy is independent of these market/contract facts.

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Logical parent is `margin_eligibility_record_id`. |
| `margin_eligibility_record_id` | TEXT NOT NULL FK | Stable source record. |
| `effective_from_date` | TEXT NOT NULL | Inclusive. |
| `effective_to_date` | TEXT NULL | Inclusive. |
| `standardized_margin_status` | TEXT NOT NULL | `Eligible`, `Ineligible`, `Restricted`, `Unknown`. |
| `loan_stock_status` | TEXT NOT NULL | `Eligible`, `Ineligible`, `Restricted`, `Unknown`. |
| `long_open_status` | TEXT NOT NULL | `Allowed`, `Prohibited`, `Restricted`, `Unknown`. |
| `short_open_status` | TEXT NOT NULL | `Allowed`, `Prohibited`, `Restricted`, `Unknown`. |
| `regulation_codes_json` | TEXT NOT NULL | Normalized array of source regulations. |
| `notes` | TEXT NULL | Source explanation. |

### 4.10 `published_margin_costs`

Stable logical identity for one provider-published market cost fact.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `provider` | TEXT NOT NULL | JPX/JSF etc. |
| `cost_type` | TEXT NOT NULL | Initially `Backwardation`. |
| `source_record_key` | TEXT NOT NULL | Provider ID or deterministic fallback. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(instrument_id, provider, cost_type, source_record_key)`.

### 4.11 `published_margin_cost_revisions`

Provider-published market facts such as standardized-margin backwardation. These are not position cost ledger entries.

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Logical parent is `published_margin_cost_id`. |
| `published_margin_cost_id` | TEXT NOT NULL FK | Stable published fact. |
| `application_date` | TEXT NOT NULL | Source application date. |
| `period_start_date` | TEXT NULL | Covered period. |
| `period_end_date` | TEXT NULL | Covered period. |
| `included_days` | INTEGER NULL | Days already included in the published unit. |
| `publication_status` | TEXT NOT NULL | `KnownAmount`, `KnownZero`, `NotOccurred`, `Unpublished`, `FetchFailed`, `Unknown`. |
| `amount_per_share` | TEXT NULL | Present only for known amount/zero. |
| `currency` | TEXT NULL | Required with amount. |
| `published_at_utc` | TEXT NULL | Provider publication instant. |
| `unit` | TEXT NULL | Explicit source unit; states whether days are already included. |

`KnownZero` is different from `NotOccurred`, and neither is inferred from `NULL`.

### 4.12 `fundamental_records`

Stable provider snapshot identity, separated so corrections to an as-of date stay in one chain.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `provider` | TEXT NOT NULL | Fundamental provider. |
| `source_record_key` | TEXT NOT NULL | Provider ID or deterministic fallback. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(instrument_id, provider, source_record_key)`.

### 4.13 `fundamental_revisions`

| Column | Type | Constraints/notes |
|---|---|---|
| Common revision columns | — | Logical parent is `fundamental_record_id`. |
| `fundamental_record_id` | TEXT NOT NULL FK | Stable provider snapshot. |
| `as_of_date` | TEXT NOT NULL | Provider data date. |
| `fiscal_period_end_date` | TEXT NULL | Fiscal context. |
| `per` | TEXT NULL | Missing remains null. |
| `pbr` | TEXT NULL | Missing remains null. |
| `market_cap` | TEXT NULL | Canonical decimal. |
| `currency` | TEXT NULL | Required with market cap. |
| `missing_fields_json` | TEXT NOT NULL | Explicit normalized list. |
| `payload_json` | TEXT NOT NULL | Schema-versioned structured fields. |

## 5. Strategy, analysis, and candidates

### 5.1 `strategy_parameter_snapshots`

The source of current parameters remains configuration. This table freezes the fully resolved values used by a run.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `strategy_key` | TEXT NOT NULL | Stable logical strategy identifier. |
| `strategy_version` | TEXT NOT NULL | Human/domain version. |
| `schema_version` | TEXT NOT NULL | Parameter JSON schema version. |
| `algorithm_version` | TEXT NOT NULL | Candidate/risk algorithm bundle. |
| `parameters_json` | TEXT NOT NULL | Defaults and overrides already resolved. |
| `parameters_sha256` | TEXT NOT NULL | Hash of normalized JSON plus versions. |
| `captured_at_utc` | TEXT NOT NULL | Snapshot instant. |
| `source_description` | TEXT NULL | Non-secret configuration origin. |

Unique: `(strategy_key, parameters_sha256)`.

### 5.2 `analysis_runs`

One immutable completed scan/re-evaluation run. Progress fields may transition only until terminal status.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `evaluation_bar_date` | TEXT NOT NULL | Latest confirmed bar date `D`. |
| `analyzed_at_utc` | TEXT NOT NULL | Information cutoff `A`. |
| `recorded_cutoff_at_utc` | TEXT NOT NULL | Transaction-time cutoff excluding later ingestions. |
| `run_mode` | TEXT NOT NULL | `Daily`, `Manual`, `Backtest`. |
| `status` | TEXT NOT NULL | `Queued`, `Running`, `Succeeded`, `PartiallySucceeded`, `Failed`, `Cancelled`. |
| `strategy_parameter_snapshot_id` | TEXT NOT NULL FK | Frozen parameters. |
| `point_in_time_status` | TEXT NOT NULL | `Verified`, `Unverified`. |
| `price_selector_version` | TEXT NOT NULL | Deterministic revision selection algorithm. |
| `adjustment_engine_version` | TEXT NOT NULL | Corporate-action adjustment version. |
| `indicator_engine_version` | TEXT NOT NULL | Includes `ema-sma-seed-v1`/ATR/MACD versions. |
| `candidate_engine_version` | TEXT NOT NULL | Filter/scoring version. |
| `market_calendar_version_id` | TEXT NOT NULL FK | References `market_calendar_versions(id)`. |
| `application_version` | TEXT NOT NULL | App/build identifier. |
| `started_at_utc` | TEXT NULL | Operational start. |
| `completed_at_utc` | TEXT NULL | Terminal time. |
| `total_count` | INTEGER NOT NULL DEFAULT 0 | Non-negative. |
| `success_count` | INTEGER NOT NULL DEFAULT 0 | Non-negative. |
| `failure_count` | INTEGER NOT NULL DEFAULT 0 | Non-negative. |
| `summary` | TEXT NULL | Sanitized summary. |

Completed runs are immutable; a recalculation creates a new run.

### 5.3 `analysis_input_manifests`

One manifest per `(run, instrument, price provider)`. It references a content-addressed `price_revision_set` whose checkpoint/delta chain contains every exact revision member. Selection cutoffs and selector version remain audit evidence, but replay does not depend on retaining the old selector implementation or resolving equal wall-clock timestamps.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `analysis_run_id` | TEXT NOT NULL FK | Run. |
| `instrument_id` | TEXT NOT NULL FK | Subject. |
| `price_provider` | TEXT NOT NULL | Provider selected for this subject. |
| `price_revision_set_id` | TEXT NOT NULL FK | Exact member set. Must match instrument/provider and set hash. |
| `first_bar_date` | TEXT NULL | First selected bar. |
| `last_bar_date` | TEXT NULL | Last selected bar, no later than `D`. |
| `bar_count` | INTEGER NOT NULL | Selected valid bars. |
| `required_bar_count` | INTEGER NOT NULL | Required by this strategy. |
| `history_status` | TEXT NOT NULL | `Complete`, `InsufficientHistory`, `HistoryIncomplete`, `Invalid`. |
| `point_in_time_status` | TEXT NOT NULL | `Verified`, `Unverified`. |
| `selection_basis` | TEXT NOT NULL | `ObservedAt` for live runs or `SourceAvailableAt` for verified historical replay. |
| `selection_rule_version` | TEXT NOT NULL | Exact selector contract that formed the referenced set. |
| `selected_recorded_cutoff_at_utc` | TEXT NOT NULL | Excludes rows ingested later. |
| `selected_available_cutoff_at_utc` | TEXT NOT NULL | Information cutoff. |
| `price_revision_set_sha256` | TEXT NOT NULL | Ordered natural-key/content-hash set hash. |
| `corporate_action_set_sha256` | TEXT NOT NULL | Ordered applied/excluded action set hash. |
| `manifest_sha256` | TEXT NOT NULL | Hash over all fields and adjustment applications. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(analysis_run_id, instrument_id, price_provider)`. A Repository test reconstructs the referenced checkpoint/delta chain and compares its final hash to both the set and manifest. If availability cannot be proven, the set/manifest is retained as `Unverified` and cannot be used for formal backtest results.

### 5.4 `analysis_action_applications`

Exact action revision and adjustment evidence used by a manifest.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `analysis_input_manifest_id` | TEXT NOT NULL FK | Manifest. |
| `corporate_action_revision_id` | TEXT NOT NULL FK | Exact action revision. |
| `application_status` | TEXT NOT NULL | `Applied`, `ExcludedNotEffective`, `ExcludedUnavailable`, `Unsupported`, `ReconciliationRequired`. |
| `reference_price_revision_id` | TEXT NULL FK | Ex-dividend reference close revision when used. |
| `price_factor` | TEXT NULL | Factor applied to historical OHLC. |
| `volume_factor` | TEXT NULL | Factor applied to historical volume. |
| `cumulative_price_factor` | TEXT NULL | Factor after this action in deterministic order. |
| `cumulative_volume_factor` | TEXT NULL | Factor after this action. |
| `reason` | TEXT NOT NULL | Explainable application/exclusion reason. |
| `ordinal` | INTEGER NOT NULL | Deterministic order. |

Unique: `(analysis_input_manifest_id, corporate_action_revision_id)` and `(analysis_input_manifest_id, ordinal)`.

### 5.5 `technical_analysis_results`

Stores candidate and non-candidate outcomes, including explicit exclusion reasons.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `analysis_run_id` | TEXT NOT NULL FK | Run. |
| `analysis_input_manifest_id` | TEXT NOT NULL FK | Exact subject input. |
| `instrument_id` | TEXT NOT NULL FK | Redundant for indexed queries; must match manifest. |
| `position_side` | TEXT NOT NULL | `Long`, `Short`. |
| `signal_purpose` | TEXT NOT NULL | Must equal `Entry` in the initial release. Instrument-level `Exit` rows are reserved for a future additive migration with an explicit position-evaluation relationship. |
| `outcome` | TEXT NOT NULL | `Candidate`, `NotCandidate`, `InsufficientHistory`, `HistoryIncomplete`, `InvalidData`, `PointInTimeUnverified`, `ReconciliationRequired`, `Failed`. |
| `reason_summary` | TEXT NOT NULL | Displayable reason. |
| `reasons_json` | TEXT NOT NULL | Ordered structured reasons. |
| `calculation_start_bar_date` | TEXT NULL | Fixed EMA/indicator origin. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(analysis_run_id, instrument_id, position_side, signal_purpose)`.

Initial-release candidate scanning writes only `Entry` results. Position-specific `Exit`/`Hold`/`StopLoss`/`TakeProfit` decisions, which depend on lot, partial-exit, risk-plan, and cost state, are stored in `position_evaluations`; `technical_analysis_results` does not duplicate them at instrument scope.

### 5.6 `indicator_results`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `technical_analysis_result_id` | TEXT NOT NULL FK | Parent result. |
| `indicator_key` | TEXT NOT NULL | `MACD`, `EMA20`, `EMA50`, `EMA200`, `VolumeAverage20`, `VolumeRatio`, `ATR14`, etc. |
| `algorithm_id` | TEXT NOT NULL | Example `ema-sma-seed-v1`. |
| `parameters_json` | TEXT NOT NULL | Periods/method inputs. |
| `values_json` | TEXT NOT NULL | Schema-versioned full-precision scalar outputs/states actually used at `evaluation_bar_date`, plus only the prior-bar values/states required for a crossover decision. Never the full time series. |
| `calculation_start_bar_date` | TEXT NOT NULL | Fixed origin. |
| `input_sha256` | TEXT NOT NULL | Indicator-specific input hash. |
| `ordinal` | INTEGER NOT NULL | Stable display order. |

Unique: `(technical_analysis_result_id, indicator_key)` and `(technical_analysis_result_id, ordinal)`.

An indicator time series is reconstructed from the exact `analysis_input_manifest`, parameters, and frozen engine/algorithm versions when needed. Persisting the complete per-bar series in `values_json` is prohibited; the stored decision scalars preserve the displayed reason while bounding daily database growth.

### 5.7 `candidate_results`

Only qualified `Entry` candidates receive this row; the technical result remains the source of the complete evaluated population. A DB constraint rejects a referenced technical result whose `signal_purpose` is not `Entry`.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `technical_analysis_result_id` | TEXT NOT NULL FK UNIQUE | Qualified result. |
| `score` | INTEGER NOT NULL | 0 to 100. It is not a probability. |
| `confidence` | TEXT NOT NULL | `High`, `Medium`, `Low`. |
| `primary_reason` | TEXT NOT NULL | Display reason. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

### 5.8 `candidate_score_components`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `candidate_result_id` | TEXT NOT NULL FK | Candidate. |
| `component_key` | TEXT NOT NULL | Stable scoring component. |
| `matched` | INTEGER NOT NULL | Boolean. |
| `raw_value_json` | TEXT NOT NULL | Strength/input evidence. |
| `weight` | TEXT NOT NULL | Frozen decimal weight. |
| `awarded_score` | TEXT NOT NULL | Unrounded contribution. |
| `reason` | TEXT NOT NULL | Explanation. |
| `ordinal` | INTEGER NOT NULL | Stable order. |

Unique: `(candidate_result_id, component_key)` and `(candidate_result_id, ordinal)`. Application validation requires rounded component totals to agree with `candidate_results.score` under the stored scoring version.

## 6. Positions, executions, and risk

### 6.1 `positions`

Stable grouping identity. It does not contain mutable current quantity or average price as a source of truth.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `instrument_id` | TEXT NOT NULL FK | Instrument. |
| `position_side` | TEXT NOT NULL | `Long`, `Short`. |
| `strategy_parameter_snapshot_id` | TEXT NULL FK | Null for an unrelated manual position. |
| `origin_candidate_result_id` | TEXT NULL FK | Input context only. Never authorizes an execution. |
| `created_at_utc` | TEXT NOT NULL | User-created record time. |

Multiple positions for the same instrument/side are allowed; the application does not silently merge strategies or accounts.

### 6.2 `position_state_revisions`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Immutable revision UUID. |
| `revision_no` | INTEGER NOT NULL | Monotonic within `position_id`. |
| `supersedes_id` | TEXT NULL FK self | Direct predecessor. |
| `content_sha256` | TEXT NOT NULL | Hash of normalized state content. |
| `recorded_at_utc` | TEXT NOT NULL | Persistence time. |
| `position_id` | TEXT NOT NULL FK | Position. |
| `status` | TEXT NOT NULL | `Open`, `Closed`, `Archived`. |
| `reconciliation_status` | TEXT NOT NULL | `Clear`, `Required`, `InProgress`, `Resolved`. |
| `effective_at_utc` | TEXT NOT NULL | State effective instant. |
| `memo` | TEXT NULL | User note snapshot. |
| `reason` | TEXT NOT NULL | User action or derived reconciliation reason. |

Unique: `(position_id, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. Current display state is the leaf revision. Quantities and prices are projected from executions, allocations, and adjustments, not accepted from this table. This user/operational revision has no source-availability fields; `effective_at_utc` and `recorded_at_utc` retain their distinct meanings.

### 6.3 `trade_executions`

Stable logical identity for a user-confirmed broker execution record.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `position_id` | TEXT NOT NULL FK | Position. |
| `execution_kind` | TEXT NOT NULL | `Open`, `Close`. |
| `origin` | TEXT NOT NULL | Must equal `UserConfirmed`; DB `CHECK` prevents analysis/AI/system origins. |
| `candidate_context_id` | TEXT NULL FK | References `candidate_results(id)`; optional input assistance provenance. |
| `created_at_utc` | TEXT NOT NULL | Logical record creation. |

No foreign key or trigger creates this row from a signal, price, AI result, deadline, or cost warning.

### 6.4 `trade_execution_revisions`

Original execution values are immutable. A correction or void is a new revision.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Immutable revision UUID. |
| `revision_no` | INTEGER NOT NULL | Monotonic within `trade_execution_id`. |
| `supersedes_id` | TEXT NULL FK self | Direct predecessor. |
| `content_sha256` | TEXT NOT NULL | Hash of the normalized user-entered values. |
| `source_artifact_id` | TEXT NULL FK | Optional broker-statement evidence. |
| `recorded_at_utc` | TEXT NOT NULL | Persistence time. |
| `trade_execution_id` | TEXT NOT NULL FK | Logical execution. |
| `executed_at_utc` | TEXT NOT NULL | User-entered/selected broker execution time. |
| `price` | TEXT NOT NULL | Positive canonical decimal. |
| `quantity` | INTEGER NOT NULL | Positive whole shares. |
| `currency` | TEXT NOT NULL | ISO 4217. |
| `record_disposition` | TEXT NOT NULL | `Effective`, `Voided`. Current values require an unsuperseded `Effective` leaf. |
| `change_kind` | TEXT NOT NULL | `Initial`, `Correction`, `Void`. |
| `broker` | TEXT NULL | User-confirmed broker name. |
| `external_reference` | TEXT NULL | Optional non-secret statement/execution reference. |
| `user_note` | TEXT NULL | Note snapshot. |
| `user_confirmed_at_utc` | TEXT NOT NULL | Explicit confirmation time. |
| `correction_reason` | TEXT NULL | Required for revision > 1 or void. |

The revision content hash covers all user-entered values. Changing instrument/side requires voiding and registering a new logical record under the correct position.

Unique: `(trade_execution_id, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`.

### 6.5 `margin_lots`

One opening execution creates at most one MarginLot. A lot's current quantity is derived in the correct corporate-action unit.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `position_id` | TEXT NOT NULL FK | Position. |
| `opening_trade_execution_id` | TEXT NOT NULL FK UNIQUE | Must reference an `Open` execution. |
| `initial_opening_trade_execution_revision_id` | TEXT NOT NULL FK UNIQUE | Exact effective execution revision used when the lot was created. |
| `created_at_utc` | TEXT NOT NULL | Lot creation time. |

### 6.6 `margin_lot_contract_revisions`

Contract terms are frozen per lot; a generic product template is not looked up retroactively.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Immutable revision UUID. |
| `revision_no` | INTEGER NOT NULL | Monotonic within `margin_lot_id`. |
| `supersedes_id` | TEXT NULL FK self | Direct predecessor. |
| `content_sha256` | TEXT NOT NULL | Hash of normalized contract content. |
| `source_artifact_id` | TEXT NULL FK | Optional broker-statement evidence. |
| `recorded_at_utc` | TEXT NOT NULL | Persistence time. |
| `margin_lot_id` | TEXT NOT NULL FK | Lot. |
| `opening_trade_execution_revision_id` | TEXT NOT NULL FK | Exact opening values/instant used for this contract snapshot. |
| `margin_type` | TEXT NOT NULL | `Standardized`, `General`, `Unknown`. |
| `broker` | TEXT NOT NULL | User-confirmed broker. |
| `product_name` | TEXT NOT NULL | User-confirmed product. |
| `effective_from_date` | TEXT NOT NULL | Inclusive contract validity. |
| `effective_to_date` | TEXT NULL | Inclusive. |
| `term_type` | TEXT NOT NULL | `FixedDate`, `NoFixedTerm`, `Unknown`. |
| `final_repayment_at_utc` | TEXT NULL | Required only for `FixedDate`; broker-confirmed, never auto-filled as trade date + 6 months. |
| `buyer_interest_rate` | TEXT NULL | Contract snapshot. |
| `stock_lending_rate` | TEXT NULL | Contract snapshot. |
| `rate_unit` | TEXT NULL | Explicit annual/daily/etc. unit. |
| `contract_currency` | TEXT NOT NULL | ISO 4217 settlement currency. |
| `day_count_convention` | TEXT NULL | Contract convention. |
| `special_fee_policy_json` | TEXT NOT NULL | Broker-specific fee rules. |
| `rights_processing_json` | TEXT NOT NULL | Dividend/right handling terms. |
| `confirmed_at_utc` | TEXT NOT NULL | User/broker confirmation instant. |
| `evidence` | TEXT NOT NULL | Statement/document description. |
| `change_kind` | TEXT NOT NULL | `Initial`, `ContractAmendment`, `InputCorrection`. |

Unique: `(margin_lot_id, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. A `CHECK` enforces the deadline/term combination. Changing `margin_type` after `Initial` is rejected except an explicit `InputCorrection`, which also places the position into reconciliation until reviewed. This user/broker-confirmed contract uses `confirmed_at_utc` instead of source-availability/first-observed fields.

### 6.7 `lot_allocation_revisions`

Explicitly allocates a closing execution to a lot. No FIFO or implicit allocation is created.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Revision ID. |
| `allocation_key` | TEXT NOT NULL | Stable logical allocation UUID. |
| `revision_no` | INTEGER NOT NULL | Starts at 1. |
| `supersedes_id` | TEXT NULL FK self | Correction chain. |
| `closing_trade_execution_id` | TEXT NOT NULL FK | Must reference `Close`. |
| `closing_trade_execution_revision_id` | TEXT NOT NULL FK | Exact effective close revision whose quantity is allocated. |
| `margin_lot_id` | TEXT NOT NULL FK | User-selected lot. |
| `quantity` | INTEGER NOT NULL | Positive executed shares in the unit effective at execution time. |
| `record_disposition` | TEXT NOT NULL | `Effective`, `Voided`. |
| `change_kind` | TEXT NOT NULL | `Initial`, `Correction`, `Void`. |
| `user_confirmed_at_utc` | TEXT NOT NULL | Explicit allocation confirmation. |
| `correction_reason` | TEXT NULL | Required after revision 1. |
| `content_sha256` | TEXT NOT NULL | Normalized content hash. |
| `recorded_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(allocation_key, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. In one transaction, application validation ensures unsuperseded `Effective` allocation leaves equal the referenced unsuperseded `Effective` close revision quantity and never exceed each lot's adjusted remaining quantity.

### 6.8 `position_adjustments`

Per-lot corporate-action conversion. It never edits an execution or creates an execution.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Revision UUID. |
| `adjustment_key` | TEXT NOT NULL | Stable logical adjustment UUID. |
| `revision_no` | INTEGER NOT NULL | Starts at 1. |
| `supersedes_id` | TEXT NULL FK self | User reconciliation/correction chain. |
| `replaces_adjustment_key` | TEXT NULL | Prior logical adjustment replaced because its corporate-action revision was corrected. |
| `position_id` | TEXT NOT NULL FK | Position. |
| `margin_lot_id` | TEXT NOT NULL FK | Affected lot. |
| `corporate_action_revision_id` | TEXT NOT NULL FK | Exact action evidence. |
| `status` | TEXT NOT NULL | `Applied`, `ReconciliationRequired`, `Resolved`, `Reversed`. |
| `effective_date` | TEXT NOT NULL | Action effective date. |
| `quantity_factor` | TEXT NULL | New/old share factor. |
| `price_factor` | TEXT NULL | Reciprocal price factor. |
| `before_quantity` | TEXT NOT NULL | Current-basis quantity before adjustment. |
| `after_quantity` | TEXT NULL | Null if reconciliation required. |
| `before_basis_price` | TEXT NOT NULL | Current-basis acquisition price before. |
| `after_basis_price` | TEXT NULL | Converted price. |
| `before_fixed_atr` | TEXT NULL | Fixed ATR before; null only when no valid risk basis exists. |
| `after_fixed_atr` | TEXT NULL | Converted ATR. |
| `before_stop_price` | TEXT NULL | Active stop before. |
| `after_stop_price` | TEXT NULL | Converted stop. |
| `before_take_profit_price` | TEXT NULL | Active target before. |
| `after_take_profit_price` | TEXT NULL | Converted target. |
| `details_json` | TEXT NOT NULL | Rounding, fractional-share, cash/reconciliation details. |
| `confirmed_at_utc` | TEXT NULL | User reconciliation confirmation when needed. |
| `content_sha256` | TEXT NOT NULL | Normalized adjustment hash. |
| `recorded_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(adjustment_key, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. The initial logical key is unique for `(margin_lot_id, corporate_action_revision_id)`. User reconciliation appends a revision under that key. A source-action correction atomically appends a `Reversed` leaf to the old key and an initial row under a new key whose `replaces_adjustment_key` points to the old key. Projection applies only unsuperseded `Applied`/`Resolved` leaves corresponding to the applicable corporate-action revision leaf at the requested cutoff.

`before_stop_price`/`after_stop_price` and the corresponding take-profit fields are conversion audit evidence, not the current projection source. When a valid risk basis exists, the same transaction appends a `risk_plan_revisions` row with `plan_reason = CorporateActionConversion`; its trigger references this adjustment, its predecessor values equal the adjustment's `before_*` values, and its new values equal the `after_*` values. The current stop/take-profit projection reads only the risk-plan leaf.

### 6.9 `risk_basis_snapshots`

Risk bases are lot-specific so multiple entries are not silently averaged. They form an append-only revision chain because a user may correct an opening execution after the initial basis was captured.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Revision UUID. |
| `margin_lot_id` | TEXT NOT NULL FK | Lot. |
| `revision_no` | INTEGER NOT NULL | Monotonic within the lot. |
| `supersedes_id` | TEXT NULL FK self | Prior risk basis. |
| `opening_trade_execution_revision_id` | TEXT NOT NULL FK | Exact effective opening values used by this basis. |
| `origin_candidate_result_id` | TEXT NULL FK | Candidate-linked basis. |
| `strategy_parameter_snapshot_id` | TEXT NULL FK | Frozen strategy/risk parameters. |
| `analysis_input_manifest_id` | TEXT NULL FK | Exact ATR input where available. |
| `entry_basis_price` | TEXT NOT NULL | Same original/current unit as creation time. |
| `atr_reference_bar_date` | TEXT NOT NULL | Fixed reference date. |
| `fixed_atr` | TEXT NOT NULL | Positive. |
| `atr_period` | INTEGER NOT NULL | Initially 14. |
| `atr_algorithm_id` | TEXT NOT NULL | Wilder implementation version. |
| `stop_multiplier` | TEXT NOT NULL | Side-specific frozen `k`. |
| `risk_amount_r` | TEXT NOT NULL | `k * fixed_atr`. |
| `partial_take_profit_r_multiple` | TEXT NOT NULL | Initially 1.5. |
| `partial_take_profit_fraction` | TEXT NOT NULL | Initially 0.50. |
| `initial_stop_price` | TEXT NOT NULL | Frozen candidate. |
| `initial_take_profit_price` | TEXT NOT NULL | Frozen candidate. |
| `content_sha256` | TEXT NOT NULL | Normalized risk basis hash. |
| `created_at_utc` | TEXT NOT NULL | Snapshot time. |

Unique: `(margin_lot_id, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. Correcting the opening execution places the position into reconciliation and appends a new basis plus dependent risk-plan revisions; old bases remain tied to the old execution revision.

### 6.10 `risk_plan_revisions`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | Immutable revision UUID. |
| `revision_no` | INTEGER NOT NULL | Monotonic within `risk_basis_snapshot_id`. |
| `supersedes_id` | TEXT NULL FK self | Direct predecessor. |
| `content_sha256` | TEXT NOT NULL | Hash of normalized plan content. |
| `recorded_at_utc` | TEXT NOT NULL | Persistence time. |
| `risk_basis_snapshot_id` | TEXT NOT NULL FK | Lot risk basis. |
| `stop_price` | TEXT NOT NULL | Current stop candidate. |
| `take_profit_price` | TEXT NOT NULL | Current partial take-profit candidate. |
| `trigger_trade_execution_id` | TEXT NULL FK | User-registered partial close that permits breakeven revision. |
| `trigger_lot_allocation_revision_id` | TEXT NULL FK | Exact effective allocation proving this lot was partially closed. |
| `trigger_position_adjustment_id` | TEXT NULL FK | Exact corporate-action conversion when `plan_reason` is `CorporateActionConversion`. |
| `plan_reason` | TEXT NOT NULL | `Initial`, `PartialExitBreakeven`, `CorporateActionConversion`, `UserCorrection`. |
| `effective_at_utc` | TEXT NOT NULL | Plan effective instant. |
| `is_cost_adjusted` | INTEGER NOT NULL DEFAULT 0 | Must remain 0 for the defined price-based plan. |

Unique: `(risk_basis_snapshot_id, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. A DB `CHECK` requires `is_cost_adjusted = 0`. A mere 1.5R price touch cannot create `PartialExitBreakeven`; both trigger execution and exact unsuperseded `Effective` allocation revision are required. `CorporateActionConversion` requires an exact effective `trigger_position_adjustment_id` and the atomic before/after equality described in section 6.8. This derived plan uses `effective_at_utc` and does not carry source-availability fields.

### 6.11 `position_evaluation_input_manifests`

Freezes every position-side revision used by a holding re-evaluation. Lists are small compared with price history and therefore retain exact IDs in normalized JSON envelopes as well as hashes.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `analysis_run_id` | TEXT NOT NULL FK | Run. |
| `position_id` | TEXT NOT NULL FK | Position. |
| `analysis_input_manifest_id` | TEXT NOT NULL FK | Exact market/price/action input manifest. |
| `current_price_revision_id` | TEXT NOT NULL FK | Exact evaluation bar revision. |
| `trade_execution_revision_ids_json` | TEXT NOT NULL | Ordered exact effective revision IDs. |
| `lot_allocation_revision_ids_json` | TEXT NOT NULL | Ordered exact allocation leaves. |
| `position_adjustment_ids_json` | TEXT NOT NULL | Ordered exact adjustment leaves. |
| `contract_revision_ids_json` | TEXT NOT NULL | Exact lot contract revisions. |
| `risk_basis_snapshot_ids_json` | TEXT NOT NULL | Exact per-lot basis revisions. |
| `risk_plan_revision_ids_json` | TEXT NOT NULL | Exact active plan leaves. |
| `margin_cost_observation_ids_json` | TEXT NOT NULL | Exact estimate/confirmed inputs. |
| `projection_version` | TEXT NOT NULL | Lot/unit/P&L projection algorithm. |
| `recorded_cutoff_at_utc` | TEXT NOT NULL | Transaction cutoff. |
| `manifest_sha256` | TEXT NOT NULL | Hash of all natural keys, ordered IDs/content hashes, and versions. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(analysis_run_id, position_id)`. Repository verification loads every listed row, checks that it belongs to the position/lot graph, and recomputes the manifest hash.

### 6.12 `position_evaluations`

Append-only holding re-evaluation result; never generates a close execution. This is the initial-release source of position-specific `Exit` decisions; two positions in the same instrument are evaluated independently from their exact lot/risk/cost manifests.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `analysis_run_id` | TEXT NOT NULL FK | Run. |
| `position_id` | TEXT NOT NULL FK | Position. |
| `position_evaluation_input_manifest_id` | TEXT NOT NULL FK UNIQUE | Exact position/market/cost input set. |
| `evaluation_bar_date` | TEXT NOT NULL | Evaluated bar date. |
| `exit_decision` | TEXT NOT NULL | `Hold`, `TakeProfit`, `StopLoss`, `Exit`. |
| `reason_summary` | TEXT NOT NULL | Display explanation. |
| `reasons_json` | TEXT NOT NULL | Structured reasons. |
| `lot_evaluations_json` | TEXT NOT NULL | Per-lot bases/lines; avoids silent averaging. |
| `current_quantity` | TEXT NOT NULL | Projected current-basis quantity. |
| `price_pnl` | TEXT NULL | Price-only reference P/L. |
| `confirmed_cost_pnl` | TEXT NULL | Null unless confirmed ledger coverage is sufficient. |
| `estimated_net_pnl` | TEXT NULL | Null when required estimates are unavailable. |
| `cost_to_r_ratio` | TEXT NULL | Null, not zero, when inputs are missing. |
| `partial_exit_quantity` | INTEGER NULL | Candidate whole shares. |
| `partial_exit_status` | TEXT NOT NULL | `NotApplicable`, `Candidate`, `NotFeasible`. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(analysis_run_id, position_id)`.

## 7. Margin cost ledger

### 7.1 `margin_cost_items`

Stable logical identity for one lot/cost/period item. A confirmed observation does not overwrite or double-count an estimate.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `margin_lot_id` | TEXT NOT NULL FK | Lot. |
| `cost_type` | TEXT NOT NULL | `BuyerInterest`, `StockLendingFee`, `Backwardation`, `DividendEquivalent`, `BrokerSpecific`, `Other`. |
| `occurrence_key` | TEXT NOT NULL | Deterministic source/contract occurrence key preventing duplicate logical items. |
| `period_start_date` | TEXT NOT NULL | Inclusive. |
| `period_end_date` | TEXT NOT NULL | Inclusive. |
| `broker_statement_line_id` | TEXT NULL | Exact broker identity when supplied. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(margin_lot_id, cost_type, occurrence_key)`. An additional partial unique index enforces `UNIQUE(margin_lot_id, broker_statement_line_id) WHERE broker_statement_line_id IS NOT NULL`.

### 7.2 `margin_cost_observations`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `margin_cost_item_id` | TEXT NOT NULL FK | Logical cost item. |
| `revision_no` | INTEGER NOT NULL | Observation revision. |
| `supersedes_id` | TEXT NULL FK self | Correction chain within the same valuation kind. |
| `reconciles_estimate_id` | TEXT NULL FK self | Optional audit link from a Confirmed observation to the Estimate it was compared with; it does not control aggregation or supersede/delete the estimate. |
| `valuation_kind` | TEXT NOT NULL | `Estimate`, `Confirmed`. Correction is represented by revision/supersedes. |
| `direction` | TEXT NOT NULL | `Charge`, `Credit`. |
| `amount_status` | TEXT NOT NULL | `KnownAmount`, `KnownZero`, `NotOccurred`, `Unpublished`, `FetchFailed`, `Unknown`, `NotApplicable`. |
| `quantity` | TEXT NULL | Cost basis quantity in its effective unit. |
| `rate` | TEXT NULL | Source/contract rate. |
| `rate_unit` | TEXT NULL | Per-share/day, annual percentage, already-days-included, etc. |
| `included_days` | INTEGER NULL | Explicit source/calculation days. |
| `day_count_convention` | TEXT NULL | Contract convention. |
| `amount` | TEXT NULL | Total amount for known amount/zero. |
| `currency` | TEXT NULL | Required with amount. |
| `formula_version` | TEXT NULL | Estimate formula version. |
| `margin_lot_contract_revision_id` | TEXT NULL FK | Exact contract terms used for an estimate/interpretation. |
| `published_margin_cost_revision_id` | TEXT NULL FK | Exact public cost fact used for an estimate. |
| `source_kind` | TEXT NOT NULL | `ApplicationEstimate`, `PublishedMarketData`, `BrokerStatement`, `UserEntry`. |
| `source_artifact_id` | TEXT NULL FK | Evidence. |
| `source_published_at_utc` | TEXT NULL | Publication time. |
| `available_at_utc` | TEXT NULL | Availability time. |
| `observed_at_utc` | TEXT NOT NULL | Observation time. |
| `booked_at_utc` | TEXT NULL | Broker posting time. |
| `content_sha256` | TEXT NOT NULL | Normalized observation hash. |
| `recorded_at_utc` | TEXT NOT NULL | Insert time. |

Unique: `(margin_cost_item_id, valuation_kind, revision_no)` and filtered unique `supersedes_id WHERE supersedes_id IS NOT NULL`. `KnownAmount` requires non-zero amount and currency; `KnownZero` requires canonical amount `"0"` and currency; all other missing/non-applicable states require amount and currency null. When present, `reconciles_estimate_id` must reference an unsuperseded Estimate of the same cost item.

Confirmed and Estimate aggregates are calculated separately. For the combined reference total, an effective Confirmed leaf with `KnownAmount`, `KnownZero`, `NotOccurred`, or `NotApplicable` resolves the entire cost item: that Confirmed leaf is selected and every effective Estimate for the item is excluded, regardless of `reconciles_estimate_id`. If the Confirmed leaf is `Unpublished`, `FetchFailed`, or `Unknown`, it does not suppress the effective Estimate; the unresolved Confirmed state remains visible. Estimates are never counted in the confirmed aggregate and all historical observations remain auditable.

### 7.3 `margin_cost_amount_components`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `margin_cost_observation_id` | TEXT NOT NULL FK | Observation. |
| `component_type` | TEXT NOT NULL | `Gross`, `TaxEquivalent`, `Net`, `BrokerBooked`, `Other`. |
| `direction` | TEXT NOT NULL | `Charge`, `Credit`. |
| `amount_status` | TEXT NOT NULL | Same known/missing states as observation. |
| `amount` | TEXT NULL | Canonical decimal. |
| `currency` | TEXT NULL | Required with amount. |
| `ordinal` | INTEGER NOT NULL | Stable order. |

Unique: `(margin_cost_observation_id, component_type)` and `(margin_cost_observation_id, ordinal)`.

## 8. AI queue and results

### 8.1 `prompt_template_snapshots`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `template_version` | TEXT NOT NULL | Stable version. |
| `template_text` | TEXT NOT NULL | Secret-free prompt template. |
| `template_sha256` | TEXT NOT NULL UNIQUE | Normalized template hash. |
| `created_at_utc` | TEXT NOT NULL | Insert time. |

### 8.2 `ai_profile_snapshots`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `profile_name` | TEXT NOT NULL | Display name. |
| `executable_identity` | TEXT NOT NULL | Executable name/path identity without secrets. |
| `requested_model` | TEXT NULL | Null delegates to CLI default. |
| `timeout_seconds` | INTEGER NOT NULL | Positive. |
| `arguments_json` | TEXT NOT NULL | Sanitized, non-secret argument array. |
| `configuration_json` | TEXT NOT NULL | Normalized execution profile. |
| `profile_sha256` | TEXT NOT NULL UNIQUE | Profile hash used for deduplication. |
| `created_at_utc` | TEXT NOT NULL | Snapshot time. |

### 8.3 `ai_check_jobs`

One logical request for a candidate/input/profile. Explicit retries and rechecks create attempts under this job; duplicate active work is prohibited.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `candidate_result_id` | TEXT NOT NULL FK | Entry candidate only in the initial release. |
| `request_origin` | TEXT NOT NULL | `User`, `Automatic`. |
| `priority` | INTEGER NOT NULL | Lower value runs first; user jobs receive higher priority by policy. |
| `candidate_side` | TEXT NOT NULL | Frozen `Long`/`Short`. |
| `evaluation_bar_date` | TEXT NOT NULL | Frozen candidate date. |
| `input_snapshot_json` | TEXT NOT NULL | Full normalized, secret-free AI input. |
| `input_sha256` | TEXT NOT NULL | Input hash. |
| `technical_manifest_sha256` | TEXT NOT NULL | Candidate manifest hash. |
| `strategy_snapshot_sha256` | TEXT NOT NULL | Strategy hash. |
| `prompt_template_snapshot_id` | TEXT NOT NULL FK | Frozen prompt. |
| `ai_profile_snapshot_id` | TEXT NOT NULL FK | Frozen profile. |
| `automatic_selection_rank` | INTEGER NULL | Required for automatic jobs; per-direction deterministic rank. |
| `selection_policy_version` | TEXT NULL | Required for automatic jobs; includes score/tie-break policy. |
| `automatic_configuration_json` | TEXT NULL | Frozen auto-check enablement/top-N configuration. |
| `automatic_configuration_sha256` | TEXT NULL | Required with automatic configuration. |
| `requested_at_utc` | TEXT NOT NULL | Request time. |

Unique: `(candidate_result_id, input_sha256, ai_profile_snapshot_id, prompt_template_snapshot_id)`. A deliberate recheck of unchanged input is a new attempt, not a duplicate job.

Checks require the automatic rank/policy/configuration/hash only for `Automatic` jobs and require them all to be null for `User` jobs. Automatic selection records the per-direction rank, configured top-N, score-descending/code-ascending tie-break rule, and selection algorithm version.

### 8.4 `ai_job_request_events`

Append-only user/automatic requests and priority promotion. If a user selects a candidate whose automatic attempt is still queued, the job is not duplicated; a `PriorityPromotion` event records the user request and queue ordering uses the promoted priority.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `ai_check_job_id` | TEXT NOT NULL FK | Job. |
| `event_kind` | TEXT NOT NULL | `InitialRequest`, `PriorityPromotion`, `RetryRequest`, `RecheckRequest`. |
| `request_origin` | TEXT NOT NULL | `User`, `Automatic`. |
| `requested_priority` | INTEGER NOT NULL | Priority requested by this event. |
| `requested_at_utc` | TEXT NOT NULL | Request instant. |
| `ordinal` | INTEGER NOT NULL | Monotonic within job. |

Unique: `(ai_check_job_id, ordinal)`.

### 8.5 `ai_attempts`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `ai_check_job_id` | TEXT NOT NULL FK | Job. |
| `attempt_no` | INTEGER NOT NULL | Starts at 1. |
| `attempt_kind` | TEXT NOT NULL | `Initial`, `Retry`, `Recheck`. |
| `request_origin` | TEXT NOT NULL | `User`, `Automatic`; frozen per attempt. |
| `requested_at_utc` | TEXT NOT NULL | Exact request event time that caused this attempt. |
| `priority_at_queue` | INTEGER NOT NULL | Effective priority when queued. |
| `status` | TEXT NOT NULL | Cached operational state: `Queued`, `Running`, `Succeeded`, `Failed`, `TimedOut`, `InsufficientInformation`, `Cancelled`. |
| `queued_at_utc` | TEXT NOT NULL | Queue time. |
| `started_at_utc` | TEXT NULL | Start time. |
| `completed_at_utc` | TEXT NULL | Terminal time. |
| `cli_version` | TEXT NULL | Observed CLI version. |
| `actual_model` | TEXT NULL | Observed model when available. |
| `timeout_seconds` | INTEGER NOT NULL | Frozen effective timeout. |
| `arguments_json` | TEXT NOT NULL | Effective sanitized arguments. |
| `exit_code` | INTEGER NULL | Process exit code. |
| `error_kind` | TEXT NULL | `CliFailure`, `Timeout`, `Cancelled`, `Interrupted`, `InvalidResponse`, `ParseFailure`, `Unknown`. |
| `error_message` | TEXT NULL | Sanitized bounded summary. |
| `sanitized_stderr` | TEXT NULL | Sanitized bounded stderr. |
| `raw_response_sha256` | TEXT NULL | Raw stdout hash. |

Unique: `(ai_check_job_id, attempt_no)`. At most one `Queued` or `Running` attempt per job is enforced by `CREATE UNIQUE INDEX ... ON ai_attempts(ai_check_job_id) WHERE status IN ('Queued', 'Running')`. Terminal attempts are immutable.

### 8.6 `ai_attempt_events`

Append-only audit of queue transitions; `ai_attempts.status` is a rebuildable operational projection.

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `ai_attempt_id` | TEXT NOT NULL FK | Attempt. |
| `from_status` | TEXT NULL | Null for initial `Queued`. |
| `to_status` | TEXT NOT NULL | Valid AI attempt state. |
| `occurred_at_utc` | TEXT NOT NULL | Transition instant. |
| `reason` | TEXT NULL | Cancellation/interruption/failure context. |
| `ordinal` | INTEGER NOT NULL | Starts at 1. |

Unique: `(ai_attempt_id, ordinal)`. The application validates the transition graph.

### 8.7 `ai_results`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `ai_attempt_id` | TEXT NOT NULL FK UNIQUE | Successful or insufficient-information attempt. |
| `schema_version` | TEXT NOT NULL | Structured result schema version. |
| `parser_version` | TEXT NOT NULL | Parser version. |
| `verdict` | TEXT NULL | `Bullish`, `Neutral`, `Bearish`; null for insufficient information. |
| `confidence` | TEXT NULL | `High`, `Medium`, `Low`; null when unavailable. |
| `summary` | TEXT NULL | Plain text. |
| `technical_view` | TEXT NULL | Plain text. |
| `fundamental_view` | TEXT NULL | Plain text. |
| `positive_factors_json` | TEXT NOT NULL | Normalized array of strings/typed items for this schema version. |
| `risk_factors_json` | TEXT NOT NULL | Normalized array. |
| `invalidation_conditions_json` | TEXT NOT NULL | Normalized array. |
| `checked_at_utc` | TEXT NOT NULL | AI check instant, distinct from source publication. |
| `structured_result_json` | TEXT NOT NULL | Full parsed result retained for forward compatibility. |
| `structured_result_sha256` | TEXT NOT NULL | Result hash. |
| `created_at_utc` | TEXT NOT NULL | Persistence time. |

`InsufficientInformation` is not converted to `Neutral`. `Stale` is derived at query time when a newer candidate analysis exists; a successful result is never rewritten to stale.

### 8.8 `ai_result_sources`

| Column | Type | Constraints/notes |
|---|---|---|
| `id` | TEXT PK | UUID. |
| `ai_result_id` | TEXT NOT NULL FK | Result. |
| `url` | TEXT NOT NULL | Source URL. |
| `title` | TEXT NULL | Source title. |
| `published_at_utc` | TEXT NULL | Source publication instant. |
| `retrieved_at_utc` | TEXT NOT NULL | AI/tool retrieval instant. |
| `ordinal` | INTEGER NOT NULL | Stable citation order. |

Unique: `(ai_result_id, ordinal)`. Duplicate URLs may be retained only if they represent distinct published versions; application validation records the distinction.

## 9. Mutability and state machines

The immutable-table trigger set is exactly:

```text
instruments
instrument_identifiers
instrument_identifier_revisions
instrument_master_revisions
market_calendar_versions
market_calendar_days
source_artifacts
data_update_items
data_update_failures
daily_prices
daily_price_revisions
price_history_assessments
price_revision_sets
price_revision_set_changes
corporate_actions
corporate_action_revisions
margin_eligibility_records
margin_eligibility_revisions
published_margin_costs
published_margin_cost_revisions
fundamental_records
fundamental_revisions
strategy_parameter_snapshots
analysis_input_manifests
analysis_action_applications
technical_analysis_results
indicator_results
candidate_results
candidate_score_components
positions
position_state_revisions
trade_executions
trade_execution_revisions
margin_lots
margin_lot_contract_revisions
lot_allocation_revisions
position_adjustments
risk_basis_snapshots
risk_plan_revisions
position_evaluation_input_manifests
position_evaluations
margin_cost_items
margin_cost_observations
margin_cost_amount_components
prompt_template_snapshots
ai_profile_snapshots
ai_check_jobs
ai_job_request_events
ai_attempt_events
ai_results
ai_result_sources
```

The business migration creates one `BEFORE UPDATE` and one `BEFORE DELETE` abort trigger for every immutable table named above. Trigger names follow `trg_<table>_immutable_update` / `trg_<table>_immutable_delete`. These triggers are handwritten migration SQL and are outside the EF model snapshot. Any future migration that rebuilds an affected SQLite table must explicitly drop its triggers before the rebuild, recreate them afterward, and verify all expected trigger names through `sqlite_master`; the migration is incomplete without that verification.

Only these operational projections permit controlled updates:

- `data_update_runs`: `status`, `started_at_utc`, `completed_at_utc`, success/failure/unchanged counts, and `summary` until terminal.
- `analysis_runs`: `status`, `started_at_utc`, `completed_at_utc`, total/success/failure counts, and `summary` until terminal.
- `ai_attempts`: `status`, start/completion timestamps, observed CLI/model, exit/error diagnostics, sanitized stderr, and raw response hash until terminal. Identity, job, request origin/time/priority, timeout, and arguments are never updated.

Each operational table has `BEFORE UPDATE` triggers that reject changes outside its allowlist and any update after a terminal status; delete remains prohibited. Every AI status change appends `ai_attempt_events` and updates the attempt projection in the same transaction. Invalid backward transitions are rejected. A crashed `Running` attempt appends `Failed` with `error_kind = Interrupted`; queued attempts remain resumable.

Deletes are not part of normal business operations. Voids, cancellations, corrections, superseding revisions, and archive states preserve history. Foreign keys use `RESTRICT`; no analysis or AI cascade can delete a user record.

## 10. Cross-row invariants

SQLite `CHECK` constraints enforce row-local rules. The following set-level rules must be checked in one Application/Repository transaction and covered by tests:

1. A superseding row has the same logical parent, revision number increments by one, and does not form a cycle.
2. A price revision set contains one effective revision per logical bar, its parent/change chain is acyclic, and reconstruction yields its final ordered set hash. Its parent set has the same instrument/provider; every added/replaced/removed daily-price revision belongs to that instrument/provider; and the change `bar_date` matches the logical bar. Replace/Remove must name the parent's effective member at that date. An analysis manifest references a set for the same instrument/provider and matching hash.
3. An analysis input selects only confirmed bars through `evaluation_bar_date`, excludes revisions recorded after the run cutoff, and observes the configured information-time cutoff.
4. An applied corporate action satisfies both `effective_date <= evaluation_bar_date` and the selected availability rule. Dividend adjustment references the exact pre-ex-date close revision used for its factor.
5. Reconstructing an analysis manifest yields its stored price/action/manifest hashes. Run, manifest, technical-result, and redundant instrument IDs must belong to the same graph.
6. A `candidate_result` can reference only an initial-release `Entry` technical result with outcome `Candidate`; instrument-scoped technical `Exit` rows are not created.
7. `trade_executions` can be created only by the manual registration use case after explicit confirmation; source prices, signals, deadlines, costs, and AI cannot invoke that repository method.
8. Opening executions and closing executions have positive values. A MarginLot references an Open logical execution and exact effective opening revision from that execution; lot and execution belong to the same position.
9. Unsuperseded `Effective` lot allocations exactly equal the referenced effective closing revision quantity, and cumulative allocated quantity cannot exceed the lot quantity after chronological corporate-action conversion. Allocation, lot, closing execution, and position IDs must match their graph.
10. Correcting/voiding an execution or allocation that already affects a lot places the position into reconciliation until all dependent allocations, contract interpretations, risk bases/plans, costs, and projections have append-only replacement records referencing exact new revisions.
11. A corrected corporate-action revision atomically reverses the old adjustment leaf and adds a replacing adjustment; a projection cannot apply both old and corrected factors.
12. Split/consolidation conversion changes quantity, basis price, fixed ATR, stop, and take-profit together when a risk basis exists. The adjustment's before/after stop and take-profit evidence must equal the predecessor/new `CorporateActionConversion` risk-plan revisions appended in the same transaction; only the risk-plan leaf drives the current projection. Unsupported actions stop re-evaluation with `ReconciliationRequired`.
13. A partial-exit breakeven risk plan requires a user-confirmed partial closing execution and exact effective allocation revision. It never loosens the prior stop in an adverse direction.
14. A fixed margin term has a confirmed repayment deadline; unknown/no-fixed-term contracts do not contain a fabricated deadline.
15. Margin type does not change during a lot except through an explicit input-correction workflow with reconciliation.
16. A position-evaluation manifest's exact execution/allocation/adjustment/contract/risk/cost IDs all belong to its position and cutoff; its current price belongs to the referenced analysis price set. Exit decisions are position-scoped and remain independent when multiple positions share an instrument.
17. Estimate and confirmed margin costs are aggregated separately. A resolving effective Confirmed leaf selects the entire item for the combined reference total and excludes every Estimate of that item with or without a reconciliation link; an unresolved Confirmed leaf preserves an available Estimate as unresolved reference data.
18. Missing, unpublished, fetch-failed, not-occurred, and known-zero amounts remain distinct.
19. At most one AI attempt per job is active. Retries/rechecks append attempts and never overwrite a terminal attempt/result; user priority promotion appends a request event.

## 11. Current-state projections

The initial migration creates tables and indexes, not mutable materialized summary tables. Repository queries provide these projections:

- latest master/margin/price/action leaf as of an information and transaction cutoff;
- exact price revision sets reconstructed from immutable checkpoint/delta chains;
- current execution and lot-allocation leaves;
- lot quantities in a specified corporate-action unit;
- current position quantity/average basis and reconciliation status;
- current risk-plan leaf per lot as the sole source of projected stop/take-profit values; adjustment `before_*`/`after_*` fields are audit evidence only;
- earliest confirmed lot maturity and remaining trading days using a recorded calendar version;
- confirmed cost total, unresolved estimate total, and unknown/missing cost counts;
- latest candidate and AI result, with `Stale` derived from newer analysis runs.

If performance later requires cached projection tables, they must be rebuildable, carry a projection version/source hash, and never become the audit source of truth.

## 12. Migration and verification order

The empty `InitialCreate` migration remains unchanged. Implementation adds a new `AddBusinessSchema` migration and then verifies at minimum:

1. Migration applies to an empty SQLite database and after `InitialCreate`.
2. `IMigrator` can stop after `InitialCreate` and then advance to `AddBusinessSchema`; `PRAGMA foreign_keys = ON`, `foreign_key_check`, and `integrity_check` succeed.
3. `sqlite_schema` contains all expected tables, snake_case FK indexes, partial unique indexes, CHECK SQL, immutable triggers, and operational allowlist/terminal triggers.
4. Partial unique indexes are exercised with actual inserts, including nullable branches and branch attempts in `trade_execution_revisions`, `lot_allocation_revisions`, `position_adjustments`, `risk_basis_snapshots`, and `margin_cost_observations`. All immutable-table `UPDATE`/`DELETE` operations fail; valid operational transitions succeed and terminal updates fail.
5. Fixed-width UTC instants support translated SQL cutoff/order queries, and decimal/UUID/date/hash values round-trip through their explicit converters in exact canonical form.
6. Identical data re-fetches create `data_update_items` with `Unchanged` but no new business revision; corrections append a revision.
7. Price checkpoint/delta reconstruction yields the exact prior analysis set after later price/action corrections, including equal-time concurrent ingestion cases.
8. A corporate-action ratio correction reverses the old adjustment and applies only the replacement factor.
9. Trade revision, lot allocation, corporate-action conversion, risk-basis correction, and partial close preserve the original execution and exact dependency revisions; adjustment before/after price lines exactly match the atomic predecessor/new risk-plan revisions.
10. A saved position evaluation reconstructs the same exact position/cost input manifest after later corrections, and two positions in one instrument can produce independent Exit decisions without instrument-level Exit technical rows.
11. Cost known-zero/missing states, source revision links, occurrence deduplication, item-level Estimate/Confirmed selection (including a null `reconciles_estimate_id`), and unresolved Confirmed fallback do not double count.
12. AI active-job deduplication, automatic-selection evidence, user priority promotion, retry history, interrupted recovery, and stale derivation behave as specified.
13. FK `RESTRICT` rejects audit-parent deletion, and no path from signal/price/AI/cost/deadline automatically creates a trade execution.
14. `dotnet ef migrations has-pending-model-changes` reports no pending model change; after every later migration, all handwritten triggers still exist.

The application may implement these groups in more than one additive migration, but it must not weaken the logical contracts in this document or rewrite an applied migration.
