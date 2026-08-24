# TODO

AGENT.md / docs で仮決定した内容に基づく、今後の作業リスト。
未着手（ソリューション自体が未作成）のため、まずは基盤構築から始める。

## 1. プロジェクト基盤構築
- [ ] .sln 作成、Domain / Application / Infrastructure / Presentation の各プロジェクトを分離して作成（[`AGENT.md`](AGENT.md) Architecture 節）
- [ ] WPF + Prism + MahApps.Metro の初期セットアップ
- [ ] SQLite 3 + EF Core を導入し、DbContext の初期マイグレーションを作成
- [ ] `dotnet restore` / `build` / `test` が通る状態を作る（CI 前提の最低ライン）

## 2. 未確定仕様の解消
- [ ] DB の具体的なテーブル定義・列構成を設計する（[`AGENT.md`](AGENT.md) Currently undecided、[`AGENT.md`](AGENT.md) SQLite 節）
  - 価格データ、銘柄マスタ、信用情報、戦略パラメータ、候補/スコア、ポジション、約定履歴、AI 結果の各テーブルを、決定済みドキュメントの内容を踏まえて設計する

## 3. 仮決定パラメータの検証・調整（実装後にバックテスト等で見直す前提）
- [ ] MACD パラメータ 12/26/9 の妥当性検証（[`technical-analysis.md`](docs/technical-analysis.md)）
- [ ] EMA 20/50/200 のトレンド判定ロジックの妥当性検証
- [ ] 出来高倍率フィルタの閾値（1.5倍、仮値）の調整
- [ ] ATR 期間14日、損切倍率 ATR×3、利確 1.5R の妥当性検証（[`risk-management.md`](docs/risk-management.md)）
- [ ] 候補スコアの重み付け（未確定）を決定し、0〜100スコア・信頼度ラベルとの対応を詰める（[`technical-analysis.md`](docs/technical-analysis.md) Candidate score calculation）
- [ ] Long/Short 非対称条件（Short は全条件一致必須）の実データでの妥当性検証
- [ ] 株価データの初期取得期間（直近2年、仮値）で指標計算に十分か検証（[`data-sources.md`](docs/data-sources.md)）
- [ ] AI 結果 schema のうち型未確定のフィールド（Summary/TechnicalView/FundamentalView/PositiveFactors/RiskFactors/InvalidationConditions/CheckedAt/Sources）の型を確定する（[`ai-analysis.md`](docs/ai-analysis.md)）
- [ ] Codex CLI のデフォルト timeout（120秒）・並列数（2〜3）の妥当性を実運用で検証

## 4. 外部データ取得の実装検証
- [ ] Yahoo Finance 非公式 chart API の安定性・レート制限の実地確認、失敗時のフォールバック設計
- [ ] JPX 公式の上場銘柄一覧・信用取引銘柄一覧のファイル形式を確認し、パーサーを実装
- [ ] Yahoo Finance 企業情報エンドポイントから取得できる項目（PER/PBR/時価総額等）の実際の可用性を確認

## 5. Non-negotiable rules の遵守確認
- [ ] 売買履歴が UI 操作以外（BUY/SELLサイン・現在値・AI判定）から自動生成されないことをテストで担保（[`AGENT.md`](AGENT.md) Non-negotiable rules、Testing 節）
- [ ] 自動売買・自動発注に繋がる導線が存在しないことをレビューでチェックする仕組みを用意する
