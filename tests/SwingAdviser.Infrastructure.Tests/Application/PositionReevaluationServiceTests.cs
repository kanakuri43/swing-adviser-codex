using SwingAdviser.Application.Positions;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Infrastructure.Tests.Application;

public sealed class PositionReevaluationServiceTests
{
    [Fact]
    public async Task ReevaluateAsync_OrdersOpenPositionsDeterministically_AndAggregatesAllSuccess()
    {
        var runId = new AnalysisRunId(Guid.Parse("00000000-0000-0000-0000-000000000100"));
        var earlierInstrument = new InstrumentId(Guid.Parse("00000000-0000-0000-0000-000000000010"));
        var laterInstrument = new InstrumentId(Guid.Parse("00000000-0000-0000-0000-000000000020"));
        var firstPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var repository = new RecordingRepository(
            [
                new OpenPositionReevaluationTarget(secondPosition, laterInstrument),
                new OpenPositionReevaluationTarget(firstPosition, earlierInstrument),
            ]);
        var progress = new RecordingProgress();
        var service = new PositionReevaluationService(repository);

        var summary = await service.ReevaluateAsync(
            new PositionReevaluationRequest(runId),
            progress);

        Assert.Equal([firstPosition, secondPosition], repository.EvaluatedPositionIds);
        Assert.Equal([firstPosition, secondPosition], summary.Items.Select(item => item.Target.PositionId));
        Assert.All(summary.Items, item =>
        {
            Assert.Equal(PositionReevaluationItemStatus.Completed, item.Status);
            Assert.Equal(PositionEvaluationOutcome.Evaluated, item.Outcome);
            Assert.Null(item.FailureReason);
        });
        Assert.Equal(2, summary.TotalPositionCount);
        Assert.Equal(2, summary.CompletedPositionCount);
        Assert.Equal(0, summary.FailedPositionCount);
        Assert.Equal(AnalysisRunStatus.Succeeded, summary.SuggestedRunStatus);
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
        Assert.Equal([1, 2], progress.Values.Select(value => value.ProcessedPositionCount));
        Assert.All(progress.Values, value => Assert.Equal(2, value.TotalPositionCount));
    }

    [Fact]
    public async Task ReevaluateAsync_ContinuesAfterOnePositionFails_AndReportsPartialSuccess()
    {
        var runId = AnalysisRunId.New();
        var failedPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var succeedingPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var repository = new RecordingRepository(
            [
                Target(succeedingPosition, "00000000-0000-0000-0000-000000000002"),
                Target(failedPosition, "00000000-0000-0000-0000-000000000001"),
            ],
            failures: new HashSet<PositionId> { failedPosition });

        var summary = await new PositionReevaluationService(repository)
            .ReevaluateAsync(new PositionReevaluationRequest(runId));

        Assert.Equal([failedPosition, succeedingPosition], repository.EvaluatedPositionIds);
        Assert.Equal(1, summary.CompletedPositionCount);
        Assert.Equal(1, summary.FailedPositionCount);
        Assert.Equal(AnalysisRunStatus.PartiallySucceeded, summary.SuggestedRunStatus);
        var failed = summary.Items.Single(item => item.Target.PositionId == failedPosition);
        Assert.Equal(PositionReevaluationItemStatus.Failed, failed.Status);
        Assert.Null(failed.Outcome);
        Assert.Contains("POSITION_REEVALUATION_FAILED: InvalidOperationException", failed.FailureReason, StringComparison.Ordinal);
        Assert.Equal(PositionReevaluationItemStatus.Completed,
            summary.Items.Single(item => item.Target.PositionId == succeedingPosition).Status);
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
    }

    [Fact]
    public async Task ReevaluateAsync_ReportsFailedWhenEveryPositionFails()
    {
        var runId = AnalysisRunId.New();
        var firstPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var repository = new RecordingRepository(
            [
                Target(firstPosition, "00000000-0000-0000-0000-000000000001"),
                Target(secondPosition, "00000000-0000-0000-0000-000000000002"),
            ],
            failures: new HashSet<PositionId> { firstPosition, secondPosition });

        var summary = await new PositionReevaluationService(repository)
            .ReevaluateAsync(new PositionReevaluationRequest(runId));

        Assert.Equal(0, summary.CompletedPositionCount);
        Assert.Equal(2, summary.FailedPositionCount);
        Assert.Equal(AnalysisRunStatus.Failed, summary.SuggestedRunStatus);
        Assert.All(summary.Items, item => Assert.Equal(PositionReevaluationItemStatus.Failed, item.Status));
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
    }

