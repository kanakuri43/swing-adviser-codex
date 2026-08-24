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

## Execution policy
AIチェックはテクニカル結果を確定保存した後の非同期処理とし、日次更新のコア完了をブロックしない。初期対象は当日の Long/Short Entry 候補とし、保有ポジション向けのAI判定はプロンプトと意味が異なるため初期対象外とする。

既定では利用者が候補一覧から単件または複数件を明示選択して実行する。設定で自動実行を有効化した場合だけ、Long/Short各方向のスコア上位3件を「スコア降順、同点は銘柄コード昇順」で決定的に選ぶ。自動実行の有効/無効と上位件数は設定化し、候補スコアは勝率ではなくキュー選定順位にのみ使う。

永続化する試行状態は `Queued -> Running -> Succeeded | Failed | TimedOut | InsufficientInformation | Cancelled` とする。キュー中のキャンセルも許可する。再試行では終端状態を書き換えず新しい attempt を作る。同一candidate result・正規化入力hash・AI profileの重複投入を防ぐ。新しいanalysis run後の旧結果は削除せず、表示上 `Stale` とする。情報不足を `Neutral` に変換しない。

1件の失敗で他ジョブを停止せず、初期版では暗黙の自動再試行を行わない。利用者要求を自動要求より優先して待ち行列へ入れるが、実行中ジョブは中断しない。アプリ終了時に実行中だった試行は `Failed(Interrupted)` とし、待機中ジョブは次回再開可能にする。

試行ごとに起動元、analysis/candidate ID、候補方向、`EvaluationBarDate`、正規化した入力snapshot/hash、テクニカル入力manifest/hash、戦略snapshot hash、prompt template version/hash、要求/開始/完了日時、CLI/version/model（取得可能な場合）、timeout、秘密を除いた引数、exit code、error kind、sanitized stderr、raw response hash、構造化結果、source URL/title/published/retrieved日時を保存する。再チェック結果は元結果と分離し、後日の情報を過去分析時点の情報だったように表示しない。

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

デフォルト設定(仮決定): 実行ファイルは PATH 上の `codex`、model はアプリ側で固定せず Codex CLI のデフォルトに委ねる、timeout は 120秒(仮値)。AI チェックはグローバルな永続キューで最大2並列とする。いずれも設定画面から上書き可能にし、コード中にハードコードしない。
