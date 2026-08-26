using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Presentation.TradingWorkspace;

public sealed class ManualExecutionDialogRequestEventArgs : EventArgs
{
    private ManualExecutionDialogRequestEventArgs(
        Guid instrumentId,
        Guid? candidateId,
        Guid? positionId,
        string code,
        string name,
        PositionSide side,
        ExecutionKind kind,
        IReadOnlyList<MarginLotListItem> lots,
        TradeExecutionListItem? correctionTarget)
    {
        InstrumentId = instrumentId;
        CandidateId = candidateId;
        PositionId = positionId;
        Code = code;
        Name = name;
        Side = side;
        Kind = kind;
        Lots = lots;
        CorrectionTarget = correctionTarget;
    }

    public Guid InstrumentId { get; }
    public Guid? CandidateId { get; }
    public Guid? PositionId { get; }
    public string Code { get; }
    public string Name { get; }
    public PositionSide Side { get; }
    public ExecutionKind Kind { get; }
    public IReadOnlyList<MarginLotListItem> Lots { get; }
    public TradeExecutionListItem? CorrectionTarget { get; }

    public static ManualExecutionDialogRequestEventArgs ForCandidate(CandidateListItem item) => new(
        item.InstrumentId, item.CandidateId, null, item.Code, item.Name, item.Side, ExecutionKind.Open, [], null);

    public static ManualExecutionDialogRequestEventArgs ForPosition(PositionListItem item) => new(
        item.InstrumentId, null, item.PositionId, item.Code, item.Name, item.Side, ExecutionKind.Close, item.Lots, null);

    public static ManualExecutionDialogRequestEventArgs ForCorrection(TradeExecutionListItem item) => new(
        item.InstrumentId, null, item.PositionId, item.Code, item.Name, item.Side, item.Kind, [], item);
}
