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
- [x] **主要画面のUIモックを起動し、デザインを利用者と確認する**
  - **実施タイミング**: Domain/Application の主要ユースケースと画面表示用データ契約が固まり、実DB・外部APIへ接続する前。モックデータだけで候補一覧、保有ポジション、約定履歴、手動約定登録、進捗・エラー・AI状態を一通り遷移できる状態にする。
  - Long/Short、Entry/Exit、参考情報と利用者入力の境界が誤読されないこと、重要情報の優先順位、ウィンドウサイズ変更時のレイアウト、確認操作の分かりやすさを実際に起動して確認する。
  - 確認結果を反映してから実データ接続と画面実装を進め、手戻りを抑える。モックから売買履歴を自動生成したり、実注文へつながる導線は作らない。
  - **進捗（Claude Codeが `feature/ui-mock` ブランチで実施中）**: `mocks/SwingAdviser.UiMock`（`src/SwingAdviser.Presentation` とは別プロジェクト、Domainのみ参照・Infrastructure非参照）に、全体レイアウト戦略が異なる3案（案A タブ切替型、案B マスタ詳細2ペイン型、案C ダッシュボード型）をモックデータのみで実装済み。候補一覧は各案作り込み、保有/履歴/明細/手動約定登録は3案共通テンプレート。起動後 `F1`/`F2`/`F3` またはウィンドウ右上のボタンで再起動なしに案を切り替え可能。
    - 起動: `dotnet run --project .\mocks\SwingAdviser.UiMock\SwingAdviser.UiMock.csproj`
    - **検証済み（2026-08-25）**: .NET 10.0.400 SDK導入後（`global.json` を `10.0.303`→`10.0.400` に更新）、`dotnet build` / `dotnet test`（25/25成功）/ 実起動を確認。実際にウィンドウを操作し、3案の切替（F1/F2/F3、状態が引き継がれることを確認）、候補一覧の列表示、AI状態バッジ、売建可否（不明/規制あり/売建不可の書き分け）、保有ポジション明細（コスト未調整の建値候補注記、未公表/確定の書き分け）を目視確認済み。
    - 実機テストで発見・修正したバグ: (1) XAML `mc:Ignorable="d"` に `xmlns:d` 未宣言、(2) `Style.Setter` で `TargetName` を使用（ControlTemplate外では不可）、(3) `MockScenarioState` コンストラクタでコマンド初期化前に `LoadScenario` を呼びNRE、(4) 案間で共有する `ResourceDictionary`（`SharedTemplates.*.xaml`）が他ファイルの `StaticResource` を解決できない、(5) 「再試行」ボタンの活性条件が失敗系のみで情報不足/キャンセル/旧結果から再試行できなかった、(6) 案Bの詳細ペインが未選択時にラベルだけの中途半端な表示になっていた（プレースホルダーを追加）、(7) 案Cの「期限接近」タイルが「期限不明」を誤って合算していた。いずれも修正・再ビルド確認済み。
    - **採用案決定（2026-08-26）**: 案A（タブ切替型）に確定。案B（マスタ詳細2ペイン型）・案C（ダッシュボード型）は不採用。
    - **移植完了（2026-08-26）**: 案Aを `src/SwingAdviser.Presentation` へ移植し、`mocks/SwingAdviser.UiMock` のソースとソリューション登録を削除した。候補/保有/履歴タブ、保有DataGrid、Entry/Current価格列、Entry/Exitの手動登録、訂正revision表示を本番Application/SQLite接続へ置換し、F1/F2/F3切替・起動時オーバーレイ等のモック専用配線は含めていない。
    - **案A 追加修正（2026-08-26、Claude Codeが実施。Codexレビュー未実施＝下記を引き継ぎ用に明記）**:
      - 上部レイアウトを「日次更新」固定枠→タブ（候補/保有/履歴）の順に入替（`Variants/A/TabbedVariantView.xaml`）。固定表示物を先に置く方が利用者の視線導線に合うという判断。
      - 「保有」セクションをカード列からDataGridへ変更し、候補タブと同じ「一覧＋主な判定理由＋操作」の構造に統一（案A限定、案B/Cのカード型 `PositionCardTemplate` は未変更）。列: コード/銘柄名/Long-Short（frozen）+ 種別(Exit固定)/数量/エントリー時価格/現在価格（参考）/価格損益/判定基準バー日/適用戦略/決済判定/主な判定理由/損切候補/利確候補/返済期限/要照合状態/操作。
      - 決済判定（Hold/利確候補/損切候補/決済候補）に `SeverityBadge` を追加（`MockLabels.DecisionSeverity`）。
      - 保有一覧にAI状態・スコア・信頼度列は追加していない。理由: `docs/ai-analysis.md:32`「保有ポジション向けのAI判定はプロンプトと意味が異なるため初期対象外」、`docs/product-spec.md:40`「保有ポジションのAIチェックは対象外」という既存の仮決定を維持する方針を利用者に確認済み（AI チェック対象拡張は別途要検討・未決定のまま）。
      - Exit側の登録フローを候補側（Entry）と対にした: `PositionListViewModel.RequestManualEntryCommand` を追加し、`Decision` が `TakeProfit`/`StopLoss`/`Exit` のときだけ「保有から登録」ボタンを有効化（Hold中は`CanExecute`でボタン無効化）。押すと既存の手動約定登録ダイアログを `ExecutionKind.Close` 初期選択で開く（`ManualExecutionEntryViewModel.Prefill` に `initialKind` 引数を追加）。渡すのは銘柄コード/銘柄名/Long-Shortのみで、価格・日時・株数・充当lotは従来通り利用者入力（AGENT.md Non-negotiable rules 準拠）。
      - `MockPositionSeed` に `EntryBasisPrice`（現在基準の取得単価、要照合中は`null`→「算出不可（要照合中）」表示）と `CurrentPrice`（参考現在価格。約定価格へ自動採用しない）を追加。
      - **モックデータの内部不整合を修正**: Entry/Current価格列を追加する過程で、既存の `PriceProfitAndLoss`（価格損益）が各行の固定ATR・損切倍率・R倍率・決済判定文言と矛盾していることが判明（例: コード9101は「損切候補」判定なのに価格損益が+142,000円の利益表示だった）。コード7203/6920/8035/9101の4件について、価格損益・確定コスト控除後損益・ネット参考損益をATR/R倍率と整合する値へ修正した（コード1605はReconciliation Required、コード4385は元々整合していたため変更なし）。実装ロジックのバグではなく手書きモックデータの整合性修正。
      - **申し送り**: 上記はすべて未コミット（作業ツリー変更のみ）。Codexのstop-review-gateはこのリポジトリでは未有効化（`/codex:setup`未実施）のため自動レビューは走っていない。案Aで確定したため案B/Cへの反映は不要（`mocks/`削除時に一緒に廃棄）。実装移植時は本項目を確認すること。
