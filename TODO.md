# TODO

AGENT.md / docs で仮決定した内容に基づく、今後の作業リスト。
基盤構築、セクション2の設計判断、セクション3のDB論理設計は完了。次は決定済み設計をDomain/EF Coreモデルと追加マイグレーションへ実装する。

**依存関係の注意**: セクション2（設計上の要修正事項）はデータモデルの根幹に関わるため、セクション3（DB スキーマ設計）より先に決着させる。後から直すと保存済みデータの再構築が必要になる。

## 1. プロジェクト基盤構築
- [x] .sln 作成、Domain / Application / Infrastructure / Presentation の各プロジェクトを分離して作成（[`AGENT.md`](AGENT.md) Architecture 節）
- [x] WPF + Prism + MahApps.Metro の初期セットアップ
- [x] SQLite 3 + EF Core を導入し、DbContext の初期マイグレーションを作成
  - 業務テーブルは未確定のため、`InitialCreate` はマイグレーション経路を検証する空の基盤マイグレーション。最初の業務スキーマはセクション2・3の決定後に新規マイグレーションとして追加する。
- [x] `dotnet restore` / `build` / `test` が通る状態を作る（CI 前提の最低ライン）
- [x] 上記が揃った時点で Serena MCP の onboarding を実行する
  - Serena 1.6.1 の `initial_instructions` / `onboarding` を実行し、必須5メモリとレイヤー別メモリを作成済み。
  - MCP の `list_memories` で登録を読み戻し、`serena memories check` で参照整合性エラーがないことを確認済み。
  - `.sln` と C# プロジェクト構造は作成済みで、language serverによるシンボル探索・参照検索が正常に動作することを確認済み。
  - AGENT.md/docs の仕様理解は CLAUDE.md 経由で既に読めているため、その目的での onboarding は不要。
  - 以後、Domain/Application/Infrastructure の構成など大きな構造変更があった場合は re-onboarding（メモリ更新）を検討する。

## 2. 設計上の要修正事項（実装前に決着させる）

### 2-1. adjclose を分析基準にした決定の見直し【最優先】
従来の「テクニカル分析は原則 adjclose を基準に行う」方針は以下と衝突していたため、案A（版管理した生OHLCV＋企業アクションから point-in-time 系列を生成）へ変更した。

- [x] **Look-ahead bias 規則との矛盾を解消する**
  - adjclose は「その日より後に発生した分割・配当」で遡及調整された値であり、[`technical-analysis.md`](docs/technical-analysis.md) Look-ahead bias 節および AGENT.md Non-negotiable rules に定義上抵触する。
  - 日次運用のみなら実害は小さいが、セクション4のパラメータ検証はバックテスト前提のため必ず顕在化する。
- [x] **判定理由の保存・再現性を担保する**
  - 分割・配当のたびに過去の adjclose が再スケールされるため、保存済みの判定値と再計算値が一致しなくなる。AGENT.md SQLite 節の監査性方針と噛み合わない。
- [x] **損切ラインの単位不一致を解消する**
  - [`risk-management.md`](docs/risk-management.md) の損切は「エントリー価格（利用者入力＝未調整の生値）から ATR×3（adjclose ベース＝調整済み）」となっており単位が食い違う。
  - 保有中に分割が起きると ATR が分割比率で縮む一方、記録済み約定価格は分割前のままとなり損切ラインが破綻する。
- [x] **保有中の企業アクションに対するポジション調整仕様を追加する**
- [x] 対処方針を決めてドキュメントへ反映する
  - 案A: 生 OHLCV ＋ 企業アクション（分割比率・配当）を別テーブルで保持し、「分析日時点で判明しているイベントのみ」を適用して調整値を算出する
  - 案B: 指標計算は未調整 close で行い、分割を不連続イベントとして明示検出する（[`data-sources.md`](docs/data-sources.md) の「企業アクションによる不連続のサイン誤検出防止」が既に想定している方向）

