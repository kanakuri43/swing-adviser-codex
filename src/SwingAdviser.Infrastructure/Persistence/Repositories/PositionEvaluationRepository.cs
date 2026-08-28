using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Repositories;

internal sealed record PositionEvaluationLotEvidence(
    Guid MarginLotId,
    Guid? RiskBasisSnapshotId,
    Guid? RiskPlanRevisionId,
    PositionEvaluationOutcome Outcome,
    ExitDecision? Decision,
    PartialExitStatus PartialExitStatus,
    long? PartialExitQuantity,
    string EvidenceJson);

internal sealed record StoredPositionEvaluation(
    Guid ManifestId,
    PositionEvaluationProjection Projection,
    PositionEvaluation Evaluation,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<PositionEvaluationLotEvidence> LotEvaluations);

internal sealed class PositionEvaluationRepository(SwingAdviserDbContext dbContext)
{
    public const string ReasonsSchemaVersion = "position-evaluation-reasons-v1";
    public const string LotEvaluationsSchemaVersion = "position-evaluation-lot-evaluations-v1";

    public async Task<StoredPositionEvaluation> SaveAsync(
        PositionEvaluationProjection projection,
        PositionEvaluation evaluation,
        IReadOnlyCollection<string> reasonCodes,
        IReadOnlyCollection<PositionEvaluationLotEvidence> lotEvaluations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(reasonCodes);
        ArgumentNullException.ThrowIfNull(lotEvaluations);

        var normalizedReasons = NormalizeReasonCodes(reasonCodes);
        ValidateReasonCodes(projection, normalizedReasons);
        var normalizedLots = lotEvaluations.OrderBy(item => item.MarginLotId).ToArray();
        ValidateResult(projection, evaluation, normalizedLots);
        var reasonsJson = WriteReasonsJson(normalizedReasons);
        var lotEvaluationsJson = WriteLotEvaluationsJson(normalizedLots);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var verifiedProjection = await new PositionEvaluationProjectionRepository(dbContext)
            .BuildAsync(projection.Manifest.AnalysisRunId, projection.PositionId, cancellationToken);
        EnsureProjectionExact(projection, verifiedProjection);
        ValidateResult(verifiedProjection, evaluation, normalizedLots);
        await ValidateCostAggregatesAsync(verifiedProjection, evaluation, cancellationToken);
        await ValidatePartialExitAsync(verifiedProjection, normalizedLots, cancellationToken);

        var manifestExists = await dbContext.Set<PositionEvaluationInputManifestRow>()
            .AnyAsync(
                row => row.AnalysisRunId == projection.Manifest.AnalysisRunId &&
                       row.PositionId == projection.PositionId,
                cancellationToken);
        var evaluationExists = await dbContext.Set<PositionEvaluationRow>()
            .AnyAsync(
                row => row.AnalysisRunId == projection.Manifest.AnalysisRunId &&
                       row.PositionId == projection.PositionId,
                cancellationToken);
        if (manifestExists || evaluationExists)
        {
            throw new InvalidOperationException("A position evaluation already exists for this analysis run and position.");
        }

        var manifest = CreateManifestRow(projection.Manifest, evaluation.InputManifestId, evaluation.CreatedAtUtc);
        var result = CreateEvaluationRow(evaluation, reasonsJson, lotEvaluationsJson);
        dbContext.Add(manifest);
        dbContext.Add(result);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            dbContext.Entry(result).State = EntityState.Detached;
            dbContext.Entry(manifest).State = EntityState.Detached;
            throw;
        }