- [x] **Application の主要ユースケースを実装し、UIをローカルSQLiteへ接続する**
  - UIモックで確定した画面契約を使い、候補参照、保有参照、手動約定登録・訂正、進捗・エラー表示を Application 層経由で動かす。ViewModelからDbContextやRepositoryを直接操作しない。
  - 外部APIへはまだ接続せず、再現可能なローカルテストデータを投入できる開発用経路を用意する。実運用DBとテストDBを混同しない。
  - **引継ぎ**: `mocks/SwingAdviser.UiMock/Shared/MockLabels.cs` のラベル整形ロジック（AI verdictは候補方向とセットで表示、コスト欠損は¥0にしない、売建可否不明を可能と推測しない 等）を本番の表示ロジックとして移植し、以下3点は必ずテストで担保する: (1) `CostAmountLabel` が `Unpublished`/`FetchFailed`/`Unknown`/`NotOccurred` で `¥0` を含む文字列を返さない、(2) `AiVerdictAlignmentLabel` が Long/Short×Bullish/Neutral/Bearish/nullの全組み合わせで整合ラベルなしの裸の強気/弱気を返さない、(3) `ShortAvailabilityLabel(Unknown)` が「不明」を含み「可」を含まない。モック側のコピーはこの移植後に削除してよい。
  - **実装結果（2026-08-26）**: `TradingWorkspaceService` と `ITradingWorkspaceRepository` をApplication境界に追加し、候補/保有/履歴照会、利用者確認済み約定の登録、明示lot配分、訂正revision追記を実装した。InfrastructureのSQLite実装はtransaction内で企業アクション調整後のlot残数量、要照合状態、楽観的revision一致を検証し、約定訂正後は元のOpen/Closed状態を維持したまま依存データを要照合にする。`--development-data` で実運用DBと別の `swing-adviser.development.db` を使用し、冪等なローカル結合データを投入できる。ラベル3要件を含むPresentationテスト14件、Application/Repository/seedを含むInfrastructureテスト33件が成功。
- [x] **ローカル結合版を起動し、実際の画面操作で一連の動作を検証する**
  - **実施タイミング**: Domainモデル、`AddBusinessSchema`、Repository、主要Applicationユースケース、UI接続が揃った後。セクション4のバックテストおよびセクション5の外部データ接続より前に実施する。
  - テストデータを使い、起動 → 一覧表示 → 詳細確認 → 利用者入力 → 確認 → SQLite保存 → 再起動後の再表示 → 訂正revision追加までを操作する。
  - Long/Short・Entry/Exitの誤読、入力検証、キャンセル、多重実行防止、進捗、失敗表示を確認し、分析結果や現在値から売買履歴が自動生成されないことも確認する。
  - **実機確認（2026-08-26）**: 開発用DBで起動し、候補2件/保有1件/約定1件の初期表示、候補/保有/履歴タブ切替をUI Automationで確認。候補から登録を開いた時点で銘柄/方向だけが引き継がれ、約定日・時刻・価格・株数を含む編集欄7個が空であることを確認した。確認画面を経て7203 Longの新規建を登録し、候補2件/保有2件/約定2件へ再読込されたこと、空入力は検証エラーになりキャンセル後も件数不変であることを確認。画面から価格2800円→2810円の訂正理由付きrevisionを追記し、再起動後にrev1/¥2,800とrev2/¥2,810が履歴詳細へ復元されたことを確認した。履歴詳細展開時に発見した`Run.Text`のTwoWayバインド例外はOneWay固定へ修正済み。

