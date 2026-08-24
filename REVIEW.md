# Review

[`TODO.md`](TODO.md) でチェック済みの範囲（1章の基盤構築、2章の設計判断、3章の DB 論理設計、7章の AI Verdict 表示）に対するレビュー指摘。

検証時点の状態:

- `dotnet build` 成功（警告 0 / エラー 0）
- `dotnet test` 成功（1件）
- 現在の実装に Non-negotiable rules 違反は見当たらない

## 重要度：高

### 1. SQLite の接続設定が2箇所に重複していて、3箇所目で事故る

`MigrationsHistoryTable("__ef_migrations_history")` が [`SwingAdviserDbContextFactory.cs`](src/SwingAdviser.Infrastructure/Persistence/SwingAdviserDbContextFactory.cs) 13行目とテスト [`SwingAdviserDbContextTests.cs`](tests/SwingAdviser.Infrastructure.Tests/Persistence/SwingAdviserDbContextTests.cs) 18行目に別々に書かれている。実行時登録（[`App.xaml.cs`](src/SwingAdviser.Presentation/App.xaml.cs) の `RegisterTypes` は現在空）で3箇所目を書くとき、これを書き忘れると EF は既定の `__EFMigrationsHistory` を見にいき、**既存 DB に対して全マイグレーションを再適用しようとする**。[`AGENT.md`](AGENT.md) SQLite 節「既存データを安易に破壊しない」に直結する事故。

**対処**: `UseSwingAdviserSqlite(...)` のような拡張メソッド1本に集約し、実運用パス（`Path.Combine(AppContext.BaseDirectory, "swing-adviser.db")`）・WAL・`busy_timeout` もそこで一元化する。[`docs/database-schema.md`](docs/database-schema.md) §2.0 で決めた「書き込み不可なら暗黙フォールバックせずエラー」も同じ1箇所に置ける。

### 2. `supersedes_id` の分岐防止インデックスが、ユーザー側4テーブルで欠落

[`docs/database-schema.md`](docs/database-schema.md) §2.3 は「Common revision columns を使うテーブルは `supersedes_id` に filtered unique index を張る」と定めている。しかし列を明示列挙しているテーブルのうち、この記述を再掲しているのは `trade_execution_revisions`（§6.4）だけ。

- `lot_allocation_revisions`（§6.7）
- `position_adjustments`（§6.8）
- `risk_basis_snapshots`（§6.9）
- `margin_cost_observations`（§7.2）

この4つは `UNIQUE(親キー, revision_no)` しかなく、同一 revision を2行が supersede する分岐チェーンを DB が許す。訂正が絡む「どれが leaf か」の判定が非決定になり、§10.1 の不変条件がアプリ層任せになる。監査性を最優先する設計方針からすると DB 制約で塞ぐべき。

### 3. 信用コストの二重計上パス（`reconciles_estimate_id` が null の Confirmed）

[`docs/database-schema.md`](docs/database-schema.md) §7.2 の規則は「Confirmed が存在すると、**それが reconcile した** Estimate を合算から除外する」。証券会社明細から直接 Confirmed を登録し `reconciles_estimate_id` を null にしたケースでは、同一 `margin_cost_item` に残っている見積が除外されず、ネット参考損益に見積＋確定が両方乗る。§10.17 も「reconciled estimate」としか書いていないので同じ穴。

**対処**: 規則を「item 単位で有効な Confirmed が1件でもあれば、その item の Estimate は合算対象外」に変えるか、Confirmed 登録時に `reconciles_estimate_id` を必須にするかのどちらか。逆日歩は数日で利益を消す規模という TODO 2-3 の前提を考えると、ここの誤差は無視できない。

### 4. `indicator_results.values_json` の中身が未定義で、容量見積もりが立たない

[`docs/database-schema.md`](docs/database-schema.md) §5.6 は "Full unrounded decimal values and states" とだけ書いており、**最新バーの値だけなのか全系列なのか**が読み取れない。これで DB 増分が1日あたり数MBか数GBかが変わる。

概算:

| テーブル | 行数/日 | 備考 |
|---|---|---|
| `indicator_results` | 約 55,000 | 3,900銘柄 × 2方向 × 指標7種 |
| `technical_analysis_results` | 約 7,800 | NotCandidate も保存する仕様 |
| `price_revision_sets` | 約 3,900 | |
| `analysis_input_manifests` | 約 3,900 | |

デスクトップ SQLite 単一ファイルで、かつ**削除を禁止**（§9 "Deletes are not part of normal business operations"）という設計なので、保持ポリシーか容量見積もりのどちらかを本書に足さないと運用1年目で行き詰まる。`values_json` を最新値のみに限定し、系列が必要なら再計算する、という線引きが現実的。

## 重要度：中

### 5. EMA200 の「上場来固定起点」決定が、未検証項目に依存している

TODO 2-2 は完了扱いだが、採用したルール（[`docs/technical-analysis.md`](docs/technical-analysis.md) EMA calculation contract 節「上場来履歴を固定起点」「履歴の完全性を確認できない場合は計算しない」）は、TODO 5「上場来の初期取得…取得元の履歴完全性を検証」が未着手のまま成立している。Yahoo 非公式 API が上場来を返さない銘柄では `HistoryIncomplete` → スキャン対象外となり、**最悪ほぼ全銘柄が候補ゼロ**というフェイル方式。

