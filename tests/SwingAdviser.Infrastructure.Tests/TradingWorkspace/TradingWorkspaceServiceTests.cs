using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Infrastructure.Tests.TradingWorkspace;

public sealed class TradingWorkspaceServiceTests
{
    [Fact]
    public async Task Register_RejectsExecutionWithoutExplicitUserConfirmation()
    {
        var repository = new RecordingRepository();
        var service = new TradingWorkspaceService(repository);
        var request = ValidOpenRequest() with { IsUserConfirmed = false };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterManualExecutionAsync(request));

        Assert.False(repository.RegisterCalled);
    }

    [Fact]
    public async Task Register_CloseRequiresExactExplicitLotAllocation()
    {
        var repository = new RecordingRepository();
        var service = new TradingWorkspaceService(repository);
        var request = ValidOpenRequest() with
        {
            PositionId = Guid.NewGuid(),
            Kind = ExecutionKind.Close,
            Quantity = 100,
            LotAllocations = [new ManualLotAllocation(Guid.NewGuid(), 99)],
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegisterManualExecutionAsync(request));

        Assert.False(repository.RegisterCalled);
    }

    [Fact]
    public async Task Correct_RequiresReasonAndExpectedLeaf()
    {
        var repository = new RecordingRepository();
        var service = new TradingWorkspaceService(repository);
        var request = new CorrectManualExecutionRequest(
            Guid.NewGuid(),
            Guid.Empty,
            Utc(1),
            1000m,
            100,
            "JPY",
            Utc(2),
            true,
            string.Empty);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CorrectManualExecutionAsync(request));

        Assert.False(repository.CorrectCalled);
    }

    private static RegisterManualExecutionRequest ValidOpenRequest() => new(
        Guid.NewGuid(),
        null,
        null,
        PositionSide.Long,
        ExecutionKind.Open,
        Utc(1),
        1000m,
        100,
        "JPY",
        Utc(2),
        true,
        []);

    private static DateTimeOffset Utc(int hour) =>
        new(2026, 8, 26, hour, 0, 0, TimeSpan.Zero);

    private sealed class RecordingRepository : ITradingWorkspaceRepository
    {
        public bool RegisterCalled { get; private set; }
        public bool CorrectCalled { get; private set; }

        public Task<TradingWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TradingWorkspaceSnapshot([], [], [], DateTimeOffset.UtcNow));

        public Task<ManualExecutionResult> RegisterManualExecutionAsync(
            RegisterManualExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            RegisterCalled = true;
            return Task.FromResult(new ManualExecutionResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1));
        }

        public Task<ManualExecutionResult> CorrectManualExecutionAsync(
            CorrectManualExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            CorrectCalled = true;
            return Task.FromResult(new ManualExecutionResult(request.ExecutionId, Guid.NewGuid(), Guid.NewGuid(), 2));
        }
    }
}