    [Fact]
    public async Task ReevaluateAsync_EvaluatesDistinctPositionsOfTheSameInstrumentIndependently()
    {
        var runId = AnalysisRunId.New();
        var instrumentId = new InstrumentId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var firstPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000010"));
        var secondPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000020"));
        var repository = new RecordingRepository(
            [
                new OpenPositionReevaluationTarget(secondPosition, instrumentId),
                new OpenPositionReevaluationTarget(firstPosition, instrumentId),
            ]);

        var summary = await new PositionReevaluationService(repository)
            .ReevaluateAsync(new PositionReevaluationRequest(runId));

        Assert.Equal([firstPosition, secondPosition], repository.EvaluatedPositionIds);
        Assert.Equal(2, summary.CompletedPositionCount);
        Assert.Equal(AnalysisRunStatus.Succeeded, summary.SuggestedRunStatus);
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
    }

    [Fact]
    public async Task ReevaluateAsync_PropagatesCancellationWithoutStartingTheNextPosition()
    {
        var runId = AnalysisRunId.New();
        var firstPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondPosition = new PositionId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        using var cancellation = new CancellationTokenSource();
        var repository = new RecordingRepository(
            [
                Target(firstPosition, "00000000-0000-0000-0000-000000000001"),
                Target(secondPosition, "00000000-0000-0000-0000-000000000002"),
            ],
            afterEvaluation: _ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PositionReevaluationService(repository).ReevaluateAsync(
                new PositionReevaluationRequest(runId),
                cancellationToken: cancellation.Token));

        Assert.Equal([firstPosition], repository.EvaluatedPositionIds);
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
    }

    [Fact]
    public async Task ReevaluateAsync_RejectsDuplicatePositionTargetsBeforeWritingAnything()
    {
        var position = PositionId.New();
        var repository = new RecordingRepository(
            [
                new OpenPositionReevaluationTarget(position, InstrumentId.New()),
                new OpenPositionReevaluationTarget(position, InstrumentId.New()),
            ]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new PositionReevaluationService(repository).ReevaluateAsync(
                new PositionReevaluationRequest(AnalysisRunId.New())));

        Assert.Empty(repository.EvaluatedPositionIds);
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
    }

    [Fact]
    public async Task ReevaluateAsync_DoesNotLetAProgressObserverInvalidateCompletedEvaluations()
    {
        var position = PositionId.New();
        var repository = new RecordingRepository(
            [new OpenPositionReevaluationTarget(position, InstrumentId.New())]);

        var summary = await new PositionReevaluationService(repository).ReevaluateAsync(
            new PositionReevaluationRequest(AnalysisRunId.New()),
            new ThrowingProgress());

        Assert.Equal(PositionReevaluationItemStatus.Completed, Assert.Single(summary.Items).Status);
        Assert.Equal([position], repository.EvaluatedPositionIds);
        Assert.Equal(0, repository.GeneratedTradeExecutionCount);
    }

    private static OpenPositionReevaluationTarget Target(PositionId positionId, string instrumentId) =>
        new(positionId, new InstrumentId(Guid.Parse(instrumentId)));

    private sealed class RecordingRepository : IPositionReevaluationRepository
    {
        private readonly IReadOnlyList<OpenPositionReevaluationTarget> targets;
        private readonly IReadOnlySet<PositionId> failures;
        private readonly Action<PositionId>? afterEvaluation;

        public RecordingRepository(
            IReadOnlyList<OpenPositionReevaluationTarget> targets,
            IReadOnlySet<PositionId>? failures = null,
            Action<PositionId>? afterEvaluation = null)
        {
            this.targets = targets;
            this.failures = failures ?? new HashSet<PositionId>();
            this.afterEvaluation = afterEvaluation;
        }

        public List<PositionId> EvaluatedPositionIds { get; } = [];
        public int GeneratedTradeExecutionCount { get; private set; }

        public Task<IReadOnlyList<OpenPositionReevaluationTarget>> ListOpenPositionsAsync(
            AnalysisRunId analysisRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(targets);

        public Task<PositionEvaluationOutcome> EvaluateAndPersistAsync(
            AnalysisRunId analysisRunId,
            PositionId positionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluatedPositionIds.Add(positionId);
            afterEvaluation?.Invoke(positionId);
            if (failures.Contains(positionId))
            {
                throw new InvalidOperationException("fixture failure");
            }

            return Task.FromResult(PositionEvaluationOutcome.Evaluated);
        }
    }

    private sealed class RecordingProgress : IProgress<PositionReevaluationProgress>
    {
        public List<PositionReevaluationProgress> Values { get; } = [];

        public void Report(PositionReevaluationProgress value) => Values.Add(value);
    }

    private sealed class ThrowingProgress : IProgress<PositionReevaluationProgress>
    {
        public void Report(PositionReevaluationProgress value) =>
            throw new InvalidOperationException("observer failure");
    }
}
