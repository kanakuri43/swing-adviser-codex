using SwingAdviser.Domain.Common;

namespace SwingAdviser.UiMock.Mock;

/// <summary>
/// モック用の固定データセット。全て決定論的な値で、乱数は使わない。
/// 3案すべてが同一のインスタンスを参照することで、案間の比較が同一内容で行える。
/// </summary>
public static class MockDataSet
{
    private static readonly DateOnly EvaluationBarDate = new(2026, 8, 25);
    private static readonly DateTimeOffset AnalyzedAtUtc = new(2026, 8, 25, 9, 5, 0, TimeSpan.Zero); // 2026-08-25 18:05 JST

    public static MockScenario Normal { get; } = BuildNormal();
    public static MockScenario ZeroCandidates { get; } = BuildZeroCandidates();

    public static IReadOnlyList<MockScenario> All { get; } = new[] { Normal, ZeroCandidates };

    private static MockScenario BuildNormal()
    {
        var candidates = new List<MockCandidateSeed>
        {
            new("7203", "トヨタ自動車", PositionSide.Long, 82, ConfidenceLevel.High, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50>EMA200、MACDゴールデンクロス、出来高1.8倍",
                MockAiState.Succeeded, AiVerdict.Bullish, null, null, null, null),
            new("6758", "ソニーグループ", PositionSide.Long, 71, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50、MACDゴールデンクロス直後",
                MockAiState.Succeeded, AiVerdict.Bearish, null, null, null, null),
            new("9432", "日本電信電話", PositionSide.Long, 64, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50>EMA200、出来高1.6倍",
                MockAiState.Succeeded, AiVerdict.Neutral, null, null, null, null),
            new("8306", "三菱UFJフィナンシャル・グループ", PositionSide.Long, 58, ConfidenceLevel.Low, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50、MACD弱含み",
                MockAiState.NotRun, null, null, null, null, null),
            new("4063", "信越化学工業", PositionSide.Long, 55, ConfidenceLevel.Low, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50、出来高1.5倍ぎりぎり",
                MockAiState.Queued, null, null, null, null, null),
            new("6501", "日立製作所", PositionSide.Long, 68, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "MACDゴールデンクロス、EMA20>EMA50>EMA200",
                MockAiState.Running, null, null, null, null, null),
            new("7267", "本田技研工業", PositionSide.Long, 61, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50、出来高1.7倍",
                MockAiState.Failed, null, null, "Codex CLI応答の解析に失敗（JSON不正）", null, null),
            new("6902", "デンソー", PositionSide.Long, 57, ConfidenceLevel.Low, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50、MACD弱含み",
                MockAiState.FailedInterrupted, null, null, "アプリ終了により実行中に中断", null, null),
            new("4568", "第一三共", PositionSide.Long, 66, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "MACDゴールデンクロス、出来高1.9倍",
                MockAiState.TimedOut, null, null, "180秒でタイムアウト", null, null),
            new("8035", "東京エレクトロン", PositionSide.Long, 74, ConfidenceLevel.High, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50>EMA200、MACDゴールデンクロス、出来高2.1倍",
                MockAiState.InsufficientInformation, null, null, "参照可能な材料が不足", null, null),
            new("9984", "ソフトバンクグループ", PositionSide.Long, 62, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "EMA20>EMA50、出来高1.6倍",
                MockAiState.Cancelled, null, null, "利用者が待機中にキャンセル", null, null),
            new("6098", "リクルートホールディングス", PositionSide.Long, 70, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングLong v1", "MACDゴールデンクロス、EMA20>EMA50",
                MockAiState.Stale, null, new DateOnly(2026, 8, 22), null, null, null),
            new("6920", "レーザーテック", PositionSide.Short, 77, ConfidenceLevel.High, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングShort v1", "全条件一致（EMA逆行、MACDデッドクロス、出来高1.6倍）",
                MockAiState.Succeeded, AiVerdict.Bearish, null, null, OpenPermissionStatus.Allowed, null),
            new("3092", "ZOZO", PositionSide.Short, 69, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングShort v1", "全条件一致（EMA逆行、MACDデッドクロス、出来高1.5倍）",
                MockAiState.Succeeded, AiVerdict.Bullish, null, null, OpenPermissionStatus.Unknown,
                "売建可否は未取得。売建可能とは推測しない"),
            new("2413", "エムスリー", PositionSide.Short, 65, ConfidenceLevel.Medium, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングShort v1", "全条件一致（EMA逆行、MACDデッドクロス、出来高1.7倍）",
                MockAiState.NotRun, null, null, null, OpenPermissionStatus.Restricted,
                "規制コード: 増担保規制"),
            new("4385", "メルカリ", PositionSide.Short, 60, ConfidenceLevel.Low, EvaluationBarDate, AnalyzedAtUtc,
                "標準スイングShort v1", "全条件一致（EMA逆行、MACDデッドクロス、出来高1.5倍）",
                MockAiState.NotRun, null, null, null, OpenPermissionStatus.Prohibited,
                "貸株在庫なし（貸株=Ineligible）のため売建不可。テクニカル候補ではあるが実際には売建できない"),
        };

        var exclusions = new List<MockExclusionSeed>
        {
            new("285A", "（新規上場・コード未定着）", TechnicalAnalysisOutcome.InsufficientHistory, 62, 200,
                "上場から日が浅く必須本数に未達"),
            new("7011", "三菱重工業", TechnicalAnalysisOutcome.HistoryIncomplete, 198, 200,
                "日足に欠損あり（2026-05-07, 2026-06-11 未取得）"),
            new("4901", "富士フイルムホールディングス", TechnicalAnalysisOutcome.InvalidData, null, null,
                "2026-08-21 の日足で高値<終値の不正値を検出"),
            new("9101", "日本郵船", TechnicalAnalysisOutcome.PointInTimeUnverified, null, null,
                "配当落ち調整で分母(株価)が調整額以下となり自動調整不可"),
            new("1605", "INPEX", TechnicalAnalysisOutcome.ReconciliationRequired, null, null,
                "未対応の企業アクション（株式交換）を検出。照合完了まで対象外"),
            new("6146", "ディスコ", TechnicalAnalysisOutcome.Failed, null, null,
                "指標計算中に例外が発生（再試行可能）"),
        };

        var positions = BuildPositions();
        var executions = BuildExecutions();

        return new MockScenario(
            Key: "normal",
            DisplayName: "通常（候補あり）",
            Candidates: candidates,
            Exclusions: exclusions,
            Positions: positions,
            Executions: executions,
            UpdateProgress: new MockUpdateProgressSeed(Total: 3842, Completed: 3842, Failed: 7, Status: AnalysisRunStatus.PartiallySucceeded),
            AiQueueProgress: new MockAiQueueProgressSeed(Total: 13, Running: 1, Queued: 1, Completed: 11, Failed: 3),
            EmptyStateNote: null);
    }

