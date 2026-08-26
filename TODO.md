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

- [x] **テクニカル指標計算エンジンを実装する**（MACD/EMA20・50・200/ATR/出来高倍率。[`technical-analysis.md`](docs/technical-analysis.md) 準拠、Look-ahead bias 規則・point-in-time系列の遵守を含む）
  - **実装結果（2026-08-26）**: Domain層に純粋計算エンジンと、InfrastructureのPIT選択・企業アクション調整境界だけが生成できる検証済み系列型を追加。EMA20/50/200（SMA seed）、MACD 12/26/9（signalもSMA seed）、Wilder ATR14、評価日前20本平均出来高・出来高倍率を`decimal`・中間丸めなしで算出し、当日/判定に必要な前日値、algorithm ID、正規化JSON、指標別入力hash、固定計算起点を返す。最低201本、manifestの価格revision ID/集合hash・企業アクション集合hash、件数・日付範囲・必要本数、日付順/重複、未来足、確定状態、履歴不完全、PIT未検証、企業アクション要照合をfail-closedで検証する。非線形golden値、上場来固定起点、再現性、decimal正規化、分割単位、ゼロ出来高、拒否境界を含む単体テスト21件を追加。
- [x] **候補抽出・スコアリングロジック（全銘柄スキャン）を実装する**（Long/Short非対称条件、0〜100スコア、信頼度ラベル。[`technical-analysis.md`](docs/technical-analysis.md) Candidate score calculation）
  - **実装結果（2026-08-26）**: Domain層に`candidate-scoring-engine-v1`と完全正規化可能な型付き戦略パラメータを追加。MACD方向状態、EMA strict stack、方向別出来高gateをfail-closedで判定し、LongはMACD/EMA、ShortはMACD/EMA/出来高をATR正規化した強度で0〜100へ加算、High/Medium/Lowへ分類する。価格水準・分割単位不変、最終1回丸め、component合計・weight・ordinal・JSONの整合を検証する。Application層に、設定化されたTSE国内普通株ユニバースを決定的順序で処理し、指標を銘柄ごとに1回だけ計算してLong/Shortを別評価する`AllInstrumentScanService`を追加。PIT request identity、run/engine version、parameter snapshot/hashを照合し、進捗・キャンセル・銘柄単位の失敗継続・方向別ランキング・run status集計を返す。Infrastructureが準備した検証済み系列と結果保存を接続できる境界であり、外部データ取得自体はセクション5の別タスクとする。
- [ ] **保有ポジションの再評価（損切・利確・継続保有判定）ロジックを実装する**（ATR倍数・R倍率・部分決済・建値移動・テクニカル反転条件。[`risk-management.md`](docs/risk-management.md)）
- [ ] **AIチェック（Codex CLI）実行連携を実装する**（`AiCheckJob`/`AiAttempt`/`AiResult` の生成・タイムアウト・並列数・失敗時フォールバック。[`ai-analysis.md`](docs/ai-analysis.md)）
- [ ] **株価更新（差分更新）バッチのApplicationユースケースを実装する**（セクション5のHTTPクライアント実装後に接続。日次更新フローの起点）
- [ ] **バックテスト基盤を構築する**（以下の検証タスクすべての前提。[`technical-analysis.md`](docs/technical-analysis.md) Look-ahead bias 節の規則をバックテストにも適用すること）
- [ ] MACD パラメータ 12/26/9 の妥当性検証（[`technical-analysis.md`](docs/technical-analysis.md)）
- [ ] EMA 20/50/200 のトレンド判定ロジックの妥当性検証
- [ ] 出来高倍率フィルタの閾値（1.5倍、仮値）の調整
- [ ] ATR 期間14日、損切倍率 Long 3.0/Short 2.5、50%利確 1.5R、部分決済後の建値移動の妥当性検証（[`risk-management.md`](docs/risk-management.md)）
- [ ] 候補スコアの初期仮値（Long 50/50/0、Short 40/40/20、High >= 80、Medium >= 60）をバックテストで検証・調整する（[`technical-analysis.md`](docs/technical-analysis.md) Candidate score calculation）
- [ ] Long/Short 非対称条件（Short は全条件一致必須）の実データでの妥当性検証
- [ ] 上場来の初期取得について、実行時間・保存容量・取得元の履歴完全性を検証（[`data-sources.md`](docs/data-sources.md)）
- [ ] 日次分析結果について、想定全銘柄数 × Long/Short × 指標数で DB 本体・索引・WAL の行数/bytes per day と1年増分を実測し、バックアップ容量・保持方針を決める（`indicator_results.values_json` に時系列全体を保存しない前提）
- [ ] AI 結果 schema のうち型未確定のフィールド（Summary/TechnicalView/FundamentalView/PositiveFactors/RiskFactors/InvalidationConditions/CheckedAt/Sources）の型を確定する（[`ai-analysis.md`](docs/ai-analysis.md)）
- [ ] Codex CLI のデフォルト timeout（120秒）・並列数2・自動チェック上位件数3/方向の妥当性を実運用で検証

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