        return await ReadAsync(
            projection.Manifest.AnalysisRunId,
            projection.PositionId,
            cancellationToken);
    }

    public async Task<StoredPositionEvaluation> ReadAsync(
        Guid analysisRunId,
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        if (analysisRunId == Guid.Empty || positionId == Guid.Empty)
        {
            throw new ArgumentException("Analysis run and position IDs cannot be empty.");
        }

        var manifest = await dbContext.Set<PositionEvaluationInputManifestRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.AnalysisRunId == analysisRunId && row.PositionId == positionId,
                cancellationToken);
        var result = await dbContext.Set<PositionEvaluationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.AnalysisRunId == analysisRunId && row.PositionId == positionId,
                cancellationToken);
        if (manifest is null && result is null)
        {
            throw new KeyNotFoundException("The position evaluation does not exist.");
        }

        if (manifest is null ||
            result is null ||
            result.PositionEvaluationInputManifestId != manifest.Id ||
            result.CreatedAtUtc != manifest.CreatedAtUtc)
        {
            throw new InvalidDataException("The position evaluation manifest/result pair is incomplete or mismatched.");
        }

        var projection = await new PositionEvaluationProjectionRepository(dbContext)
            .BuildAsync(analysisRunId, positionId, cancellationToken);
        EnsureManifestExact(manifest, projection.Manifest);

        var evaluation = ParseEvaluation(result);
        var reasonCodes = ParseReasonsJson(result.ReasonsJson);
        var lotEvaluations = ParseLotEvaluationsJson(result.LotEvaluationsJson);
        ValidateReasonCodes(projection, reasonCodes);
        ValidateResult(projection, evaluation, lotEvaluations);
        await ValidateCostAggregatesAsync(projection, evaluation, cancellationToken);
        await ValidatePartialExitAsync(projection, lotEvaluations, cancellationToken);

        return new StoredPositionEvaluation(
            manifest.Id,
            projection,
            evaluation,
            reasonCodes,
            lotEvaluations);
    }

    private static PositionEvaluationInputManifestRow CreateManifestRow(
        PositionEvaluationManifestDraft draft,
        Guid id,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The position evaluation manifest ID cannot be empty.");
        }

        return new PositionEvaluationInputManifestRow
        {
            Id = id,
            AnalysisRunId = draft.AnalysisRunId,
            PositionId = draft.PositionId,
            AnalysisInputManifestId = draft.AnalysisInputManifestId,
            CurrentPriceRevisionId = draft.CurrentPriceRevisionId,
            TradeExecutionRevisionIdsJson = draft.TradeExecutionRevisionIdsJson,
            LotAllocationRevisionIdsJson = draft.LotAllocationRevisionIdsJson,
            PositionAdjustmentIdsJson = draft.PositionAdjustmentIdsJson,
            ContractRevisionIdsJson = draft.ContractRevisionIdsJson,
            RiskBasisSnapshotIdsJson = draft.RiskBasisSnapshotIdsJson,
            RiskPlanRevisionIdsJson = draft.RiskPlanRevisionIdsJson,
            MarginCostObservationIdsJson = draft.MarginCostObservationIdsJson,
            ProjectionVersion = draft.ProjectionVersion,
            RecordedCutoffAtUtc = draft.RecordedCutoffAtUtc,
            ManifestSha256 = draft.ManifestSha256,
            CreatedAtUtc = createdAtUtc,
        };
    }

    private static PositionEvaluationRow CreateEvaluationRow(
        PositionEvaluation evaluation,
        string reasonsJson,
        string lotEvaluationsJson) => new()
    {
        Id = evaluation.Id,
        AnalysisRunId = evaluation.AnalysisRunId.Value,
        PositionId = evaluation.PositionId.Value,
        PositionEvaluationInputManifestId = evaluation.InputManifestId,
        EvaluationBarDate = evaluation.EvaluationBarDate,
        EvaluationOutcome = evaluation.Outcome.ToString(),
        ExitDecision = evaluation.Decision?.ToString(),
        ReasonSummary = evaluation.ReasonSummary,
        ReasonsJson = reasonsJson,
        LotEvaluationsJson = lotEvaluationsJson,
        CurrentQuantity = evaluation.CurrentQuantity,
        PricePnl = evaluation.PriceProfitAndLoss,
        ConfirmedCostPnl = evaluation.ConfirmedCostProfitAndLoss,
        EstimatedNetPnl = evaluation.EstimatedNetProfitAndLoss,
        CostToRRatio = evaluation.CostToRRatio,
        PartialExitQuantity = evaluation.PartialExitQuantity,
        PartialExitStatus = evaluation.PartialExitStatus.ToString(),
        CreatedAtUtc = evaluation.CreatedAtUtc,
    };

    private static PositionEvaluation ParseEvaluation(PositionEvaluationRow row)
    {
        if (!Enum.TryParse<PositionEvaluationOutcome>(row.EvaluationOutcome, false, out var outcome) ||
            !Enum.IsDefined(outcome) ||
            outcome.ToString() != row.EvaluationOutcome)
        {
            throw new InvalidDataException("The stored position evaluation outcome is invalid.");
        }

        ExitDecision? decision = null;
        if (row.ExitDecision is not null)
        {
            if (!Enum.TryParse<ExitDecision>(row.ExitDecision, false, out var parsed) ||
                !Enum.IsDefined(parsed) ||
                parsed.ToString() != row.ExitDecision)
            {
                throw new InvalidDataException("The stored position evaluation decision is invalid.");
            }

            decision = parsed;
        }

        if (!Enum.TryParse<PartialExitStatus>(row.PartialExitStatus, false, out var partialStatus) ||
            !Enum.IsDefined(partialStatus) ||
            partialStatus.ToString() != row.PartialExitStatus)
        {
            throw new InvalidDataException("The stored partial-exit status is invalid.");
        }

        return new PositionEvaluation(
            row.Id,
            new AnalysisRunId(row.AnalysisRunId),
            new PositionId(row.PositionId),
            row.PositionEvaluationInputManifestId,
            row.EvaluationBarDate,
            outcome,
            decision,
            row.ReasonSummary,
            row.CurrentQuantity,
            row.PricePnl,
            row.ConfirmedCostPnl,
            row.EstimatedNetPnl,
            row.CostToRRatio,
            row.PartialExitQuantity,
            partialStatus,
            row.CreatedAtUtc);
    }

    private static void ValidateResult(
        PositionEvaluationProjection projection,
        PositionEvaluation evaluation,
        IReadOnlyList<PositionEvaluationLotEvidence> lotEvaluations)
    {
        if (evaluation.AnalysisRunId.Value != projection.Manifest.AnalysisRunId ||
            evaluation.PositionId.Value != projection.PositionId ||
            evaluation.EvaluationBarDate != projection.CurrentPrice.BarDate ||
            evaluation.InputManifestId == Guid.Empty ||
            evaluation.CreatedAtUtc < projection.Manifest.RecordedCutoffAtUtc)
        {
            throw new InvalidDataException("The evaluation identity, date, manifest, or creation time does not match the projection.");
        }

        var canonicalHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(projection.Manifest.CanonicalJson)))
            .ToLowerInvariant();
        if (!string.Equals(canonicalHash, projection.Manifest.ManifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The position evaluation manifest hash is invalid.");
        }

        var expectedFailure = projection.Status switch
        {
            PositionProjectionStatus.IncompletePositionData => PositionEvaluationOutcome.IncompletePositionData,
            PositionProjectionStatus.PointInTimeUnverified => PositionEvaluationOutcome.PointInTimeUnverified,
            PositionProjectionStatus.ReconciliationRequired => PositionEvaluationOutcome.ReconciliationRequired,
            _ => (PositionEvaluationOutcome?)null,
        };
        if (expectedFailure.HasValue && evaluation.Outcome != expectedFailure.Value)
        {
            throw new InvalidDataException("The evaluation outcome does not preserve the projection's fail-closed status.");
        }

        if (evaluation.Outcome != PositionEvaluationOutcome.Evaluated &&
            (evaluation.PriceProfitAndLoss.HasValue ||
             evaluation.ConfirmedCostProfitAndLoss.HasValue ||
             evaluation.EstimatedNetProfitAndLoss.HasValue ||
             evaluation.CostToRRatio.HasValue))
        {
            throw new InvalidDataException("A fail-closed evaluation cannot publish profit/loss aggregates.");
        }

        var activeLots = projection.Lots
            .Where(lot => lot.CurrentQuantity != 0m)
            .OrderBy(lot => lot.MarginLotId)
            .ToArray();
        if (lotEvaluations.Count != activeLots.Length ||
            lotEvaluations.Select(item => item.MarginLotId).Distinct().Count() != lotEvaluations.Count)
        {
            throw new InvalidDataException("The lot evaluation set does not match the active projected lots.");
        }

        for (var index = 0; index < activeLots.Length; index++)
        {
            var expected = activeLots[index];
            var actual = lotEvaluations[index];
            if (actual.MarginLotId != expected.MarginLotId ||
                actual.RiskBasisSnapshotId != expected.RiskBasisSnapshotId ||
                actual.RiskPlanRevisionId != expected.RiskPlanRevisionId)
            {
                throw new InvalidDataException("A lot evaluation does not reference the exact projected lot evidence.");
            }

            ValidateLotEvidence(actual);
        }

        if (projection.Lots.Count > 0 && projection.Lots.All(lot => lot.CurrentQuantity.HasValue))
        {
            var projectedQuantity = projection.Lots.Sum(lot => lot.CurrentQuantity!.Value);
            if (evaluation.CurrentQuantity != projectedQuantity)
            {
                throw new InvalidDataException("The evaluation quantity does not equal the projected lot total.");
            }
        }
        else if (evaluation.CurrentQuantity.HasValue)
        {
            throw new InvalidDataException("An unreliable projected quantity cannot be persisted as known.");
        }

        if (evaluation.Outcome == PositionEvaluationOutcome.Evaluated)
        {
            if (lotEvaluations.Count == 0 || lotEvaluations.Any(item => item.Outcome != PositionEvaluationOutcome.Evaluated))
            {
                throw new InvalidDataException("An evaluated position requires every active lot to be evaluated.");
            }

            var aggregate = lotEvaluations
                .Select(item => item.Decision!.Value)
                .OrderByDescending(DecisionPriority)
                .First();
            if (evaluation.Decision != aggregate)
            {
                throw new InvalidDataException("The position decision does not match the lot decision priority.");
            }

            ValidateCurrentBarDecisions(projection, lotEvaluations);
            ValidatePriceProfitAndLoss(projection, evaluation);
            ValidateAggregatePartialExit(evaluation, lotEvaluations);
        }
    }

    private static void ValidateCurrentBarDecisions(
        PositionEvaluationProjection projection,
        IReadOnlyList<PositionEvaluationLotEvidence> lotEvaluations)
    {
        var lotById = projection.Lots.ToDictionary(lot => lot.MarginLotId);
        foreach (var evidence in lotEvaluations)
        {
            var lot = lotById[evidence.MarginLotId];
            if (!lot.StopPrice.HasValue || !lot.TakeProfitPrice.HasValue)
            {
                throw new InvalidDataException("An evaluated lot requires exact stop and take-profit lines.");
            }

            var stopReached = projection.PositionSide switch
            {
                "Long" => projection.CurrentPrice.Low <= lot.StopPrice.Value,
                "Short" => projection.CurrentPrice.High >= lot.StopPrice.Value,
                _ => throw new InvalidDataException("The projected position side is invalid."),
            };
            var targetReached = projection.PositionSide switch
            {
                "Long" => projection.CurrentPrice.High >= lot.TakeProfitPrice.Value,
                "Short" => projection.CurrentPrice.Low <= lot.TakeProfitPrice.Value,
                _ => throw new InvalidDataException("The projected position side is invalid."),
            };
            if (stopReached && evidence.Decision != ExitDecision.StopLoss)
            {
                throw new InvalidDataException("A current-bar stop reach must retain StopLoss priority.");
            }

            if (!stopReached && targetReached && evidence.Decision == ExitDecision.Hold)
            {
                throw new InvalidDataException("A current-bar target reach cannot be persisted as Hold.");
            }
        }
    }

    private static void ValidatePriceProfitAndLoss(
        PositionEvaluationProjection projection,
        PositionEvaluation evaluation)
    {
        decimal expected = 0m;
        foreach (var lot in projection.Lots.Where(item => item.CurrentQuantity is > 0m))
        {
            if (!lot.EntryBasisPrice.HasValue)
            {
                throw new InvalidDataException("An evaluated lot requires an exact entry basis price.");
            }

            var priceChange = projection.PositionSide switch
            {
                "Long" => projection.CurrentPrice.Close - lot.EntryBasisPrice.Value,
                "Short" => lot.EntryBasisPrice.Value - projection.CurrentPrice.Close,
                _ => throw new InvalidDataException("The projected position side is invalid."),
            };
            expected = checked(expected + checked(priceChange * lot.CurrentQuantity!.Value));
        }

        if (evaluation.PriceProfitAndLoss != expected)
        {
            throw new InvalidDataException("The position price profit/loss does not match the exact projected lots.");
        }
    }

    private async Task ValidateCostAggregatesAsync(
        PositionEvaluationProjection projection,
        PositionEvaluation evaluation,
        CancellationToken cancellationToken)
    {
        if (evaluation.Outcome != PositionEvaluationOutcome.Evaluated)
        {
            return;
        }

        var activeLots = projection.Lots.Where(lot => lot.CurrentQuantity is > 0m).ToArray();
        var observationIds = activeLots.SelectMany(lot => lot.MarginCostObservationIds).Distinct().ToArray();
        var observations = await dbContext.Set<MarginCostObservationRow>()
            .AsNoTracking()
            .Where(row => observationIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        if (observations.Count != observationIds.Length)
        {
            throw new InvalidDataException("An exact margin-cost observation is missing.");
        }

        var itemIds = activeLots.SelectMany(lot => lot.MarginCostItemIds).Distinct().ToArray();
        var items = await dbContext.Set<MarginCostItemRow>()
            .AsNoTracking()
            .Where(row => itemIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        if (items.Count != itemIds.Length)
        {
            throw new InvalidDataException("An exact margin-cost item is missing.");
        }

        var itemById = items.ToDictionary(row => row.Id);
        var observationsByItem = observations.ToLookup(row => row.MarginCostItemId);
        var allConfirmedComplete = true;
        var allReferenceComplete = true;
        decimal confirmedNetCost = 0m;
        decimal referenceNetCost = 0m;
        decimal totalRisk = 0m;

        foreach (var lot in activeLots)
        {
            var lotItemIds = lot.MarginCostItemIds.ToArray();
            if (lotItemIds.Any(id => itemById[id].MarginLotId != lot.MarginLotId))
            {
                throw new InvalidDataException("A margin-cost item belongs to another projected lot.");
            }

            var lotConfirmedComplete = lotItemIds.Length > 0;
            var lotReferenceComplete = lotItemIds.Length > 0;
            decimal lotConfirmedCost = 0m;
            decimal lotReferenceCost = 0m;
            foreach (var itemId in lotItemIds)
            {
                var itemObservations = observationsByItem[itemId].ToArray();
                var confirmed = itemObservations.SingleOrDefault(row => row.ValuationKind == "Confirmed");
                var estimate = itemObservations.SingleOrDefault(row => row.ValuationKind == "Estimate");
                var confirmedAmount = ResolveNetCost(confirmed, lot.PriceCurrency);
                var referenceAmount = confirmedAmount.IsResolved
                    ? confirmedAmount
                    : ResolveNetCost(estimate, lot.PriceCurrency);
                if (confirmedAmount.IsResolved)
                {
                    lotConfirmedCost = checked(lotConfirmedCost + confirmedAmount.NetCost);
                }
                else
                {
                    lotConfirmedComplete = false;
                }

                if (referenceAmount.IsResolved)
                {
                    lotReferenceCost = checked(lotReferenceCost + referenceAmount.NetCost);
                }
                else
                {
                    lotReferenceComplete = false;
                }
            }

            if (lotConfirmedComplete)
            {
                confirmedNetCost = checked(confirmedNetCost + lotConfirmedCost);
            }
            else
            {
                allConfirmedComplete = false;
            }

            if (lotReferenceComplete)
            {
                referenceNetCost = checked(referenceNetCost + lotReferenceCost);
            }
            else
            {
                allReferenceComplete = false;
            }

            if (!lot.RiskAmountR.HasValue)
            {
                throw new InvalidDataException("An evaluated lot requires an exact 1R amount.");
            }
            totalRisk = checked(totalRisk + checked(lot.RiskAmountR.Value * lot.CurrentQuantity!.Value));
        }

        var pricePnl = evaluation.PriceProfitAndLoss!.Value;
        var expectedConfirmedPnl = allConfirmedComplete ? pricePnl - confirmedNetCost : (decimal?)null;
        var expectedEstimatedPnl = allReferenceComplete ? pricePnl - referenceNetCost : (decimal?)null;
        var expectedCostToR = allReferenceComplete ? referenceNetCost / totalRisk : (decimal?)null;
        if (evaluation.ConfirmedCostProfitAndLoss != expectedConfirmedPnl ||
            evaluation.EstimatedNetProfitAndLoss != expectedEstimatedPnl ||
            evaluation.CostToRRatio != expectedCostToR)
        {
            throw new InvalidDataException("The position cost aggregates do not match the exact lot cost observations.");
        }
    }

    private async Task ValidatePartialExitAsync(
        PositionEvaluationProjection projection,
        IReadOnlyList<PositionEvaluationLotEvidence> lotEvaluations,
        CancellationToken cancellationToken)
    {
        var takeProfitLots = lotEvaluations
            .Where(lot => lot.Outcome == PositionEvaluationOutcome.Evaluated &&
                          lot.Decision == ExitDecision.TakeProfit)
            .ToArray();
        if (takeProfitLots.Length == 0)
        {
            return;
        }

        var marketManifest = await dbContext.Set<AnalysisInputManifestRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == projection.Manifest.AnalysisInputManifestId, cancellationToken);
        var masterRows = await dbContext.Set<InstrumentMasterRevisionRow>()
            .AsNoTracking()
            .Where(row => row.InstrumentId == projection.InstrumentId &&
                          row.RecordedAtUtc <= projection.Manifest.RecordedCutoffAtUtc &&
                          row.EffectiveFromDate <= projection.CurrentPrice.BarDate &&
                          (row.EffectiveToDate == null || row.EffectiveToDate >= projection.CurrentPrice.BarDate))
            .ToListAsync(cancellationToken);
        var availableRows = masterRows.Where(row => marketManifest.SelectionBasis switch
        {
            "ObservedAt" => row.FirstObservedAtUtc <= marketManifest.SelectedAvailableCutoffAtUtc,
            "AvailableAt" => row.AvailableAtUtc is { } availableAt &&
                             availableAt <= marketManifest.SelectedAvailableCutoffAtUtc,
            _ => false,
        }).ToArray();
        var leaves = availableRows
            .GroupBy(row => row.Provider, StringComparer.Ordinal)
            .SelectMany(group =>
            {
                var superseded = group.Where(row => row.SupersedesId.HasValue)
                    .Select(row => row.SupersedesId!.Value)
                    .ToHashSet();
                return group.Where(row => !superseded.Contains(row.Id));
            })
            .Where(row => row.AvailabilityStatus == "Known" && row.ChangeKind != "Cancellation")
            .ToArray();
        var tradingUnit = leaves.Length == 1 ? leaves[0].TradingUnit : null;
        var projectedByLot = projection.Lots.ToDictionary(lot => lot.MarginLotId);
        foreach (var evidence in takeProfitLots)
        {
            var projected = projectedByLot[evidence.MarginLotId];
            if (tradingUnit is not > 0 ||
                projected.CurrentQuantity is not > 0m ||
                projected.PartialTakeProfitFraction is not > 0m)
            {
                if (evidence.PartialExitStatus != PartialExitStatus.NotFeasible ||
                    evidence.PartialExitQuantity.HasValue)
                {
                    throw new InvalidDataException("A partial-exit result requires an exact point-in-time trading unit.");
                }

                continue;
            }

            var calculated = LotPartialExitQuantityCalculator.Calculate(
                new MarginLotId(evidence.MarginLotId),
                projected.CurrentQuantity.Value,
                new WholeShareQuantity(tradingUnit.Value),
                projected.PartialTakeProfitFraction.Value);
            if (evidence.PartialExitStatus != calculated.Status ||
                evidence.PartialExitQuantity != calculated.CandidateQuantity?.Value)
            {
                throw new InvalidDataException("The lot partial-exit result does not match the point-in-time trading unit.");
            }
        }
    }

    private static (bool IsResolved, decimal NetCost) ResolveNetCost(
        MarginCostObservationRow? observation,
        string? expectedCurrency)
    {
        if (observation is null)
        {
            return (false, 0m);
        }

        if (!Enum.TryParse<AmountStatus>(observation.AmountStatus, false, out var status) ||
            !Enum.IsDefined(status) ||
            status.ToString() != observation.AmountStatus)
        {
            throw new InvalidDataException("A margin-cost observation has an invalid amount status.");
        }

        var resolved = status is AmountStatus.KnownAmount or AmountStatus.KnownZero or
            AmountStatus.NotOccurred or AmountStatus.NotApplicable;
        if (!resolved)
        {
            return (false, 0m);
        }

        var magnitude = status switch
        {
            AmountStatus.KnownAmount when observation.Amount is > 0m && observation.Currency == expectedCurrency => observation.Amount.Value,
            AmountStatus.KnownZero when observation.Amount == 0m && observation.Currency == expectedCurrency => 0m,
            AmountStatus.NotOccurred or AmountStatus.NotApplicable when observation.Amount is null && observation.Currency is null => 0m,
            _ => throw new InvalidDataException("A margin-cost observation has inconsistent amount/currency evidence."),
        };
        return observation.Direction switch
        {
            "Charge" => (true, magnitude),
            "Credit" => (true, -magnitude),
            _ => throw new InvalidDataException("A margin-cost observation has an invalid direction."),
        };
    }

    private static void ValidateLotEvidence(PositionEvaluationLotEvidence evidence)
    {
        if (evidence.MarginLotId == Guid.Empty ||
            (evidence.Outcome == PositionEvaluationOutcome.Evaluated) != evidence.Decision.HasValue)
        {
            throw new InvalidDataException("A lot evaluation has an invalid outcome/decision contract.");
        }

        if (evidence.PartialExitStatus == PartialExitStatus.Candidate)
        {
            if (evidence.PartialExitQuantity is null or <= 0)
            {
                throw new InvalidDataException("A lot partial-exit candidate requires a positive quantity.");
            }
        }
        else if (evidence.PartialExitQuantity.HasValue)
        {
            throw new InvalidDataException("Only a lot partial-exit candidate can carry a quantity.");
        }

        if (evidence.Outcome != PositionEvaluationOutcome.Evaluated &&
            evidence.PartialExitStatus != PartialExitStatus.NotApplicable)
        {
            throw new InvalidDataException("A fail-closed lot cannot carry partial-exit advice.");
        }

        using var document = JsonDocument.Parse(evidence.EvidenceJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Lot evidence must be a JSON object.");
        }
    }

    private static void ValidateAggregatePartialExit(
        PositionEvaluation evaluation,
        IReadOnlyList<PositionEvaluationLotEvidence> lots)
    {
        if (evaluation.Decision != ExitDecision.TakeProfit)
        {
            if (evaluation.PartialExitStatus != PartialExitStatus.NotApplicable ||
                evaluation.PartialExitQuantity.HasValue)
            {
                throw new InvalidDataException("Only a TakeProfit position can publish partial-exit advice.");
            }

            return;
        }

        var candidateQuantity = lots
            .Where(item => item.Decision == ExitDecision.TakeProfit &&
                           item.PartialExitStatus == PartialExitStatus.Candidate)
            .Sum(item => item.PartialExitQuantity!.Value);
        var expectedStatus = candidateQuantity > 0
            ? PartialExitStatus.Candidate
            : lots.Any(item => item.Decision == ExitDecision.TakeProfit &&
                               item.PartialExitStatus == PartialExitStatus.NotFeasible)
                ? PartialExitStatus.NotFeasible
                : PartialExitStatus.NotApplicable;
        if (evaluation.PartialExitStatus != expectedStatus ||
            evaluation.PartialExitQuantity != (candidateQuantity > 0 ? candidateQuantity : null))
        {
            throw new InvalidDataException("The position partial-exit result does not match the lot candidates.");
        }
    }

    private static int DecisionPriority(ExitDecision decision) => decision switch
    {
        ExitDecision.StopLoss => 4,
        ExitDecision.Exit => 3,
        ExitDecision.TakeProfit => 2,
        ExitDecision.Hold => 1,
        _ => throw new InvalidDataException("Unknown position exit decision."),
    };

    private static string[] NormalizeReasonCodes(IReadOnlyCollection<string> reasonCodes)
    {
        var normalized = reasonCodes
            .Select(code => code?.Trim())
            .Where(code => !string.IsNullOrEmpty(code))
            .Select(code => code!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0 || normalized.Any(code => code.Any(char.IsWhiteSpace)))
        {
            throw new InvalidDataException("At least one stable, whitespace-free reason code is required.");
        }

        return normalized;
    }

    private static void ValidateReasonCodes(
        PositionEvaluationProjection projection,
        IReadOnlyCollection<string> reasonCodes)
    {
        if (projection.Status != PositionProjectionStatus.Ready &&
            projection.StatusReasons.Any(reason => !reasonCodes.Contains(reason, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The stored reasons do not preserve every projection failure code.");
        }
    }

    private static string WriteReasonsJson(IReadOnlyList<string> reasons) => WriteJson(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", ReasonsSchemaVersion);
        writer.WritePropertyName("reasons");
        writer.WriteStartArray();
        foreach (var reason in reasons)
        {
            writer.WriteStringValue(reason);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static string WriteLotEvaluationsJson(IReadOnlyList<PositionEvaluationLotEvidence> lots) => WriteJson(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", LotEvaluationsSchemaVersion);
        writer.WritePropertyName("lots");
        writer.WriteStartArray();
        foreach (var lot in lots)
        {
            using var evidence = JsonDocument.Parse(lot.EvidenceJson);
            writer.WriteStartObject();
            writer.WriteString("marginLotId", lot.MarginLotId.ToString("D"));
            WriteNullableGuid(writer, "riskBasisSnapshotId", lot.RiskBasisSnapshotId);
            WriteNullableGuid(writer, "riskPlanRevisionId", lot.RiskPlanRevisionId);
            writer.WriteString("outcome", lot.Outcome.ToString());
            if (lot.Decision.HasValue)
            {
                writer.WriteString("decision", lot.Decision.Value.ToString());
            }
            else
            {
                writer.WriteNull("decision");
            }
            writer.WriteString("partialExitStatus", lot.PartialExitStatus.ToString());
            if (lot.PartialExitQuantity.HasValue)
            {
                writer.WriteNumber("partialExitQuantity", lot.PartialExitQuantity.Value);
            }
            else
            {
                writer.WriteNull("partialExitQuantity");
            }
            writer.WritePropertyName("evidence");
            WriteCanonicalElement(writer, evidence.RootElement);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static IReadOnlyList<string> ParseReasonsJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.GetProperty("schemaVersion").GetString() != ReasonsSchemaVersion)
        {
            throw new InvalidDataException("The reasons JSON schema is invalid.");
        }

        var reasons = root.GetProperty("reasons").EnumerateArray()
            .Select(element => element.GetString() ?? throw new InvalidDataException("A reason code is null."))
            .ToArray();
        var normalized = NormalizeReasonCodes(reasons);
        if (!string.Equals(json, WriteReasonsJson(normalized), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The reasons JSON is not canonical.");
        }

        return normalized;
    }

    private static IReadOnlyList<PositionEvaluationLotEvidence> ParseLotEvaluationsJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.GetProperty("schemaVersion").GetString() != LotEvaluationsSchemaVersion)
        {
            throw new InvalidDataException("The lot evaluations JSON schema is invalid.");
        }

        var lots = root.GetProperty("lots").EnumerateArray().Select(element =>
        {
            var outcome = ParseEnum<PositionEvaluationOutcome>(element, "outcome");
            ExitDecision? decision = element.GetProperty("decision").ValueKind == JsonValueKind.Null
                ? null
                : ParseEnum<ExitDecision>(element, "decision");
            var partialStatus = ParseEnum<PartialExitStatus>(element, "partialExitStatus");
            var partialQuantity = element.GetProperty("partialExitQuantity").ValueKind == JsonValueKind.Null
                ? (long?)null
                : element.GetProperty("partialExitQuantity").GetInt64();
            return new PositionEvaluationLotEvidence(
                ParseGuid(element, "marginLotId")!.Value,
                ParseGuid(element, "riskBasisSnapshotId"),
                ParseGuid(element, "riskPlanRevisionId"),
                outcome,
                decision,
                partialStatus,
                partialQuantity,
                element.GetProperty("evidence").GetRawText());
        }).OrderBy(item => item.MarginLotId).ToArray();

        if (!string.Equals(json, WriteLotEvaluationsJson(lots), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The lot evaluations JSON is not canonical.");
        }

        return lots;
    }

    private static T ParseEnum<T>(JsonElement element, string propertyName) where T : struct, Enum
    {
        var text = element.GetProperty(propertyName).GetString();
        if (!Enum.TryParse<T>(text, false, out var value) ||
            !Enum.IsDefined(value) ||
            value.ToString() != text)
        {
            throw new InvalidDataException($"The {propertyName} value is invalid.");
        }

        return value;
    }

    private static Guid? ParseGuid(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        if (property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var text = property.GetString();
        if (!Guid.TryParseExact(text, "D", out var value) || value == Guid.Empty || value.ToString("D") != text)
        {
            throw new InvalidDataException($"The {propertyName} value is invalid.");
        }

        return value;
    }

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = element.EnumerateObject().ToArray();
                if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                {
                    throw new InvalidDataException("Lot evidence cannot contain duplicate JSON property names.");
                }
                foreach (var property in properties.OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number when element.TryGetInt64(out var integer):
                writer.WriteNumberValue(integer);
                break;
            case JsonValueKind.Number when element.TryGetDecimal(out var number):
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.Number:
                throw new InvalidDataException("Lot evidence numbers must fit the supported integer or decimal range.");
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException("Lot evidence contains an unsupported JSON token.");
        }
    }

    private static void WriteNullableGuid(Utf8JsonWriter writer, string propertyName, Guid? value)
    {
        if (value.HasValue)
        {
            writer.WriteString(propertyName, value.Value.ToString("D"));
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }

    private static void EnsureProjectionExact(
        PositionEvaluationProjection supplied,
        PositionEvaluationProjection verified)
    {
        if (supplied.PositionId != verified.PositionId ||
            supplied.InstrumentId != verified.InstrumentId ||
            supplied.PositionSide != verified.PositionSide ||
            supplied.Status != verified.Status ||
            !supplied.StatusReasons.SequenceEqual(verified.StatusReasons, StringComparer.Ordinal) ||
            supplied.CurrentPrice != verified.CurrentPrice ||
            supplied.Lots.Count != verified.Lots.Count ||
            supplied.Lots.Zip(verified.Lots).Any(pair => !LotProjectionEquals(pair.First, pair.Second)))
        {
            throw new InvalidDataException("The supplied position projection is not the exact current reconstruction.");
        }

        EnsureManifestDraftExact(supplied.Manifest, verified.Manifest);
    }

    private static bool LotProjectionEquals(
        PositionEvaluationLotProjection left,
        PositionEvaluationLotProjection right) =>
        left.MarginLotId == right.MarginLotId &&
        left.OpeningTradeExecutionRevisionId == right.OpeningTradeExecutionRevisionId &&
        left.CurrentQuantity == right.CurrentQuantity &&
        left.EntryBasisPrice == right.EntryBasisPrice &&
        left.FixedAtr == right.FixedAtr &&
        left.RiskAmountR == right.RiskAmountR &&
        left.StopMultiplier == right.StopMultiplier &&
        left.PartialTakeProfitRMultiple == right.PartialTakeProfitRMultiple &&
        left.PartialTakeProfitFraction == right.PartialTakeProfitFraction &&
        left.AtrReferenceBarDate == right.AtrReferenceBarDate &&
        left.AtrPeriod == right.AtrPeriod &&
        left.AtrAlgorithmId == right.AtrAlgorithmId &&
        left.StopPrice == right.StopPrice &&
        left.TakeProfitPrice == right.TakeProfitPrice &&
        left.PriceCurrency == right.PriceCurrency &&
        left.PriceUnitBasisSha256 == right.PriceUnitBasisSha256 &&
        left.ContractRevisionId == right.ContractRevisionId &&
        left.RiskBasisSnapshotId == right.RiskBasisSnapshotId &&
        left.RiskPlanRevisionId == right.RiskPlanRevisionId &&
        left.MarginCostItemIds.SequenceEqual(right.MarginCostItemIds) &&
        left.MarginCostObservationIds.SequenceEqual(right.MarginCostObservationIds);

    private static void EnsureManifestExact(
        PositionEvaluationInputManifestRow stored,
        PositionEvaluationManifestDraft rebuilt)
    {
        if (stored.AnalysisRunId != rebuilt.AnalysisRunId ||
            stored.PositionId != rebuilt.PositionId ||
            stored.AnalysisInputManifestId != rebuilt.AnalysisInputManifestId ||
            stored.CurrentPriceRevisionId != rebuilt.CurrentPriceRevisionId ||
            stored.TradeExecutionRevisionIdsJson != rebuilt.TradeExecutionRevisionIdsJson ||
            stored.LotAllocationRevisionIdsJson != rebuilt.LotAllocationRevisionIdsJson ||
            stored.PositionAdjustmentIdsJson != rebuilt.PositionAdjustmentIdsJson ||
            stored.ContractRevisionIdsJson != rebuilt.ContractRevisionIdsJson ||
            stored.RiskBasisSnapshotIdsJson != rebuilt.RiskBasisSnapshotIdsJson ||
            stored.RiskPlanRevisionIdsJson != rebuilt.RiskPlanRevisionIdsJson ||
            stored.MarginCostObservationIdsJson != rebuilt.MarginCostObservationIdsJson ||
            stored.ProjectionVersion != rebuilt.ProjectionVersion ||
            stored.RecordedCutoffAtUtc != rebuilt.RecordedCutoffAtUtc ||
            stored.ManifestSha256 != rebuilt.ManifestSha256)
        {
            throw new InvalidDataException("The stored position evaluation manifest does not match its exact source graph.");
        }
    }

    private static void EnsureManifestDraftExact(
        PositionEvaluationManifestDraft supplied,
        PositionEvaluationManifestDraft verified)
    {
        if (supplied != verified)
        {
            throw new InvalidDataException("The supplied position evaluation manifest is not the exact verified draft.");
        }
    }
}
