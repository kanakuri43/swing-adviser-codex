namespace SwingAdviser.Domain.Common;

public readonly record struct InstrumentId(Guid Value)
{
    public static InstrumentId New() => new(Guid.NewGuid());
}

public readonly record struct AnalysisRunId(Guid Value)
{
    public static AnalysisRunId New() => new(Guid.NewGuid());
}

public readonly record struct CandidateResultId(Guid Value)
{
    public static CandidateResultId New() => new(Guid.NewGuid());
}

public readonly record struct PositionId(Guid Value)
{
    public static PositionId New() => new(Guid.NewGuid());
}

public readonly record struct TradeExecutionId(Guid Value)
{
    public static TradeExecutionId New() => new(Guid.NewGuid());
}

public readonly record struct TradeExecutionRevisionId(Guid Value)
{
    public static TradeExecutionRevisionId New() => new(Guid.NewGuid());
}

public readonly record struct MarginLotId(Guid Value)
{
    public static MarginLotId New() => new(Guid.NewGuid());
}

public readonly record struct MarginCostItemId(Guid Value)
{
    public static MarginCostItemId New() => new(Guid.NewGuid());
}

public readonly record struct MarginCostObservationId(Guid Value)
{
    public static MarginCostObservationId New() => new(Guid.NewGuid());
}

public readonly record struct AiCheckJobId(Guid Value)
{
    public static AiCheckJobId New() => new(Guid.NewGuid());
}

public readonly record struct AiAttemptId(Guid Value)
{
    public static AiAttemptId New() => new(Guid.NewGuid());
}
