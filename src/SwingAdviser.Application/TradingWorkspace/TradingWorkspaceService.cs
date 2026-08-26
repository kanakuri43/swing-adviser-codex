using SwingAdviser.Domain.Common;

namespace SwingAdviser.Application.TradingWorkspace;

public sealed class TradingWorkspaceService(ITradingWorkspaceRepository repository)
{
    public Task<TradingWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default) =>
        repository.LoadAsync(cancellationToken);

    public Task<ManualExecutionResult> RegisterManualExecutionAsync(
        RegisterManualExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommon(
            request.ExecutedAtUtc,
            request.Price,
            request.Quantity,
            request.Currency,
            request.UserConfirmedAtUtc,
            request.IsUserConfirmed);

        if (request.InstrumentId == Guid.Empty)
        {
            throw new ArgumentException("Instrument is required.", nameof(request));
        }

        if (request.Kind == ExecutionKind.Open)
        {
            if (request.PositionId is not null || request.LotAllocations.Count != 0)
            {
                throw new ArgumentException("An opening execution creates a new position and cannot allocate existing lots.", nameof(request));
            }
        }
        else
        {
            if (request.PositionId is null || request.PositionId == Guid.Empty)
            {
                throw new ArgumentException("A closing execution requires an explicitly selected position.", nameof(request));
            }

            if (request.LotAllocations.Count == 0 ||
                request.LotAllocations.Any(x => x.MarginLotId == Guid.Empty || x.Quantity <= 0) ||
                request.LotAllocations.Select(x => x.MarginLotId).Distinct().Count() != request.LotAllocations.Count ||
                request.LotAllocations.Sum(x => x.Quantity) != request.Quantity)
            {
                throw new ArgumentException("Closing quantity must be explicitly and exactly allocated to distinct lots.", nameof(request));
            }

            if (request.CandidateContextId is not null)
            {
                throw new ArgumentException("An entry candidate cannot be attached to a closing execution.", nameof(request));
            }
        }

        return repository.RegisterManualExecutionAsync(request, cancellationToken);
    }

    public Task<ManualExecutionResult> CorrectManualExecutionAsync(
        CorrectManualExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommon(
            request.ExecutedAtUtc,
            request.Price,
            request.Quantity,
            request.Currency,
            request.UserConfirmedAtUtc,
            request.IsUserConfirmed);

        if (request.ExecutionId == Guid.Empty || request.ExpectedCurrentRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Execution and expected revision are required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CorrectionReason))
        {
            throw new ArgumentException("A correction reason is required.", nameof(request));
        }

        return repository.CorrectManualExecutionAsync(request, cancellationToken);
    }

    private static void ValidateCommon(
        DateTimeOffset executedAtUtc,
        decimal price,
        long quantity,
        string currency,
        DateTimeOffset userConfirmedAtUtc,
        bool isUserConfirmed)
    {
        if (!isUserConfirmed)
        {
            throw new InvalidOperationException("Only an explicitly user-confirmed execution can be saved.");
        }

        if (executedAtUtc.Offset != TimeSpan.Zero || userConfirmedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Execution timestamps must be UTC.");
        }

        if (price <= 0 || quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price and quantity must be positive.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3 || currency != currency.ToUpperInvariant())
        {
            throw new ArgumentException("Currency must be a three-letter uppercase code.", nameof(currency));
        }
    }
}
