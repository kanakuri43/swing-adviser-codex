# AI Analysis

AGENT.md の要約から分割した、AI チェック(Codex CLI)まわりの詳細。
Non-negotiable rules・優先順位は AGENT.md 側が正であり、本ファイルの内容と矛盾する場合は AGENT.md を優先する。

## AI check
テクニカル分析で抽出した任意銘柄に対し、Codex CLI で追加調査を行う。
AI はチャート確認だけに限定せず、可能な範囲で以下を確認する。

- テクニカル状況
- 直近ニュース
- 決算・業績・業績予想
- バリュエーション
- 財務状態
- 配当/株主還元
- セクター/マクロ環境
- 材料/イベント
- 流動性、急変理由
- テクニカルとファンダメンタルの矛盾

AI 結果は可能な限り構造化する。
schema(仮確定): `Verdict`, `Confidence`, `Summary`, `TechnicalView`, `FundamentalView`, `PositiveFactors`, `RiskFactors`, `InvalidationConditions`, `CheckedAt`, `Sources/Citations`。

- `Verdict`: `Bullish` / `Neutral` / `Bearish`（相場見通しの表現。BUY/SELL 推奨ではないことを UI 上も明示する）。
- `Confidence`: `High` / `Medium` / `Low` のラベル。
- 上記以外のフィールドの詳細な型は今後詰める。

AI 失敗/timeout/情報不足は別状態として扱い、テクニカル結果まで無効にしない。
AI 判定を自動売買トリガーとして使わない。

ファンダメンタル情報（PER/PBR等の構造化データ）の取得元は [`data-sources.md`](./data-sources.md) を参照。決算詳細・ニュース・業績予想等の非構造化情報は Codex CLI(AI)自身の調査に委ねる。

## Codex CLI integration
実行設定をハードコードしない。設定候補: executable path、working directory、model、timeout、additional arguments。

実行時:
- stdout/stderr と exit code を取得
- timeout/cancellation を考慮
- UI thread をブロックしない
- shell injection を避ける
- 診断可能なエラーを残す
- 不要な個人情報/秘密情報をプロンプトへ含めない

AI へ渡した入力条件または識別情報を追跡できる設計を優先する。

デフォルト設定(仮決定): 実行ファイルは PATH 上の `codex`、model はアプリ側で固定せず Codex CLI のデフォルトに委ねる、timeout は 120秒(仮値)。AI チェックの並列実行数はデフォルト 2〜3(仮値)。いずれも設定画面から上書き可能にし、コード中にハードコードしない。