    private static MockScenario BuildZeroCandidates()
    {
        var exclusions = new List<MockExclusionSeed>
        {
            new("—", "（内訳代表）履歴不足", TechnicalAnalysisOutcome.InsufficientHistory, null, 200, "必須本数未達の銘柄が多数"),
            new("—", "（内訳代表）履歴不完全", TechnicalAnalysisOutcome.HistoryIncomplete, null, 200, "日足欠損のある銘柄が多数"),
            new("—", "（内訳代表）要照合", TechnicalAnalysisOutcome.ReconciliationRequired, null, null, "未対応の企業アクションが多数"),
            new("—", "（内訳代表）point-in-time未検証", TechnicalAnalysisOutcome.PointInTimeUnverified, null, null, "配当落ち調整不可の銘柄が多数"),
        };

        // 保有ポジションはその日の候補抽出結果とは独立して存在するため、Normal と同じデータを再利用する。
        var positions = BuildPositions();
        var executions = BuildExecutions();

        return new MockScenario(
            Key: "zero",
            DisplayName: "候補ゼロ（EMA200 fail-closed 等）",
            Candidates: Array.Empty<MockCandidateSeed>(),
            Exclusions: exclusions,
            Positions: positions,
            Executions: executions,
            UpdateProgress: new MockUpdateProgressSeed(Total: 3842, Completed: 3842, Failed: 5, Status: AnalysisRunStatus.PartiallySucceeded),
            AiQueueProgress: new MockAiQueueProgressSeed(Total: 0, Running: 0, Queued: 0, Completed: 0, Failed: 0),
            EmptyStateNote: "条件一致 0件 / スキャン対象 3,842銘柄中 3,842銘柄が対象外です"
                + "（内訳: 履歴不足 2,150 / 履歴不完全 640 / データ不正 12 / 要照合 38 / 判定失敗 5 / point-in-time未検証 997）。"
                + "相場の安全性を示すものではありません。");
    }