## 4. 仮決定パラメータの検証・調整（実装後にバックテスト等で見直す前提）

**前提（2026-08-26 レビューで判明。）**: 現状 `src/SwingAdviser.Domain/Analysis/*`（`CandidateResult`/`TechnicalAnalysisResult` 等）はエンティティ（入れ物）のみで、テクニカル指標計算・候補抽出・スコアリング・保有ポジションの損切利確判定・AIチェック実行のロジックは1行も実装されていない。セクション3で完成した「主要ユースケース」は候補/保有の参照・手動約定登録・訂正のみで、AGENT.md Application 節が挙げる「株価更新、全銘柄スキャン、候補抽出、保有再評価、AIチェック」本体は未着手。本章の検証・調整タスクはこれらの実装が前提のため、以下を先行実装タスクとして明記する（未実装のままでは「検証」を開始できない）。

### 4.0 実装済みの前提

- [x] **4.0.1 テクニカル指標計算エンジンを実装する**（MACD/EMA20・50・200/ATR/出来高倍率。[`technical-analysis.md`](docs/technical-analysis.md) 準拠、Look-ahead bias 規則・point-in-time系列の遵守を含む）
  - **実装結果（2026-08-26）**: Domain層に純粋計算エンジンと、InfrastructureのPIT選択・企業アクション調整境界だけが生成できる検証済み系列型を追加。EMA20/50/200（SMA seed）、MACD 12/26/9（signalもSMA seed）、Wilder ATR14、評価日前20本平均出来高・出来高倍率を`decimal`・中間丸めなしで算出し、当日/判定に必要な前日値、algorithm ID、正規化JSON、指標別入力hash、固定計算起点を返す。最低201本、manifestの価格revision ID/集合hash・企業アクション集合hash、件数・日付範囲・必要本数、日付順/重複、未来足、確定状態、履歴不完全、PIT未検証、企業アクション要照合をfail-closedで検証する。非線形golden値、上場来固定起点、再現性、decimal正規化、分割単位、ゼロ出来高、拒否境界を含む単体テスト21件を追加。
- [x] **4.0.2 候補抽出・スコアリングロジック（全銘柄スキャン）を実装する**（Long/Short非対称条件、0〜100スコア、信頼度ラベル。[`technical-analysis.md`](docs/technical-analysis.md) Candidate score calculation）
  - **実装結果（2026-08-26）**: Domain層に`candidate-scoring-engine-v1`と完全正規化可能な型付き戦略パラメータを追加。MACD方向状態、EMA strict stack、方向別出来高gateをfail-closedで判定し、LongはMACD/EMA、ShortはMACD/EMA/出来高をATR正規化した強度で0〜100へ加算、High/Medium/Lowへ分類する。価格水準・分割単位不変、最終1回丸め、component合計・weight・ordinal・JSONの整合を検証する。Application層に、設定化されたTSE国内普通株ユニバースを決定的順序で処理し、指標を銘柄ごとに1回だけ計算してLong/Shortを別評価する`AllInstrumentScanService`を追加。PIT request identity、run/engine version、parameter snapshot/hashを照合し、進捗・キャンセル・銘柄単位の失敗継続・方向別ランキング・run status集計を返す。Infrastructureが準備した検証済み系列と結果保存を接続できる境界であり、外部データ取得自体はセクション5の別タスクとする。
以下の未実装項目は、**原則としてチェックボックス1個をCodexの5時間利用枠1回以内で実装・テスト・差分確認まで完了できる作業単位**として分割する。`依存`に未完了項目がある場合は、先にその項目を完了する。各項目のテストは正常系だけでなく、記載したfail-closed条件とNon-negotiable rulesも確認する。

### 4.1 保有ポジションの再評価

- [x] **4.1.1 再評価の判定契約と競合時の優先順位を確定する**（[`risk-management.md`](docs/risk-management.md) とDB schemaだけでは一意に決まらない、ライン到達に使うOHLC、同一足で損切・利確・反転が競合した場合、過去の1.5R到達状態、複数lotの集約規則を決定表として文書化する。Long/Shortの境界値、データ不足、要照合時の期待結果まで固定する）
  - **決定結果（2026-08-27）**: `holding-risk-evaluation-v1` を文書化。日足 High/Low の一致を含む方向別ライン到達、MACDのstrict cross、EMA20のstrictな終値状態、同一足の `StopLoss > Exit > TakeProfit > Hold` を固定した。過去1.5R到達はlot別の適格足・risk-plan revision証跡から再構築し、複数lotを平均せず同じ優先順位でpositionへ集約する。判定不能を`Hold`へ変換しない`evaluation_outcome`とnull decision契約をDB schemaへ追加し、要照合、履歴不足/不完全、PIT未検証、単位不正、日中順序不明をfail-closedにした。実DBへの追加migrationは永続化を実装する4.1.10で行う。
