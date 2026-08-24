# Risk Management

AGENT.md の要約から分割した、保有ポジションの損切/利確ルール。
指標そのものの定義は [`technical-analysis.md`](./technical-analysis.md) を参照。
Non-negotiable rules・優先順位は AGENT.md 側が正であり、本ファイルの内容と矛盾する場合は AGENT.md を優先する。

## Stop-loss / take-profit rules
損切/利確ルール(仮決定): ボラティリティ適応型（ATR倍数）と固定 R 倍率を併用する。
- 建玉時の基準価格を `entry_basis_price`、固定ATRを `atr_basis`、方向別の損切倍率を `k` とし、Long の損切ラインは `entry_basis_price - k * atr_basis`、Short は `entry_basis_price + k * atr_basis` とする。初期値は Long `k = 3.0`、踏み上げリスクを抑える Short `k = 2.5`（いずれも仮値）。価格、ATR、ラインは必ず同じ通貨・株式単位で扱う。倍率を狭めたことを理由に株数を自動増加させない。
- 一部利確ライン: 損切幅の 1.5R(仮値)到達で、現在数量の50%を一部利確候補として表示する（自動決済はしない）。割合は `partialTakeProfitFraction = 0.50` として設定化する。
- 残りポジションの Exit 判定: 1.5R 到達後、Long は MACD デッドクロス または EMA20 割れ のいずれか成立で Exit 候補、Short は MACD ゴールデンクロス または EMA20 上抜け のいずれか成立で Exit 候補とする。いずれも未成立の間は HOLD。
- 本アプリは自動決済・自動発注を行わない。上記はあくまで判断支援表示であり、実際の決済は利用者が証券会社側で行う。

### Partial take-profit state transition
1.5Rへの価格到達は候補表示だけを発生させ、ポジション数量や損切候補を変更しない。利用者が実際の部分決済について約定日時・価格・数量を明示登録し、残数量が確定した後にだけ、残ポジションの損切候補を現在基準の `entry_basis_price` へ移す。初期損切候補は監査用に残し、新しい stop-plan revision を追記する。

新しい損切候補は Long なら `max(従来候補, entry_basis_price)`、Short なら `min(従来候補, entry_basis_price)` とし、従来より不利な方向へ緩めない。これは「建値候補（コスト未調整）」であり、手数料、金利、貸株料、逆日歩、配当相当額、スリッページを含む損益ゼロを保証しない旨をUIに明示する。

候補数量は `floor(現在数量 * 0.50 / 売買単位) * 売買単位` とし、残りも最低1売買単位を満たす場合だけ提示する。厳密に50%へできない場合は実効割合を併記し、分割不能なら `PartialExitNotFeasible` とする。全決済候補へ暗黙変換しない。

### ATR calculation and reference date
ATR14 は [`data-sources.md`](./data-sources.md) の同一版の point-in-time 調整済み High/Low/Close を使い、Wilder 方式で計算する。

```text
TR[t] = max(High[t] - Low[t], abs(High[t] - Close[t-1]), abs(Low[t] - Close[t-1]))
initial ATR14 = first 14 TR values' SMA
ATR14[t] = (ATR14[t-1] * 13 + TR[t]) / 14
```

先頭日足の TR は `High - Low` とする。C# `decimal` で中間丸めを行わず、表示時だけ丸める。

- 候補に紐づく建玉は、その候補の `EvaluationBarDate` のATRを使う。
- 候補に紐づかない手動建玉は、約定日時より前に確定していた直近取引日のATRを使う。後から登録した場合でも約定当日以後の日足を遡及利用しない。
- 建玉時に `atr_reference_bar_date`、ATR値、期間、方式、入力データ版、倍率、損切幅 `R` を固定する。日次のATR再計算で既存の基準値・ラインを上書きしない。現在ATRを表示する場合は参考情報として固定ATRと区別する。

### Corporate actions while holding
利用者が登録した元約定価格・元約定株数は監査原票として変更しない。分割比率 `r = 新株数 / 旧株数` が保有中に効力発生した場合、企業アクション調整履歴を追加し、現在基準の株数を `r` 倍、取得単価・固定ATR・損切/利確価格を `r` で割る。これらを一組で換算し、単位不一致を作らない。

現金配当は株数・取得単価・固定ATR・価格ラインを変更しない。制度信用の配当金相当額は買建の受取/売建の支払、一般信用は証券会社の契約条件として、企業アクション予測と後日の実額台帳を分離する。権利落ちを警告表示する。端株・現金交付、合併等の未対応イベントは `ReconciliationRequired` とし、照合完了まで当該ポジションの自動再評価を停止する。企業アクション調整から約定履歴を生成してはならない。

## Carrying costs and maturity
ATR損切・利確ラインは価格ベースの説明可能なルールとして維持し、信用取引コストをラインへ混ぜない。買方金利、貸株料、逆日歩、配当金相当額、証券会社固有コストは `MarginCostLedger` で別管理し、次の情報を保持する。

- 対象MarginLot、コスト種別、Charge/Credit、Estimate/Confirmed/Corrected
- 対象期間、数量、金額、通貨、率と単位、日数計算規約
- source、available/observed日時、revision、supersedes

証券会社明細の確定額を正とし、アプリ計算値は見積として明確に区別する。逆日歩の未公表・取得不能、契約料率不明を0円扱いしない。価格損益、確定コスト控除後損益、見積を含むネット参考損益、コスト/R比を分けて表示し、コスト増大を保有見直し理由にできるようにするが、自動決済や約定生成は行わない。

返済期限はMarginLotごとの証券会社確認値を使い、未決済lotの最短期限と残営業日をポジションへ集約表示する。警告閾値の初期値は30/10/5/1営業日前とし設定化する。期限不明、期限変更、期限接近、期限超過を別状態にし、警告だけで決済済みにしない。

保有画面での表示要件（適用戦略・損切候補・利確候補・HOLD 理由など）は [`product-spec.md`](./product-spec.md) の Positions を参照。