### 2-2. EMA200 の再現性確保【最優先】
- [x] **EMA のシード方法（SMA シード / 単純初値）と計算開始日の固定ルールを仕様化する**
  - EMA は IIR フィルタのためシードの影響が残る。EMA200（α≈0.00995）を約490営業日（2年）で計算するとシード寄与が約5%残存する。
  - [`data-sources.md`](docs/data-sources.md) の「直近2年＋差分更新」方針では運用継続で履歴が伸び、同じ過去日の EMA200 値が変化する。閾値付近の銘柄で判定が事後的に反転し、2-1と同じく再現性が失われる。
  - MACD の EMA12/26 は減衰が速いため対象外。EMA200 固有の問題。
  - **検証依存**: セクション4「上場来の初期取得」の検証およびセクション5の Yahoo Finance API 実地確認で運用可能性を確認する。`HistoryIncomplete` による除外率が許容できない場合は、本格運用前に再現可能な代替起点ルールを再検討する。

### 2-3. ドメインモデルの欠落（DB スキーマ設計の前提）
- [x] **部分決済モデルへの対応**
  - [`risk-management.md`](docs/risk-management.md) は「1.5R で一部利確」だが、[`product-spec.md`](docs/product-spec.md) Positions は単一約定を前提としたフラット構造。
  - 部分決済で株数が時系列変化し決済情報が 1:N になるため、「1ポジション : N約定」への設計変更が必要。
- [x] **信用取引コストをモデルに追加する**
  - 金利・貸株料・**逆日歩**・（Short で権利日をまたぐ場合の）配当相当額がドメインに一切存在しない。
  - 特に逆日歩は数日で利益を消す規模になり得るため、「損切・利確・継続保有の判断支援」の入力として無視できない。保有コスト累計の保持、または逆日歩発生の警告表示を検討する。
- [x] **建玉期限を Positions に追加する**（MarginLot単位。制度信用は最長6ヶ月、確定日は証券会社確認値）
- [x] **Short 側の損切を非対称にするか判断する**
  - エントリー条件は「Short は全条件一致必須」と非対称にした一方、損切は Long/Short とも ATR×3 で対称。踏み上げリスクの非対称性を理由にエントリーを厳格化したのなら、リスク管理側も揃えるべきか検討する。

### 2-4. 未定義で実装時に詰まる箇所
- [x] **「一部利確」の割合を決める**（初期値50%、売買単位へ切り下げ、分割不能は候補なし）
- [x] **一部利確後の残ポジションの損切を建値へ移すか、元のまま維持するかを決める**（利用者が部分決済約定を登録した後だけ建値候補へ移動）
- [x] **ATR の基準日を決める**（約定日 / 分析日。エントリー時に固定するか毎日再計算するか）
  - 15:30 以降に更新 → 翌日寄りで約定、というフロー上、約定日と分析日は必ずズレる。
- [x] **EMA200 を満たさない銘柄（上場200日未満）の扱いを決める**（除外 / 指標の部分適用）
- [x] **AI チェックを日次フローのどこに置くか決める**
  - [`product-spec.md`](docs/product-spec.md) Daily update workflow の1〜8に AI チェックが存在しない。全候補へ自動実行か、ユーザーが選んだ銘柄のみかを明記する（並列数 2〜3 の設定は一括実行を示唆している）。
- [x] **戦略パラメータの正となる置き場所を一本化する**
  - AGENT.md Configuration 節は「設定へ分離」、SQLite 節は「戦略バージョン/パラメータを追跡」となっており二重管理になりうる。
  - パラメータ変更で過去の判定が再現不能になるため、判定結果レコード側にパラメータのスナップショットを凍結保存する設計とする（正は設定ファイル、判定時に DB へ保存）。