- [x] **4.1.2 建玉時の固定リスク基準と初期リスクプラン生成ロジックを実装する**（`依存: 4.1.1`。`RiskBasisSnapshot` と初期 `RiskPlanRevision` を、Long 3.0 ATR、Short 2.5 ATR、1.5R、50%の凍結値から生成する。損切・利確価格、固定ATR、1R、非正価格、単位不一致をDomainテストで確認する）
  - **実装結果（2026-08-27）**: Domain層に`RiskManagementParameters.Initial`（Long 3.0、Short 2.5、1.5R、50%）と`initial-risk-plan-factory-v1`を追加し、利用者確認済みOpen execution・MarginLot・Positionを照合して、lot別`RiskBasisSnapshot`とrevision 1・triggerなしの初期`RiskPlanRevision`を必ず同じ算出ラインから生成する。instrument/currency/split-consolidation単位hash付き価格型でentryと固定ATRの単位一致を強制し、opening価格・通貨・effective leaf、position sideをcaller入力に依存せず検証する。Long/Short golden値、初期plan不変条件、0/負の算出ライン、default値、異通貨・異株式単位・別instrument/position、opening訂正、未知side、decimal overflowを含むDomainテスト15件を追加した。単位列の追加migrationと保存接続は4.1.3で行う。
- [x] **4.1.3 利用者確認済み新規建へリスク基準を付与・保存する**（`依存: 4.1.2`。候補由来は候補の評価日ATR、手動建玉は約定前の直近確定足ATRを使用し、risk basisと初期planを同一transactionでappend-only保存する。使用revision/manifest/parameter snapshotを凍結し、未来足を使わないRepositoryテストを追加する）
  - **実装結果（2026-08-27）**: 利用者確認済みOpen登録transaction内でlot生成と同時に`RiskBasisSnapshot`とrevision 1の初期`RiskPlanRevision`を追記するようにした。候補由来はexact candidate graphの評価日ATR14、候補なしはJST約定日より前かつ約定時刻より前に分析・記録・available cutoffが確定した最新のVerified ATR14を使用し、opening revision、candidate、analysis input manifest、strategy parameter snapshot、currency、corporate-action set由来のprice-unit hashを凍結する。ATRなしではposition/execution/lot/risk graph全体を保存しない。単位列のadditive migration、SQLite table rebuild後のappend-only trigger復元、開発seedの未来候補・二重risk basisを修正し、候補provenance、未来分析除外、全体rollbackをRepositoryテストで確認した。
- [x] **4.1.4 1 lotの損切・1.5R到達・継続保有判定を実装する**（`依存: 4.1.1, 4.1.2`。有効な最新risk-plan leafを使ってLong/Short別に `StopLoss` / `TakeProfit` / `Hold` と構造化理由を返し、ライン未満・一致・超過をDomainテストで確認する）
  - **実装結果（2026-08-27）**: Domain層に純粋計算の`LotRiskEvaluator`（`holding-risk-evaluation-v1`）を追加した。risk basisと同じprice unitのHigh/Low、risk-plan revision graph、評価セッション開始cutoffを受け、cutoff以前に発効・記録済みの単一leafを連続revision chainから選択する。Longは`Low <= stop`/`High >= target`、Shortは`High >= stop`/`Low <= target`を一致込みで評価し、両方到達時は`StopLoss`を優先する。代表判定にかかわらず、line種別、High/Low、比較演算子、観測価格、ライン、到達有無を構造化理由として両方返す。Long/Shortの未到達・一致・超過、同一足競合、最新as-of leaf、未来revision除外、欠落・分岐・別basis graph、単位不一致、High/Low不正をDomainテストで確認した。
- [x] **4.1.5 1.5R到達後のテクニカル反転判定を実装する**（`依存: 4.1.4`。LongはMACDデッドクロスまたはEMA20割れ、ShortはMACDゴールデンクロスまたはEMA20上抜けを評価する。各条件単独・両方・未成立・指標不足をテストし、instrument単位のExit結果を生成せずposition単位の判定に閉じる）
  - **実装結果（2026-08-27）**: Domain層にlot単位の`LotHoldingRiskEvaluator`を追加し、4.1.4の価格ライン結果へテクニカル反転を統合した。Longは前日MACD lineがsignal以上かつ当日strict未満、または`Close < EMA20`、Shortは前日lineがsignal以下かつ当日strict超過、または`Close > EMA20`で反転成立とする。条件ごとに`Matched`/`NotMatched`/`Missing`を保持し、一方成立なら他方欠損でも`Exit`、両方不成立だけ`TakeProfit`、不成立+欠損または両欠損は`Indeterminate`かつdecision nullとしてfail-closedにした。過去1.5R状態は生booleanではなく`NotReached`/exact revision証跡付き`Reached`/`Indeterminate`で受け、当日到達と統合する。`StopLoss > Exit > TakeProfit > Hold`をlot内で適用し、StopLoss時もtarget・反転根拠を保持する。Long/ShortのMACD単独、EMA20単独、両方、strict等値境界、未成立、欠損、当日/過去target、履歴不明、stop競合、単位・証跡不正をDomainテストで確認し、instrument単位の分析結果は生成しない。