    private static IReadOnlyList<MockPositionSeed> BuildPositions() => new List<MockPositionSeed>
    {
        new(
            Code: "7203", Name: "トヨタ自動車", Side: PositionSide.Long, Quantity: 500,
            AppliedStrategy: "標準スイングLong v1",
            Decision: ExitDecision.Hold, DecisionReason: "終値はEMA20上、MACDデッドクロスなし",
            EvaluationBarDate: EvaluationBarDate,
            FixedAtr: 48.5m, AtrReferenceBarDate: new DateOnly(2026, 7, 14), AtrPeriod: 14, StopMultiplier: 3.0m,
            CurrentAtrReferenceOnly: 55.2m,
            EntryBasisPrice: 2848.0m, StopPrice: 2702.5m, TakeProfitPrice: 3066.3m, CurrentPrice: 3022.6m,
            PartialExitStatus: PartialExitStatus.NotApplicable, PartialExitQuantity: null, PartialExitEffectiveFraction: null,
            PartialExitNote: "1.2R到達（1.5R未達）",
            ReconciliationStatus: ReconciliationStatus.Clear,
            CorporateActionNote: "2026-06-30 1:5分割 適用済み（株数×5、取得単価・固定ATR・価格ラインは÷5換算）",
            TermType: MarginTermType.FixedDate, FinalRepaymentDate: new DateOnly(2027, 1, 14), RemainingBusinessDays: 86,
            DeadlineChangeNote: null,
            CostLines: new[]
            {
                new MockCostLineSeed(MarginCostType.BuyerInterest, "買方金利", CostDirection.Charge, CostValuationKind.Confirmed, AmountStatus.KnownAmount, 1240m),
                new MockCostLineSeed(MarginCostType.Backwardation, "逆日歩", CostDirection.Credit, CostValuationKind.Confirmed, AmountStatus.NotOccurred, null),
            },
            PriceProfitAndLoss: 87300m, ConfirmedCostProfitAndLoss: 86060m, EstimatedNetProfitAndLoss: 86060m, CostToRRatio: 0.02m),

        new(
            Code: "6920", Name: "レーザーテック", Side: PositionSide.Short, Quantity: 300,
            AppliedStrategy: "標準スイングShort v1",
            Decision: ExitDecision.Hold, DecisionReason: "1.5R到達も反転条件未成立のためHold",
            EvaluationBarDate: EvaluationBarDate,
            FixedAtr: 310m, AtrReferenceBarDate: new DateOnly(2026, 8, 11), AtrPeriod: 14, StopMultiplier: 2.5m,
            CurrentAtrReferenceOnly: 340m,
            EntryBasisPrice: 22150m, StopPrice: 22925m, TakeProfitPrice: 20987.5m, CurrentPrice: 20987.5m,
            PartialExitStatus: PartialExitStatus.Candidate, PartialExitQuantity: 100, PartialExitEffectiveFraction: 0.333m,
            PartialExitNote: "建値候補（コスト未調整）— 手数料・金利・貸株料・逆日歩・配当相当額・スリッページを含む損益ゼロを保証しません",
            ReconciliationStatus: ReconciliationStatus.Clear,
            CorporateActionNote: null,
            TermType: MarginTermType.FixedDate, FinalRepaymentDate: new DateOnly(2027, 2, 11), RemainingBusinessDays: 120,
            DeadlineChangeNote: null,
            CostLines: new[]
            {
                new MockCostLineSeed(MarginCostType.StockLendingFee, "貸株料", CostDirection.Charge, CostValuationKind.Estimate, AmountStatus.KnownAmount, 3400m),
                new MockCostLineSeed(MarginCostType.Backwardation, "逆日歩", CostDirection.Charge, CostValuationKind.Confirmed, AmountStatus.Unpublished, null),
            },
            PriceProfitAndLoss: 348750m, ConfirmedCostProfitAndLoss: 348750m, EstimatedNetProfitAndLoss: 345350m, CostToRRatio: 0.31m),

        new(
            Code: "8035", Name: "東京エレクトロン", Side: PositionSide.Long, Quantity: 100,
            AppliedStrategy: "標準スイングLong v1",
            Decision: ExitDecision.TakeProfit, DecisionReason: "1.5R到達かつEMA20割れ",
            EvaluationBarDate: EvaluationBarDate,
            FixedAtr: 820m, AtrReferenceBarDate: new DateOnly(2026, 8, 5), AtrPeriod: 14, StopMultiplier: 3.0m,
            CurrentAtrReferenceOnly: 790m,
            EntryBasisPrice: 41200m, StopPrice: 38740m, TakeProfitPrice: 44890m, CurrentPrice: 44890m,
            PartialExitStatus: PartialExitStatus.NotFeasible, PartialExitQuantity: null, PartialExitEffectiveFraction: null,
            PartialExitNote: "売買単位100株、50%=50株 < 1単位のため分割不能",
            ReconciliationStatus: ReconciliationStatus.Clear,
            CorporateActionNote: null,
            TermType: MarginTermType.FixedDate, FinalRepaymentDate: new DateOnly(2026, 9, 4), RemainingBusinessDays: 7,
            DeadlineChangeNote: null,
            CostLines: new[]
            {
                new MockCostLineSeed(MarginCostType.BuyerInterest, "買方金利", CostDirection.Charge, CostValuationKind.Confirmed, AmountStatus.KnownAmount, 610m),
            },
            PriceProfitAndLoss: 369000m, ConfirmedCostProfitAndLoss: 368390m, EstimatedNetProfitAndLoss: 368390m, CostToRRatio: 0.025m),

        new(
            Code: "1605", Name: "INPEX", Side: PositionSide.Long, Quantity: 200,
            AppliedStrategy: "標準スイングLong v1",
            Decision: null, DecisionReason: "要照合のため自動再評価を停止",
            EvaluationBarDate: EvaluationBarDate,
            FixedAtr: 62m, AtrReferenceBarDate: new DateOnly(2026, 5, 20), AtrPeriod: 14, StopMultiplier: 3.0m,
            CurrentAtrReferenceOnly: null,
            EntryBasisPrice: null, StopPrice: 1980m, TakeProfitPrice: 2230m, CurrentPrice: null,
            PartialExitStatus: PartialExitStatus.NotApplicable, PartialExitQuantity: null, PartialExitEffectiveFraction: null,
            PartialExitNote: null,
            ReconciliationStatus: ReconciliationStatus.Required,
            CorporateActionNote: "株式交換（未対応イベント）を検出。現在基準への換算は未適用",
            TermType: MarginTermType.Unknown, FinalRepaymentDate: null, RemainingBusinessDays: null,
            DeadlineChangeNote: null,
            CostLines: new[]
            {
                new MockCostLineSeed(MarginCostType.Backwardation, "逆日歩", CostDirection.Credit, CostValuationKind.Confirmed, AmountStatus.FetchFailed, null),
            },
            PriceProfitAndLoss: null, ConfirmedCostProfitAndLoss: null, EstimatedNetProfitAndLoss: null, CostToRRatio: null),

        new(
            Code: "9101", Name: "日本郵船", Side: PositionSide.Short, Quantity: 400,
            AppliedStrategy: "標準スイングShort v1",
            Decision: ExitDecision.StopLoss, DecisionReason: "終値がATR損切ラインを上抜け",
            EvaluationBarDate: EvaluationBarDate,
            FixedAtr: 95m, AtrReferenceBarDate: new DateOnly(2026, 6, 2), AtrPeriod: 14, StopMultiplier: 2.5m,
            CurrentAtrReferenceOnly: 101m,
            EntryBasisPrice: 4180m, StopPrice: 4417.5m, TakeProfitPrice: 3823.75m, CurrentPrice: 4417.5m,
            PartialExitStatus: PartialExitStatus.NotApplicable, PartialExitQuantity: null, PartialExitEffectiveFraction: null,
            PartialExitNote: null,
            ReconciliationStatus: ReconciliationStatus.Clear,
            CorporateActionNote: "権利日跨ぎ警告: 配当金相当額の支払いが発生する見込み",
            TermType: MarginTermType.FixedDate, FinalRepaymentDate: new DateOnly(2026, 10, 16), RemainingBusinessDays: 36,
            DeadlineChangeNote: "証券会社確認により期限変更（2026-10-30 → 2026-10-16、確認revision 2）",
            CostLines: new[]
            {
                new MockCostLineSeed(MarginCostType.Backwardation, "逆日歩", CostDirection.Charge, CostValuationKind.Confirmed, AmountStatus.KnownAmount, 12800m),
                new MockCostLineSeed(MarginCostType.DividendEquivalent, "配当金相当額", CostDirection.Charge, CostValuationKind.Estimate, AmountStatus.KnownAmount, 8500m),
            },
            PriceProfitAndLoss: -95000m, ConfirmedCostProfitAndLoss: -107800m, EstimatedNetProfitAndLoss: -116300m, CostToRRatio: 0.62m),

        new(
            Code: "4385", Name: "メルカリ", Side: PositionSide.Short, Quantity: 100,
            AppliedStrategy: "標準スイングShort v1",
            Decision: ExitDecision.Exit, DecisionReason: "MACDゴールデンクロス成立（Short反転条件）",
            EvaluationBarDate: EvaluationBarDate,
            FixedAtr: 48m, AtrReferenceBarDate: new DateOnly(2026, 2, 20), AtrPeriod: 14, StopMultiplier: 2.5m,
            CurrentAtrReferenceOnly: 52m,
            EntryBasisPrice: 2310m, StopPrice: 2430m, TakeProfitPrice: 2130m, CurrentPrice: 2130m,
            PartialExitStatus: PartialExitStatus.NotApplicable, PartialExitQuantity: null, PartialExitEffectiveFraction: null,
            PartialExitNote: null,
            ReconciliationStatus: ReconciliationStatus.Clear,
            CorporateActionNote: null,
            TermType: MarginTermType.FixedDate, FinalRepaymentDate: new DateOnly(2026, 8, 21), RemainingBusinessDays: -2,
            DeadlineChangeNote: null,
            CostLines: new[]
            {
                new MockCostLineSeed(MarginCostType.StockLendingFee, "貸株料", CostDirection.Charge, CostValuationKind.Confirmed, AmountStatus.KnownAmount, 5100m),
                new MockCostLineSeed(MarginCostType.Backwardation, "逆日歩", CostDirection.Charge, CostValuationKind.Confirmed, AmountStatus.KnownZero, 0m),
            },
            PriceProfitAndLoss: 18000m, ConfirmedCostProfitAndLoss: 12900m, EstimatedNetProfitAndLoss: 12900m, CostToRRatio: 0.28m),
    };