## 3. 未確定仕様の解消
- [x] DB の具体的なテーブル定義・列構成を設計する（[`docs/database-schema.md`](docs/database-schema.md)、[`AGENT.md`](AGENT.md) SQLite 節）
  - 価格データ、企業アクション、銘柄マスタ、信用情報、戦略パラメータ、候補/スコア、ポジション、MarginLot/契約条件、信用コスト台帳、約定履歴、AIキュー/結果の各テーブルを、決定済みドキュメントの内容を踏まえて設計する
  - **前提**: セクション2（特に 2-1 の企業アクションテーブル、2-3 の部分決済・信用コスト・建玉期限、2-4 のパラメータスナップショット）を先に決着させること
- [x] 決定済み論理設計をDomainモデル、EF Core設定、追加の `AddBusinessSchema` マイグレーションへ実装する
  - 空の `InitialCreate` は変更せず、[`docs/database-schema.md`](docs/database-schema.md) の順序で追加する
  - revisionの追記制約、snake_case、`DeleteBehavior.Restrict`、point-in-time manifest再構築、手動約定だけを許す境界をRepository/migrationテストで担保する
  - 54業務テーブル、canonical UUID/UTC/date/decimal変換、FK索引、CHECK/filtered unique、append-only/運用状態トリガーを `AddBusinessSchema` へ実装済み。
  - DomainモデルはEF rowから分離し、利用者確認済み約定だけを生成できる境界、revision直系訂正、信用コストの欠損/0/確定優先、AI状態を不変条件として実装済み。
  - migration適用、schema/integrity、FK `RESTRICT`/索引、immutable/terminal更新拒否、revision分岐拒否、point-in-time price set/manifest再構築をテスト済み。
- [ ] **主要画面のUIモックを起動し、デザインを利用者と確認する**
  - **実施タイミング**: Domain/Application の主要ユースケースと画面表示用データ契約が固まり、実DB・外部APIへ接続する前。モックデータだけで候補一覧、保有ポジション、約定履歴、手動約定登録、進捗・エラー・AI状態を一通り遷移できる状態にする。
  - Long/Short、Entry/Exit、参考情報と利用者入力の境界が誤読されないこと、重要情報の優先順位、ウィンドウサイズ変更時のレイアウト、確認操作の分かりやすさを実際に起動して確認する。
  - 確認結果を反映してから実データ接続と画面実装を進め、手戻りを抑える。モックから売買履歴を自動生成したり、実注文へつながる導線は作らない。
- [ ] **Application の主要ユースケースを実装し、UIをローカルSQLiteへ接続する**
  - UIモックで確定した画面契約を使い、候補参照、保有参照、手動約定登録・訂正、進捗・エラー表示を Application 層経由で動かす。ViewModelからDbContextやRepositoryを直接操作しない。
  - 外部APIへはまだ接続せず、再現可能なローカルテストデータを投入できる開発用経路を用意する。実運用DBとテストDBを混同しない。
- [ ] **ローカル結合版を起動し、実際の画面操作で一連の動作を検証する**
  - **実施タイミング**: Domainモデル、`AddBusinessSchema`、Repository、主要Applicationユースケース、UI接続が揃った後。セクション4のバックテストおよびセクション5の外部データ接続より前に実施する。
  - テストデータを使い、起動 → 一覧表示 → 詳細確認 → 利用者入力 → 確認 → SQLite保存 → 再起動後の再表示 → 訂正revision追加までを操作する。
  - Long/Short・Entry/Exitの誤読、入力検証、キャンセル、多重実行防止、進捗、失敗表示を確認し、分析結果や現在値から売買履歴が自動生成されないことも確認する。