- [ ] **4.1.6 一部利確候補数量の計算を実装する**（`依存: 4.1.4`。現在数量の50%を売買単位で切り下げ、決済後にも1売買単位以上残せる場合だけ `Candidate`、不可能なら `NotFeasible` とする。全決済候補への暗黙変換や保有数量の自動変更を禁止するテストを追加する）
- [ ] **4.1.7 利用者登録済み部分決済後の建値移動を実装する**（`依存: 4.1.3, 4.1.6`。有効な部分決済約定と明示lot配分revisionがある場合だけ、Longは `max(従来stop, entry_basis)`、Shortは `min(...)` のplan revisionを追記する。価格到達だけでは追記せず、stopを不利な方向へ緩めず、旧planを上書きしないことをテストする）
- [ ] **4.1.8 価格損益・信用コスト・コスト/Rのlot別集計を実装する**（`依存: 4.1.2`。Long/Shortの価格損益、確定コスト控除後損益、見積込みネット損益、コスト/Rを算出する。EstimateとConfirmedの二重計上を避け、既知0と未公表・取得失敗・不明を区別し、欠損を0へ変換しない）
- [ ] **4.1.9 再評価用point-in-timeポジションprojectionと入力manifest生成を実装する**（`依存: 4.1.3, 4.1.7, 4.1.8`。約定、lot配分、企業アクション調整、契約、risk basis/plan、コストのexact leaf IDをcutoff時点で選択して正規化JSON/hashを生成する。後日訂正後の再構築、同一position/lot graph、要照合・未対応actionのfail-closedをRepositoryテストで確認する）
- [ ] **4.1.10 評価manifestと評価結果の原子的な永続化を実装する**（`依存: 4.1.4〜4.1.9`。`position_evaluation_input_manifests` と `position_evaluations` を同一transactionでappend-only保存し、lot別根拠を残す。hash、同一run/position重複拒否、読戻し、同一銘柄の複数positionが独立することを結合テストで確認する）
- [ ] **4.1.11 保有ポジション再評価のApplicationユースケースを実装する**（`依存: 4.1.5〜4.1.10`。Open positionを決定的順序で処理し、position単位の失敗継続、進捗、キャンセル、run status集計を実装する。全成功・一部失敗・全失敗・同一銘柄複数positionをテストし、`trade_executions`を生成しないことを確認する）
- [ ] **4.1.12 保有画面への再評価結果接続と安全性の結合確認を行う**（`依存: 4.1.11`。最新評価、損切・利確候補、Hold理由、価格/コスト損益、要照合状態を既存の読取経路へ反映する。利用者が明示登録するまで数量・risk plan・約定履歴が変化しない回帰テストと実画面確認を行う）

### 4.2 AIチェック（Codex CLI）実行連携