    private static IReadOnlyList<MockExecutionSeed> BuildExecutions() => new List<MockExecutionSeed>
    {
        new("7203", "トヨタ自動車", PositionSide.Long, ExecutionKind.Open,
            new[]
            {
                new MockExecutionRevisionSeed(1, ExecutionChangeKind.Initial,
                    new DateTimeOffset(2026, 7, 14, 0, 12, 0, TimeSpan.Zero), 2845m, 500,
                    new DateTimeOffset(2026, 7, 14, 0, 15, 0, TimeSpan.Zero), null),
                new MockExecutionRevisionSeed(2, ExecutionChangeKind.Correction,
                    new DateTimeOffset(2026, 7, 14, 0, 12, 0, TimeSpan.Zero), 2848m, 500,
                    new DateTimeOffset(2026, 7, 16, 1, 0, 0, TimeSpan.Zero), "証券会社の約定明細と照合し価格を訂正（2,845→2,848）"),
            },
            null),

        new("8035", "東京エレクトロン", PositionSide.Long, ExecutionKind.Open,
            new[]
            {
                new MockExecutionRevisionSeed(1, ExecutionChangeKind.Initial,
                    new DateTimeOffset(2026, 8, 5, 1, 3, 0, TimeSpan.Zero), 41200m, 100,
                    new DateTimeOffset(2026, 8, 5, 1, 5, 0, TimeSpan.Zero), null),
            },
            null),

        new("6920", "レーザーテック", PositionSide.Short, ExecutionKind.Open,
            new[]
            {
                new MockExecutionRevisionSeed(1, ExecutionChangeKind.Initial,
                    new DateTimeOffset(2026, 8, 11, 4, 40, 0, TimeSpan.Zero), 22150m, 300,
                    new DateTimeOffset(2026, 8, 11, 4, 42, 0, TimeSpan.Zero), null),
            },
            null),

        new("6920", "レーザーテック", PositionSide.Short, ExecutionKind.Close,
            new[]
            {
                new MockExecutionRevisionSeed(1, ExecutionChangeKind.Initial,
                    new DateTimeOffset(2026, 8, 22, 5, 5, 0, TimeSpan.Zero), 20980m, 100,
                    new DateTimeOffset(2026, 8, 22, 5, 7, 0, TimeSpan.Zero), null),
            },
            "充当lot L-0007（利用者が明示選択。FIFO等による推測は行いません）"),

        new("8801", "三井不動産", PositionSide.Short, ExecutionKind.Open,
            new[]
            {
                new MockExecutionRevisionSeed(1, ExecutionChangeKind.Initial,
                    new DateTimeOffset(2026, 6, 2, 1, 20, 0, TimeSpan.Zero), 4180m, 400,
                    new DateTimeOffset(2026, 6, 2, 1, 22, 0, TimeSpan.Zero), null),
                new MockExecutionRevisionSeed(2, ExecutionChangeKind.Void,
                    new DateTimeOffset(2026, 6, 2, 1, 20, 0, TimeSpan.Zero), 4180m, 400,
                    new DateTimeOffset(2026, 6, 3, 0, 30, 0, TimeSpan.Zero), "誤登録のため取消（原票は監査用に保持。この銘柄は保有ポジションではありません）"),
            },
            null),

        new("4385", "メルカリ", PositionSide.Short, ExecutionKind.Open,
            new[]
            {
                new MockExecutionRevisionSeed(1, ExecutionChangeKind.Initial,
                    new DateTimeOffset(2026, 2, 20, 0, 5, 0, TimeSpan.Zero), 2310m, 100,
                    new DateTimeOffset(2026, 2, 20, 0, 7, 0, TimeSpan.Zero), null),
            },
            null),
    };
}