**対処**: 決定自体は再現性の観点で正しいので変える必要はない。TODO 2-2 の項目に「TODO 5 の検証結果次第で代替起点ルール（固定起点日の明示指定等）を再検討」と依存を明記する。

**対応判断（2026-08-24）**: 上場来固定起点と `HistoryIncomplete` のフェイルクローズ自体は、保存済み判定の再現性を守るため変更しない。依存先は正確には TODO セクション4の上場来初期取得検証とセクション5の Yahoo Finance API 実地確認であるため、TODO 2-2 に両方への検証依存を追記した。検証前に任意の固定起点日へ変更すると元の再現性問題を戻すため、代替ルールは除外率の実測後に判断する。

### 6. Presentation → Infrastructure 直参照に対する保護がない

[`SwingAdviser.Presentation.csproj`](src/SwingAdviser.Presentation/SwingAdviser.Presentation.csproj) が Infrastructure を直接参照している。composition root としては妥当だが、[`AGENT.md`](AGENT.md) Architecture 節「ViewModel から DB/HTTP/CLI を直接操作しない」を守らせる仕組みが**コンパイル時にもテストにも存在しない**。同様に「Domain は WPF/Prism/SQLite/HTTP を参照しない」も、現状は「まだ何も書いていないから守られている」だけ。

**対処**: アーキテクチャテスト（NetArchTest 等）を1本入れるか、composition 用プロジェクトを切るかを、Domain 実装に入る前に決める。

### 7. ユーザー側リビジョンに外部データ用の列が付いている

`position_state_revisions`（§6.2）、`margin_lot_contract_revisions`（§6.6）、`risk_plan_revisions`（§6.10）は「Common revision columns」を採用しているため、`available_at_utc` / `availability_status`（`Known`/`Estimated`/`Unknown`）/ `first_observed_at_utc` を持つ。しかし中身は利用者・証券会社確認値であって「ソースからいつ利用可能になったか」という概念がない。`margin_lot_contract_revisions` に至っては `confirmed_at_utc` と `first_observed_at_utc` が並存する。

`trade_execution_revisions`（§6.4）が列を明示列挙してこれらを外しているのと不揃い。ユーザー側リビジョンは同じ明示列挙の契約に揃えないと、実装時に `availability_status` へ何を入れるかを各所で場当たりに決めることになる。

### 8. 企業アクション換算後の stop 価格が2箇所に保存される

`position_adjustments.after_stop_price` / `after_take_profit_price`（§6.8）と、`risk_plan_revisions`（`plan_reason = CorporateActionConversion`）の `stop_price` / `take_profit_price`（§6.10）が同じ値を持つ。どちらが正かが本書に書かれていない（§10.12 も「一緒に換算せよ」としか言っていない）。

**対処**: 投影が参照するのは `risk_plan_revisions` の leaf のはずなので、`position_adjustments` 側は換算の**証跡**であって投影の入力ではない、と明記するか、いずれかを導出扱いにする。TODO 2-1 で解こうとした単位不一致問題が、保存の重複という形で再発しやすい箇所。

### 9. 銘柄コードからの引き当てにインデックス指定がない

[`docs/database-schema.md`](docs/database-schema.md) §2.4 は「FK 列には必ずインデックス」と定めているが、最も高頻度な検索である `instrument_identifier_revisions(scheme, value)` からの銘柄逆引きは FK ではないため対象外になっている。日次更新で 3,900 銘柄をコード引きするフローなので、`ix_instrument_identifier_revisions_scheme_value` を明示する。

**対応判断（2026-08-24）**: 検索性能の問題意識は採用するが、提案された複合インデックスは作成しない。現スキーマでは `scheme` は親の `instrument_identifiers`、`value` は `instrument_identifier_revisions` にあり、同一テーブルの `(scheme, value)` は存在しないためである。子を値で絞って親PKへ join する実際の検索経路に合わせ、`ix_instrument_identifier_revisions_value_instrument_identifier_id (value, instrument_identifier_id)` をスキーマへ追記した。`scheme` の非正規化は親子不一致の不変条件を増やすため行わない。

### 10. `technical_analysis_results(signal_purpose = Exit)` と `position_evaluations` の関係が未定義

前者のユニークキーは `(analysis_run_id, instrument_id, position_side, signal_purpose)`（§5.5）で**銘柄単位**、後者は `(analysis_run_id, position_id)`（§6.12）で**ポジション単位**。同一銘柄に複数ポジションがある場合（§6.1 で明示的に許可）に Exit 判定をどちらで持つのかが決まっていない。`candidate_results` は「初期リリースは Entry のみ」（§8.3）なので、実質 `signal_purpose = Exit` の行が誰にも使われない可能性がある。

**対処**: 初期リリースでは `technical_analysis_results.signal_purpose` を `Entry` 固定にすると書き切るのが素直。