- [ ] **4.2.1 AI実行のDomain契約をDB schemaへ整合させる**（`AiCheckJob` / `AiAttempt` / request event / result sourceへ、自動実行設定snapshot/hash、要求日時、CLI診断、Retry/Recheckの不変条件を反映する。User/Automatic別必須項目、状態遷移、終端不変、`InsufficientInformation`と`Neutral`の区別をDomainテストで確認する）
- [ ] **4.2.2 AI入力snapshotとprompt templateの正規化・hash生成を実装する**（`依存: 4.2.1`。保存済みEntry候補から評価日、方向、テクニカルmanifest、戦略hashを含む秘密情報なしの決定的JSONとversion付きpromptを生成する。同一入力のhash再現性、未来情報・保有/Exit候補・秘密情報の除外をgolden testで確認する）
- [ ] **4.2.3 Codex CLI実行profileの設定検証・snapshot化を実装する**（`依存: 4.2.1`。executable/PATH、working directory、model、timeout既定120秒、追加引数、最大並列既定2を解決し、秘密を除いたprofile/arguments JSONとhashを作る。既定値、上書き、不正値、hash再現性をテストする）
- [ ] **4.2.4 AI結果のsemantic schema v1を確定する**（`依存: 4.2.1`。Summary/TechnicalView/FundamentalView/PositiveFactors/RiskFactors/InvalidationConditions/CheckedAt/Sourcesの型、null可否、上限、source順、schema versionを文書とDomain型へ反映する。`InsufficientInformation`時に売買方向を推測せず、旧versionを黙って現行扱いしないfixtureを用意する）
- [ ] **4.2.5 Codex CLIの構造化応答parserを実装する**（`依存: 4.2.4`。version付きJSONを `AiResult` とsourcesへ変換し、Succeeded、InsufficientInformation、InvalidResponse、ParseFailureを区別する。全verdict/confidence、不正JSON/enum、必須欠落、source順をfixtureテストで確認する）
- [ ] **4.2.6 Codex CLI process runnerを実装する**（`依存: 4.2.3`。shellを介さず引数配列で起動し、非同期stdout/stderr、exit code、timeout、利用者cancel、取得可能なCLI version/model、response hash、長さ制限・sanitized済みstderrを返す。fake CLIで成功、非0終了、timeout、cancel、大量stderr、空stdoutを統合テストする）
- [ ] **4.2.7 AI snapshot・job・初回attempt投入Repositoryを実装する**（`依存: 4.2.1〜4.2.3`。prompt/profile snapshotの再利用とjob/request event/Queued attemptを同一transactionで保存し、既存unique条件で重複を抑止する。User/Automatic制約とrollbackをSQLiteテストで確認する）
- [ ] **4.2.8 AI attempt遷移・結果保存・再試行Repositoryを実装する**（`依存: 4.2.5, 4.2.7`。event追記とstatus projection更新をatomic化し、診断、result/source、Retry/Recheckの新attempt、Queued取消を保存する。無効遷移・終端更新拒否、attempt番号、過去結果不変、1 job 1 activeをテストする）
- [ ] **4.2.9 利用者選択AIチェックのApplicationユースケースを実装する**（`依存: 4.2.2, 4.2.3, 4.2.7`。単件/複数の明示選択を検証・凍結してenqueueし、自動jobがQueuedなら複製せず優先度昇格eventを追加する。Entry候補限定、部分失敗継続、重複抑止をテストする）
- [ ] **4.2.10 自動AIチェック対象選定ユースケースを実装する**（`依存: 4.2.2, 4.2.3, 4.2.7`。有効時だけLong/Short各上位N件をscore降順・同点code昇順で選び、rank/policy/config snapshotを保存する。既定3件/方向、無効、N上書き、テクニカル確定前の投入拒否をテストする）
- [ ] **4.2.11 永続AIキューworkerを実装する**（`依存: 4.2.5, 4.2.6, 4.2.8`。priority/requested時刻順、最大並列数の範囲でRunning化→実行→parse→終端保存を行い、1件失敗で他を停止せず暗黙retryしない。成功、各失敗、情報不足、cancel、並列上限、User優先を決定的にテストする）
- [ ] **4.2.12 アプリ起動時のAIキュー復旧を実装する**（`依存: 4.2.8, 4.2.11`。残存Runningをevent付き `Failed/Interrupted` へ変換し、Queuedを再開可能にする。終端attemptを変更しない再起動テストを追加する）
- [ ] **4.2.13 最新AI結果・Stale判定の照会を実装する**（`依存: 4.2.8`。候補照会へ最新attempt/result/失敗詳細/sourceを投影し、新しいanalysis runがあれば保存結果を書き換えずStaleを導出する。未実行、各終端状態、Retry後最新、AI失敗時もテクニカル候補が残ることをテストする）
- [ ] **4.2.14 候補画面のAI実行・取消・再試行UIを接続する**（`依存: 4.2.9, 4.2.11, 4.2.13`。単件/複数選択、明示実行、Queued/Running表示、Queued取消、失敗系Retry/Recheck、非同期更新、多重押下防止をViewModel経由で接続する。AI見通しを売買推奨として表示せず、AI失敗でも候補・手動約定機能を利用可能にする）
- [ ] **4.2.15 AIチェック連携のE2E検証を追加する**（`依存: 4.2.9〜4.2.14`。一時SQLiteとfake CLIでenqueue→実行→parse→永続化→再表示を確認し、成功、timeout、非0終了、情報不足、再起動復旧を通す。利用可能な環境では実Codex CLIの単件smokeも行い、未認証等は診断可能な失敗として扱う）

### 4.3 株価更新（差分更新）バッチのApplicationユースケース