## 4. 仮決定パラメータの検証・調整（実装後にバックテスト等で見直す前提）
- [ ] **バックテスト基盤を構築する**（以下の検証タスクすべての前提。[`technical-analysis.md`](docs/technical-analysis.md) Look-ahead bias 節の規則をバックテストにも適用すること）
- [ ] MACD パラメータ 12/26/9 の妥当性検証（[`technical-analysis.md`](docs/technical-analysis.md)）
- [ ] EMA 20/50/200 のトレンド判定ロジックの妥当性検証
- [ ] 出来高倍率フィルタの閾値（1.5倍、仮値）の調整
- [ ] ATR 期間14日、損切倍率 Long 3.0/Short 2.5、50%利確 1.5R、部分決済後の建値移動の妥当性検証（[`risk-management.md`](docs/risk-management.md)）
- [ ] 候補スコアの重み付け（未確定）を決定し、0〜100スコア・信頼度ラベルとの対応を詰める（[`technical-analysis.md`](docs/technical-analysis.md) Candidate score calculation）
- [ ] Long/Short 非対称条件（Short は全条件一致必須）の実データでの妥当性検証
- [ ] 上場来の初期取得について、実行時間・保存容量・取得元の履歴完全性を検証（[`data-sources.md`](docs/data-sources.md)）
- [ ] 日次分析結果について、想定全銘柄数 × Long/Short × 指標数で DB 本体・索引・WAL の行数/bytes per day と1年増分を実測し、バックアップ容量・保持方針を決める（`indicator_results.values_json` に時系列全体を保存しない前提）
- [ ] AI 結果 schema のうち型未確定のフィールド（Summary/TechnicalView/FundamentalView/PositiveFactors/RiskFactors/InvalidationConditions/CheckedAt/Sources）の型を確定する（[`ai-analysis.md`](docs/ai-analysis.md)）
- [ ] Codex CLI のデフォルト timeout（120秒）・並列数2・自動チェック上位件数3/方向の妥当性を実運用で検証

## 5. 外部データ取得の実装検証
- [ ] Yahoo Finance 非公式 chart API の安定性・レート制限の実地確認、失敗時のフォールバック設計
- [ ] JPX 公式の上場銘柄一覧・信用取引銘柄一覧のファイル形式を確認し、パーサーを実装
- [ ] Yahoo Finance 企業情報エンドポイントから取得できる項目（PER/PBR/時価総額等）の実際の可用性を確認
- [ ] **代替データソースの候補をドキュメント化する**
  - 価格・ファンダの両方が Yahoo Finance 非公式 API に依存しており単一障害点。遮断時に機能停止する。J-Quants の無料プラン等をフォールバック候補として記載しておく。
- [ ] **銘柄コードの正規化ルールを決める**（JPX は `7203`、Yahoo は `7203.T`。2024年以降の英字を含むコードへの対応も含む）

## 6. Non-negotiable rules の遵守確認
- [ ] 売買履歴が UI 操作以外（BUY/SELLサイン・現在値・AI判定）から自動生成されないことをテストで担保（[`AGENT.md`](AGENT.md) Non-negotiable rules、Testing 節）
- [ ] 自動売買・自動発注に繋がる導線が存在しないことをレビューでチェックする仕組みを用意する
- [ ] **テストの golden data の出所を決める**
  - AGENT.md Testing 節が「期待値を本体と同じアルゴリズムで再計算するだけのテストは避ける」としているため、MACD/EMA/ATR の期待値の調達元（参考実装 `stock-simulator-codex` の出力、既知データセット、手計算値など）を確定する。

## 7. 軽微な改善
- [x] **AI Verdict の表示方法を検討する**
  - Short 候補に `Bearish` が付けば「AI も同意」、Long 候補に付けば「AI は反対」を意味する。候補方向と切り離して `Bearish` とだけ表示すると誤読されるため、候補方向に対する順張り/逆張りとして見せる（[`ai-analysis.md`](docs/ai-analysis.md)）。
- [ ] **Purpose の重複を解消する**（[`AGENT.md`](AGENT.md) と [`product-spec.md`](docs/product-spec.md) に同内容があり、片方だけ更新される事故の元）
