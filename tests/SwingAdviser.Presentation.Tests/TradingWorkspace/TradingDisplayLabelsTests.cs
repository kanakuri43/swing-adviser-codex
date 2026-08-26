using SwingAdviser.Domain.Common;
using SwingAdviser.Presentation.TradingWorkspace;

namespace SwingAdviser.Presentation.Tests.TradingWorkspace;

public sealed class TradingDisplayLabelsTests
{
    public static TheoryData<AmountStatus> MissingOrNonOccurrenceStatuses => new()
    {
        AmountStatus.Unpublished, AmountStatus.FetchFailed, AmountStatus.Unknown, AmountStatus.NotOccurred,
    };

    [Theory]
    [MemberData(nameof(MissingOrNonOccurrenceStatuses))]
    public void CostAmountLabel_DoesNotPresentMissingOrNonOccurrenceAsZero(AmountStatus status)
    {
        var label = TradingDisplayLabels.CostAmountLabel(status, null, CostValuationKind.Confirmed);
        Assert.DoesNotContain("¥0", label, StringComparison.Ordinal);
    }

    [Fact]
    public void CostAmountLabel_PresentsOnlyKnownZeroAsConfirmedZero()
    {
        Assert.Equal("¥0（確定）", TradingDisplayLabels.CostAmountLabel(AmountStatus.KnownZero, 0m, CostValuationKind.Confirmed));
    }

    [Theory]
    [InlineData(PositionSide.Long, AiVerdict.Bullish)]
    [InlineData(PositionSide.Long, AiVerdict.Neutral)]
    [InlineData(PositionSide.Long, AiVerdict.Bearish)]
    [InlineData(PositionSide.Long, null)]
    [InlineData(PositionSide.Short, AiVerdict.Bullish)]
    [InlineData(PositionSide.Short, AiVerdict.Neutral)]
    [InlineData(PositionSide.Short, AiVerdict.Bearish)]
    [InlineData(PositionSide.Short, null)]
    public void AiVerdictAlignmentLabel_NeverReturnsBareDirectionalVerdict(PositionSide side, AiVerdict? verdict)
    {
        var label = TradingDisplayLabels.AiVerdictAlignmentLabel(side, verdict);
        Assert.NotEqual("強気", label);
        Assert.NotEqual("弱気", label);
        Assert.True(verdict is null || verdict == AiVerdict.Neutral || label.Contains(side.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void ShortAvailabilityLabel_UnknownContainsUnknownAndNotAvailabilityClaim()
    {
        var label = TradingDisplayLabels.ShortAvailabilityLabel(OpenPermissionStatus.Unknown);
        Assert.Contains("不明", label, StringComparison.Ordinal);
        Assert.DoesNotContain("可", label, StringComparison.Ordinal);
    }
}