- [ ] **4.3.1 株価更新ユースケースのprovider非依存Application契約を定義する**（request/result、対象銘柄、取得範囲、進捗、キャンセル、設定snapshot、`IPriceHistorySource`、永続化port、Http/RateLimit/Timeout/InvalidData/MissingData/ProviderChanged/Cancelled/DatabaseLocked/Unknownの失敗分類を定義する。不正時刻、空provider、重複銘柄、範囲逆転をfail-fastし、銘柄順と設定hashの決定性をテストする）
- [ ] **4.3.2 初回取得・差分取得の範囲計画を実装する**（`依存: 4.3.1`。最新の有効な日足leaf、上場日証跡、評価日から、初回は上場来、2回目以降は設定化した訂正再取得重複期間を含む差分範囲を算出する。履歴なし/あり、上場日不明、未来日、上場廃止、同日再実行を境界テストする）
- [ ] **4.3.3 data update run/item/failureのRepositoryと状態遷移を実装する**（`依存: 4.3.1`。Queued→Running→Succeeded/PartiallySucceeded/Failed/Cancelled、秘密を除く設定snapshot/hash、項目別結果、sanitized failureを保存する。終端後更新拒否、件数集計、監査行のUPDATE/DELETE拒否をSQLiteテストで確認する）
- [ ] **4.3.4 日足の冪等な追記・訂正保存を実装する**（`依存: 4.3.1, 4.3.3`。natural key、canonical content hash、`daily_price_revisions`、source artifact、data update itemを1取引境界で保存し、Inserted/Corrected/Unchangedを判定する。同一再取得でrevisionを増やさず、訂正はsupersedes付きで追記し、不正OHLCV・重複日・通貨不整合を拒否する）
- [ ] **4.3.5 同一chart応答の企業アクション差分保存を実装する**（`依存: 4.3.1, 4.3.3`。source event IDまたはderived keyでidentityを決め、分割・併合・配当・取消・訂正をappend-only保存し、available/observed/recorded時刻とPIT状態を残す。同一イベント、訂正、取消、未知・不完全イベントをテストし、推測で補完しない）
- [ ] **4.3.6 価格履歴の完全性評価を実装する**（`依存: 4.3.2, 4.3.4`。取得結果、上場日証跡、確定バー、欠損からCompleteFromListing/Incomplete/Unverified/Invalidを追記する。上場日不明、先頭・途中欠落、不正バーをComplete扱いせず、下流へ明示的な `HistoryIncomplete` 等を渡す）
- [ ] **4.3.7 全銘柄差分更新バッチのオーケストレーションを実装する**（`依存: 4.3.2〜4.3.6`。範囲計画→source取得→日足/企業アクション保存→完全性評価を銘柄単位で実行し、多重実行防止、制限付き並列取得、進捗、キャンセル、失敗継続、最終status集計を行う。fake sourceで全成功・部分成功・全失敗・rate limit・再実行をテストし、売買履歴や分析結果を生成しない）
- [ ] **4.3.8 セクション5のYahoo HTTPクライアントを接続して結合検証する**（`依存: 4.3.7、セクション5のchart API client、銘柄コード正規化ルール`。DIでInfrastructure portを接続し、日足・企業アクション・取得時刻/artifact・分類済み失敗をApplicationへ渡す。stub HTTPで初回上場来、翌日差分、Unchanged、過去バー訂正、429、timeout、schema変更、部分失敗を確認する。実APIの安定性測定はセクション5に残す）

### 4.4 バックテスト基盤

- [ ] **4.4.1 バックテストの実行契約と「正式結果」の成立条件を定義する**（日付範囲、ユニバース、戦略parameter snapshot、売買・コスト仮定、結果型、再現性情報を型付き契約にする。`Backtest` / `Succeeded` / `PointInTime Verified`を満たすrunだけを正式結果とし、PIT保証なしは参考結果として明示する。実運用の約定・position・lotへ書き込まない境界をArchitectureテストで固定する）
- [ ] **4.4.2 market calendarから評価日列を生成する**（`依存: 4.4.1`。指定期間と凍結したcalendar versionから取引日を昇順生成し、休日を推測・補間しない。期間端、休場日、calendar欠損・version不一致をテストする）
- [ ] **4.4.3 評価日ごとのpoint-in-time再生入力builderを実装する**（`依存: 4.4.1, 4.4.2`。各評価日Dと情報cutoff A/recorded cutoffから、当時利用可能な銘柄master、日足revision、企業actionだけを選び、exact manifest/hashを生成する。後日訂正・未来action・available_at不明を正式入力へ混入させないRepositoryテストを追加する）
- [ ] **4.4.4 本番の指標・候補engineを再利用する履歴スキャンを実装する**（`依存: 4.4.3`。各評価日を `AnalysisRunMode.Backtest` で既存 `AllInstrumentScanService` へ渡し、engine/version/parameter snapshotを固定して候補・除外理由を保存する。同じ入力の再現性と、日付間で未来の指標値を再利用しないことをテストする）
- [ ] **4.4.5 バックテスト専用のentry/exit価格成立ルールを確定して型付きparameter化する**（`依存: 4.4.1`。候補日の次取引日以降のentry、Long/Short、寄付/指値相当、slippage、手数料、売建不可、同一足でstop/targetが両方到達した場合の扱いを文書化・version化する。未確定値をコードへ埋め込まず、曖昧な足を有利に補完しない）
- [ ] **4.4.6 バックテスト専用のposition/lot状態遷移engineを実装する**（`依存: 4.1.4〜4.1.7, 4.4.5`。entry、Long/Short、stop、1.5R部分利確、建値移動、反転exit、最終日処理を純粋計算し、イベントと根拠を返す。実運用 `trade_executions` / `positions` / `margin_lots` を生成せず、価格競合・売買単位・資金不足をgolden testで確認する）
- [ ] **4.4.7 バックテスト指標集計engineを実装する**（`依存: 4.4.6`。取引数、勝率、平均/中央値R、総損益、profit factor、最大drawdown、保有期間、Long/Short別内訳、除外件数をイベント列から決定的に算出する。0件、全勝/全敗、コスト、部分利確、未決済を手計算golden値でテストする）
- [ ] **4.4.8 バックテスト保存schemaを追加migrationとして実装する**（`依存: 4.4.1, 4.4.5`。definition/run、日次portfolio、バックテスト専用position/lot event、metricsを、分析run/manifest/parameter hashへ参照可能なappend-only表として追加する。実運用約定表とのFK/書込経路を持たないこと、制約、索引、migration upgradeをテストする）
- [ ] **4.4.9 バックテスト結果Repositoryを実装する**（`依存: 4.4.7, 4.4.8`。run状態、日次状態、イベント、metricsをtransaction単位で追記し、terminal runを不変にする。中断run、重複防止、hash/件数整合、保存→読戻し→同一metrics再計算をSQLiteテストで確認する）
- [ ] **4.4.10 期間全体のバックテストApplicationユースケースを実装する**（`依存: 4.4.2〜4.4.9`。評価日列→PIT入力→履歴スキャン→状態遷移→保存を順に実行し、進捗、キャンセル、再開不可の中断状態、失敗隔離、最終statusを返す。同一parameter/inputの再実行が同一結果になる統合テストを追加する）
- [ ] **4.4.11 複数parameter snapshotの比較ユースケースを実装する**（`依存: 4.4.10`。同じ期間・universe・PITデータに対するparameter setを独立runとして実行し、metrics差分を比較可能にする。全期間統計を候補スコア計算へ戻さず、各runのversion/hashと成功条件が同一でない比較を警告または拒否する）
- [ ] **4.4.12 Look-ahead biasと実運用データ非干渉のE2E回帰テストを追加する**（`依存: 4.4.10`。小さな既知データで未来価格、翌日価格での当日signal確定、後日訂正、未来/後日判明企業action、available_at不明を検査する。実運用の約定・position件数が前後不変であること、保存runのmanifest/hashから結果を再現できること、build/test成功を確認する）

### 4.5 仮決定パラメータと運用値の検証・調整

- [ ] **4.5.1 MACD パラメータ 12/26/9 の妥当性を検証する**（[`technical-analysis.md`](docs/technical-analysis.md)）
- [ ] **4.5.2 EMA 20/50/200 のトレンド判定ロジックの妥当性を検証する**
- [ ] **4.5.3 出来高倍率フィルタの閾値（1.5倍、仮値）を調整する**
- [ ] **4.5.4 ATR 期間14日、損切倍率 Long 3.0/Short 2.5、50%利確 1.5R、部分決済後の建値移動の妥当性を検証する**（[`risk-management.md`](docs/risk-management.md)）
- [ ] **4.5.5 候補スコアの初期仮値（Long 50/50/0、Short 40/40/20、High >= 80、Medium >= 60）をバックテストで検証・調整する**（[`technical-analysis.md`](docs/technical-analysis.md) Candidate score calculation）
- [ ] **4.5.6 Long/Short 非対称条件（Short は全条件一致必須）の実データでの妥当性を検証する**
- [ ] **4.5.7 上場来の初期取得について、実行時間・保存容量・取得元の履歴完全性を検証する**（[`data-sources.md`](docs/data-sources.md)）
- [ ] **4.5.8 日次分析結果のDB増加量と保持方針を実測・決定する**（想定全銘柄数 × Long/Short × 指標数でDB本体・索引・WALの行数/bytes per dayと1年増分を測る。`indicator_results.values_json` に時系列全体を保存しない前提）
- [ ] **4.5.9 Codex CLIのtimeout 120秒・並列数2・自動チェック上位3件/方向の妥当性を実運用で検証する**

## 5. 外部データ取得の実装検証

**前提（2026-08-26 レビューで判明。）**: 現状 Infrastructure に Yahoo Finance/JPX への HTTP クライアントは未実装。以下の「実地確認」項目は実装と同時並行、または実装直後に行う。

- [ ] **Yahoo Finance 非公式 chart API のクライアントを実装する**（日足取得・差分更新・企業アクション取得。[`data-sources.md`](docs/data-sources.md)）
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
- [x] **Purpose の重複を解消する**（[`AGENT.md`](AGENT.md) と [`product-spec.md`](docs/product-spec.md) に同内容があり、片方だけ更新される事故の元）
  - product-spec.md の Purpose 節を AGENT.md への参照のみに変更し、内容を一本化した。
