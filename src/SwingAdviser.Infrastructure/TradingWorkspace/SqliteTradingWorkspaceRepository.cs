using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.TradingWorkspace;

public sealed class SqliteTradingWorkspaceRepository(DbContextOptions<SwingAdviserDbContext> options)
    : ITradingWorkspaceRepository
{
    public async Task<TradingWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();

        var instruments = await context.Set<InstrumentRow>().AsNoTracking().ToListAsync(cancellationToken);
        var identifiers = await context.Set<InstrumentIdentifierRow>().AsNoTracking().ToListAsync(cancellationToken);
        var identifierRevisions = await context.Set<InstrumentIdentifierRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var masters = await context.Set<InstrumentMasterRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);

        var identityByInstrument = BuildIdentityMap(instruments, identifiers, identifierRevisions, masters);
        var candidates = await LoadCandidatesAsync(context, identityByInstrument, cancellationToken);
        var executions = await LoadExecutionsAsync(context, identityByInstrument, cancellationToken);
        var positions = await LoadPositionsAsync(context, identityByInstrument, cancellationToken);

        return new TradingWorkspaceSnapshot(candidates, positions, executions, DateTimeOffset.UtcNow);
    }

    public async Task<ManualExecutionResult> RegisterManualExecutionAsync(
        RegisterManualExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        if (!await context.Set<InstrumentRow>().AnyAsync(x => x.Id == request.InstrumentId, cancellationToken))
        {
            throw new InvalidOperationException("The selected instrument no longer exists.");
        }

        var riskInput = request.Kind == ExecutionKind.Open
            ? await ResolveInitialRiskInputAsync(context, request, cancellationToken)
            : null;

        var now = DateTimeOffset.UtcNow;
        var positionId = request.PositionId ?? Guid.NewGuid();
        if (request.Kind == ExecutionKind.Open)
        {
            context.Add(new PositionRow
            {
                Id = positionId,
                InstrumentId = request.InstrumentId,
                PositionSide = request.Side.ToString(),
                StrategyParameterSnapshotId = riskInput!.StrategyParameterSnapshotId,
                OriginCandidateResultId = request.CandidateContextId,
                CreatedAtUtc = now,
            });

            var initialState = new PositionStateRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 1,
                PositionId = positionId,
                Status = PositionStatus.Open.ToString(),
                ReconciliationStatus = ReconciliationStatus.Clear.ToString(),
                EffectiveAtUtc = request.ExecutedAtUtc,
                Reason = "利用者確認済み新規建約定の登録",
                RecordedAtUtc = now,
            };
            initialState.ContentSha256 = Hash(new
            {
                initialState.PositionId,
                initialState.Status,
                initialState.ReconciliationStatus,
                initialState.EffectiveAtUtc,
                initialState.Reason,
            });
            context.Add(initialState);
        }
        else
        {
            var position = await context.Set<PositionRow>().SingleOrDefaultAsync(x => x.Id == positionId, cancellationToken)
                ?? throw new InvalidOperationException("The selected position no longer exists.");
            if (position.InstrumentId != request.InstrumentId || position.PositionSide != request.Side.ToString())
            {
                throw new InvalidOperationException("The selected position does not match the instrument and side.");
            }

            var positionStates = await context.Set<PositionStateRevisionRow>()
                .Where(x => x.PositionId == positionId)
                .ToListAsync(cancellationToken);
            var currentState = Leaf(positionStates, x => x.Id, x => x.SupersedesId).Single();
            if (currentState.Status != PositionStatus.Open.ToString())
            {
                throw new InvalidOperationException("A closing execution can be registered only for an open position.");
            }

            if (currentState.ReconciliationStatus is nameof(ReconciliationStatus.Required) or nameof(ReconciliationStatus.InProgress))
            {
                throw new InvalidOperationException("Lot allocation is unavailable while the position requires reconciliation.");
            }

            await ValidateLotAllocationsAsync(context, positionId, request.LotAllocations, cancellationToken);
        }

        var executionId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        context.Add(new TradeExecutionRow
        {
            Id = executionId,
            PositionId = positionId,
            ExecutionKind = request.Kind.ToString(),
            Origin = ExecutionOrigin.UserConfirmed.ToString(),
            CandidateContextId = request.CandidateContextId,
            CreatedAtUtc = now,
        });

        var revision = CreateExecutionRevision(
            revisionId,
            executionId,
            1,
            null,
            request.ExecutedAtUtc,
            request.Price,
            request.Quantity,
            request.Currency,
            ExecutionChangeKind.Initial,
            RecordDisposition.Effective,
            request.UserConfirmedAtUtc,
            request.Broker,
            request.ExternalReference,
            request.UserNote,
            null,
            now);
        context.Add(revision);

        if (request.Kind == ExecutionKind.Open)
        {
            var lotId = Guid.NewGuid();
            context.Add(new MarginLotRow
            {
                Id = lotId,
                PositionId = positionId,
                OpeningTradeExecutionId = executionId,
                InitialOpeningTradeExecutionRevisionId = revisionId,
                CreatedAtUtc = now,
            });

            var contract = new MarginLotContractRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 1,
                MarginLotId = lotId,
                OpeningTradeExecutionRevisionId = revisionId,
                MarginType = MarginType.Unknown.ToString(),
                Broker = string.IsNullOrWhiteSpace(request.Broker) ? "未確認" : request.Broker.Trim(),
                ProductName = "未確認",
                EffectiveFromDate = DateOnly.FromDateTime(request.ExecutedAtUtc.UtcDateTime),
                TermType = MarginTermType.Unknown.ToString(),
                ContractCurrency = request.Currency,
                SpecialFeePolicyJson = "{}",
                RightsProcessingJson = "{}",
                ConfirmedAtUtc = request.UserConfirmedAtUtc,
                Evidence = "手動約定登録時点では契約条件未確認",
                ChangeKind = ContractChangeKind.Initial.ToString(),
                RecordedAtUtc = now,
            };
            contract.ContentSha256 = Hash(new
            {
                contract.MarginLotId,
                contract.OpeningTradeExecutionRevisionId,
                contract.MarginType,
                contract.Broker,
                contract.ProductName,
                contract.EffectiveFromDate,
                contract.TermType,
                contract.ContractCurrency,
                contract.Evidence,
            });
            context.Add(contract);

            AddInitialRiskPlan(
                context,
                request,
                riskInput!,
                positionId,
                executionId,
                revision,
                lotId,
                now);
        }
        else
        {
            foreach (var allocation in request.LotAllocations)
            {
                var row = new LotAllocationRevisionRow
                {
                    Id = Guid.NewGuid(),
                    AllocationKey = Guid.NewGuid(),
                    RevisionNo = 1,
                    ClosingTradeExecutionId = executionId,
                    ClosingTradeExecutionRevisionId = revisionId,
                    MarginLotId = allocation.MarginLotId,
                    Quantity = allocation.Quantity,
                    RecordDisposition = RecordDisposition.Effective.ToString(),
                    ChangeKind = ExecutionChangeKind.Initial.ToString(),
                    UserConfirmedAtUtc = request.UserConfirmedAtUtc,
                    RecordedAtUtc = now,
                };
                row.ContentSha256 = Hash(new
                {
                    row.AllocationKey,
                    row.ClosingTradeExecutionRevisionId,
                    row.MarginLotId,
                    row.Quantity,
                    row.RecordDisposition,
                });
                context.Add(row);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (request.Kind == ExecutionKind.Close &&
            await IsPositionFullyClosedAsync(context, positionId, cancellationToken))
        {
            await AppendPositionStateAsync(
                context,
                positionId,
                PositionStatus.Closed,
                ReconciliationStatus.Clear,
                request.ExecutedAtUtc,
                "利用者確認済み決済約定により全lotを決済",
                now,
                cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ManualExecutionResult(executionId, revisionId, positionId, 1);
    }

    public async Task<ManualExecutionResult> CorrectManualExecutionAsync(
        CorrectManualExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var execution = await context.Set<TradeExecutionRow>()
            .SingleOrDefaultAsync(x => x.Id == request.ExecutionId, cancellationToken)
            ?? throw new InvalidOperationException("The execution no longer exists.");
        var revisions = await context.Set<TradeExecutionRevisionRow>()
            .Where(x => x.TradeExecutionId == request.ExecutionId)
            .OrderBy(x => x.RevisionNo)
            .ToListAsync(cancellationToken);
        var leaf = Leaf(revisions, x => x.Id, x => x.SupersedesId).Single();
        if (leaf.Id != request.ExpectedCurrentRevisionId)
        {
            throw new InvalidOperationException("The execution was changed by another operation. Reload before correcting it.");
        }

        var now = DateTimeOffset.UtcNow;
        var revisionId = Guid.NewGuid();
        context.Add(CreateExecutionRevision(
            revisionId,
            execution.Id,
            leaf.RevisionNo + 1,
            leaf.Id,
            request.ExecutedAtUtc,
            request.Price,
            request.Quantity,
            request.Currency,
            ExecutionChangeKind.Correction,
            RecordDisposition.Effective,
            request.UserConfirmedAtUtc,
            request.Broker,
            request.ExternalReference,
            request.UserNote,
            request.CorrectionReason.Trim(),
            now));

        await AppendPositionStateAsync(
            context,
            execution.PositionId,
            null,
            ReconciliationStatus.Required,
            now,
            "約定訂正に伴う依存データの要照合",
            now,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ManualExecutionResult(execution.Id, revisionId, execution.PositionId, leaf.RevisionNo + 1);
    }

    private SwingAdviserDbContext CreateContext() => new(options);

    private static async Task<InitialRiskInput> ResolveInitialRiskInputAsync(
        SwingAdviserDbContext context,
        RegisterManualExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var eligibleStatus = new[]
        {
            AnalysisRunStatus.Succeeded.ToString(),
            AnalysisRunStatus.PartiallySucceeded.ToString(),
        };
        var executionJstDate = DateOnly.FromDateTime(
            request.ExecutedAtUtc.ToOffset(TimeSpan.FromHours(9)).DateTime);

        var query =
            from technical in context.Set<TechnicalAnalysisResultRow>().AsNoTracking()
            join run in context.Set<AnalysisRunRow>().AsNoTracking() on technical.AnalysisRunId equals run.Id
            join manifest in context.Set<AnalysisInputManifestRow>().AsNoTracking() on technical.AnalysisInputManifestId equals manifest.Id
            join indicator in context.Set<IndicatorResultRow>().AsNoTracking() on technical.Id equals indicator.TechnicalAnalysisResultId
            where technical.InstrumentId == request.InstrumentId
                  && technical.PositionSide == request.Side.ToString()
                  && technical.SignalPurpose == SignalPurpose.Entry.ToString()
                  && eligibleStatus.Contains(run.Status)
                  && run.PointInTimeStatus == PointInTimeStatus.Verified.ToString()
                  && run.AnalyzedAtUtc < request.ExecutedAtUtc
                  && run.RecordedCutoffAtUtc < request.ExecutedAtUtc
                  && manifest.AnalysisRunId == run.Id
                  && manifest.InstrumentId == request.InstrumentId
                  && manifest.HistoryStatus == HistoryStatus.Complete.ToString()
                  && manifest.PointInTimeStatus == PointInTimeStatus.Verified.ToString()
                  && manifest.LastBarDate == run.EvaluationBarDate
                  && manifest.SelectedAvailableCutoffAtUtc < request.ExecutedAtUtc
                  && manifest.SelectedRecordedCutoffAtUtc < request.ExecutedAtUtc
                  && indicator.IndicatorKey == "ATR14"
                  && indicator.AlgorithmId == TechnicalIndicatorEngine.AtrAlgorithmId
            select new { technical, run, manifest, indicator };

        if (request.CandidateContextId is { } candidateId)
        {
            var candidate = await context.Set<CandidateResultRow>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == candidateId, cancellationToken)
                ?? throw new InvalidOperationException("The selected candidate no longer exists.");
            query = query.Where(x => x.technical.Id == candidate.TechnicalAnalysisResultId &&
                                     x.technical.Outcome == TechnicalAnalysisOutcome.Candidate.ToString());
        }
        else
        {
            query = query.Where(x => x.run.EvaluationBarDate < executionJstDate);
        }

        var selected = await query
            .OrderByDescending(x => x.run.EvaluationBarDate)
            .ThenByDescending(x => x.run.AnalyzedAtUtc)
            .ThenBy(x => x.indicator.Ordinal)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                request.CandidateContextId is null
                    ? "No point-in-time verified ATR from the latest confirmed pre-execution bar is available."
                    : "The candidate context has no eligible point-in-time verified ATR before the opening execution.");

        var period = ReadPositiveInt(selected.indicator.ParametersJson, "period");
        if (period != 14)
        {
            throw new InvalidDataException("The initial risk basis requires ATR period 14.");
        }
        var (referenceDate, atr) = ReadAtrValue(selected.indicator.ValuesJson);
        if (referenceDate != selected.run.EvaluationBarDate)
        {
            throw new InvalidDataException("The ATR reference date does not match the frozen analysis run.");
        }

        var masterRows = await context.Set<InstrumentMasterRevisionRow>().AsNoTracking()
            .Where(x => x.InstrumentId == request.InstrumentId &&
                        x.RecordedAtUtc < request.ExecutedAtUtc &&
                        (x.AvailableAtUtc == null || x.AvailableAtUtc < request.ExecutedAtUtc))
            .ToListAsync(cancellationToken);
        var master = Leaf(masterRows, x => x.Id, x => x.SupersedesId)
            .Where(x => x.EffectiveFromDate <= executionJstDate &&
                        (x.EffectiveToDate == null || executionJstDate <= x.EffectiveToDate))
            .OrderByDescending(x => x.EffectiveFromDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No point-in-time instrument master is available for the opening execution.");
        var currency = new CurrencyCode(request.Currency);
        if (!string.Equals(master.Currency, currency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The opening currency does not match the point-in-time instrument master.");
        }
        var priceUnitBasisSha256 = Hash(new
        {
            SchemaVersion = "risk-price-unit-basis-v1",
            request.InstrumentId,
            Currency = currency.Value,
            selected.manifest.CorporateActionSetSha256,
        });

        return new InitialRiskInput(
            request.CandidateContextId,
            selected.run.StrategyParameterSnapshotId,
            selected.manifest.Id,
            referenceDate,
            atr,
            period,
            selected.indicator.AlgorithmId,
            currency.Value,
            priceUnitBasisSha256);
    }

    private static void AddInitialRiskPlan(
        SwingAdviserDbContext context,
        RegisterManualExecutionRequest request,
        InitialRiskInput riskInput,
        Guid positionId,
        Guid executionId,
        TradeExecutionRevisionRow executionRevision,
        Guid lotId,
        DateTimeOffset recordedAtUtc)
    {
        var position = new Position(
            new PositionId(positionId),
            new InstrumentId(request.InstrumentId),
            request.Side,
            riskInput.StrategyParameterSnapshotId,
            request.CandidateContextId is { } candidateId ? new CandidateResultId(candidateId) : null,
            recordedAtUtc);
        var execution = position.RegisterUserConfirmedExecution(
            new TradeExecutionId(executionId),
            ExecutionKind.Open,
            new UserConfirmedExecutionInput(
                request.ExecutedAtUtc,
                new PositivePrice(request.Price),
                new WholeShareQuantity(request.Quantity),
                new CurrencyCode(request.Currency),
                request.UserConfirmedAtUtc,
                request.Broker,
                request.ExternalReference,
                request.UserNote),
            new TradeExecutionRevisionId(executionRevision.Id),
            new RevisionMetadata(
                executionRevision.Id,
                1,
                null,
                new Sha256Hash(executionRevision.ContentSha256),
                recordedAtUtc),
            recordedAtUtc,
            request.CandidateContextId is { } contextId ? new CandidateResultId(contextId) : null);
        var lot = MarginLot.FromUserConfirmedOpening(new MarginLotId(lotId), execution, recordedAtUtc);
        var unit = new RiskPriceUnit(
            new InstrumentId(request.InstrumentId),
            new CurrencyCode(riskInput.PriceCurrency),
            new Sha256Hash(riskInput.PriceUnitBasisSha256));
        var riskBasisId = Guid.NewGuid();
        var riskPlanId = Guid.NewGuid();
        var placeholderHash = new Sha256Hash(new string('0', 64));

        var draft = InitialRiskPlanBundle.Create(
            riskBasisId,
            new RevisionMetadata(riskPlanId, 1, null, placeholderHash, recordedAtUtc),
            position,
            lot,
            new UnitizedRiskPrice(new PositivePrice(request.Price), unit),
            riskInput.AtrReferenceBarDate,
            new UnitizedRiskPrice(new PositivePrice(riskInput.FixedAtr), unit),
            riskInput.AtrPeriod,
            riskInput.AtrAlgorithmId,
            RiskManagementParameters.Initial,
            placeholderHash,
            request.ExecutedAtUtc,
            recordedAtUtc);

        var basisHash = Hash(new
        {
            SchemaVersion = "risk-basis-snapshot-v1",
            MarginLotId = lotId,
            OpeningTradeExecutionRevisionId = executionRevision.Id,
            riskInput.OriginCandidateResultId,
            riskInput.StrategyParameterSnapshotId,
            riskInput.AnalysisInputManifestId,
            riskInput.PriceCurrency,
            riskInput.PriceUnitBasisSha256,
            EntryBasisPrice = request.Price,
            riskInput.AtrReferenceBarDate,
            riskInput.FixedAtr,
            riskInput.AtrPeriod,
            riskInput.AtrAlgorithmId,
            draft.RiskBasis.StopMultiplier,
            draft.RiskBasis.RiskAmountR,
            draft.RiskBasis.PartialTakeProfitRMultiple,
            draft.RiskBasis.PartialTakeProfitFraction,
            InitialStopPrice = draft.RiskBasis.InitialStopPrice.Value,
            InitialTakeProfitPrice = draft.RiskBasis.InitialTakeProfitPrice.Value,
        });
        var planHash = Hash(new
        {
            SchemaVersion = "risk-plan-revision-v1",
            RiskBasisSnapshotId = riskBasisId,
            StopPrice = draft.RiskPlan.StopPrice.Value,
            TakeProfitPrice = draft.RiskPlan.TakeProfitPrice.Value,
            PlanReason = RiskPlanReason.Initial.ToString(),
            EffectiveAtUtc = request.ExecutedAtUtc,
            IsCostAdjusted = false,
        });
        var bundle = InitialRiskPlanBundle.Create(
            riskBasisId,
            new RevisionMetadata(riskPlanId, 1, null, new Sha256Hash(planHash), recordedAtUtc),
            position,
            lot,
            new UnitizedRiskPrice(new PositivePrice(request.Price), unit),
            riskInput.AtrReferenceBarDate,
            new UnitizedRiskPrice(new PositivePrice(riskInput.FixedAtr), unit),
            riskInput.AtrPeriod,
            riskInput.AtrAlgorithmId,
            RiskManagementParameters.Initial,
            new Sha256Hash(basisHash),
            request.ExecutedAtUtc,
            recordedAtUtc);

        context.Add(new RiskBasisSnapshotRow
        {
            Id = riskBasisId,
            MarginLotId = lotId,
            RevisionNo = 1,
            OpeningTradeExecutionRevisionId = executionRevision.Id,
            OriginCandidateResultId = riskInput.OriginCandidateResultId,
            StrategyParameterSnapshotId = riskInput.StrategyParameterSnapshotId,
            AnalysisInputManifestId = riskInput.AnalysisInputManifestId,
            PriceCurrency = riskInput.PriceCurrency,
            PriceUnitBasisSha256 = riskInput.PriceUnitBasisSha256,
            EntryBasisPrice = bundle.RiskBasis.EntryBasisPrice.Value,
            AtrReferenceBarDate = bundle.RiskBasis.AtrReferenceBarDate,
            FixedAtr = bundle.RiskBasis.FixedAtr.Value,
            AtrPeriod = bundle.RiskBasis.AtrPeriod,
            AtrAlgorithmId = bundle.RiskBasis.AtrAlgorithmId,
            StopMultiplier = bundle.RiskBasis.StopMultiplier,
            RiskAmountR = bundle.RiskBasis.RiskAmountR,
            PartialTakeProfitRMultiple = bundle.RiskBasis.PartialTakeProfitRMultiple,
            PartialTakeProfitFraction = bundle.RiskBasis.PartialTakeProfitFraction,
            InitialStopPrice = bundle.RiskBasis.InitialStopPrice.Value,
            InitialTakeProfitPrice = bundle.RiskBasis.InitialTakeProfitPrice.Value,
            ContentSha256 = bundle.RiskBasis.ContentHash.Value,
            CreatedAtUtc = bundle.RiskBasis.CreatedAtUtc,
        });
        context.Add(new RiskPlanRevisionRow
        {
            Id = riskPlanId,
            RevisionNo = 1,
            ContentSha256 = bundle.RiskPlan.Audit.ContentHash.Value,
            RecordedAtUtc = bundle.RiskPlan.Audit.RecordedAtUtc,
            RiskBasisSnapshotId = riskBasisId,
            StopPrice = bundle.RiskPlan.StopPrice.Value,
            TakeProfitPrice = bundle.RiskPlan.TakeProfitPrice.Value,
            PlanReason = bundle.RiskPlan.Reason.ToString(),
            EffectiveAtUtc = bundle.RiskPlan.EffectiveAtUtc,
            IsCostAdjusted = false,
        });
    }

    private static int ReadPositiveInt(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(propertyName, out var value) ||
            !value.TryGetInt32(out var parsed) || parsed <= 0)
        {
            throw new InvalidDataException($"Indicator parameters require a positive '{propertyName}'.");
        }

        return parsed;
    }

    private static (DateOnly ReferenceDate, decimal Atr) ReadAtrValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("value", out var value) ||
            !value.TryGetProperty("evaluationBarDate", out var dateElement) ||
            !DateOnly.TryParseExact(dateElement.GetString(), "yyyy-MM-dd", out var referenceDate) ||
            !value.TryGetProperty("current", out var atrElement) ||
            !atrElement.TryGetDecimal(out var atr) || atr <= 0m)
        {
            throw new InvalidDataException("ATR values must contain a positive current value and evaluation bar date.");
        }

        return (referenceDate, atr);
    }

    private static async Task<IReadOnlyList<CandidateListItem>> LoadCandidatesAsync(
        SwingAdviserDbContext context,
        IReadOnlyDictionary<Guid, InstrumentIdentity> identities,
        CancellationToken cancellationToken)
    {
        var candidateRows = await context.Set<CandidateResultRow>().AsNoTracking().ToListAsync(cancellationToken);
        var technicalRows = await context.Set<TechnicalAnalysisResultRow>().AsNoTracking().ToListAsync(cancellationToken);
        var runRows = await context.Set<AnalysisRunRow>().AsNoTracking().ToListAsync(cancellationToken);
        var snapshots = await context.Set<StrategyParameterSnapshotRow>().AsNoTracking().ToListAsync(cancellationToken);
        var jobs = await context.Set<AiCheckJobRow>().AsNoTracking().ToListAsync(cancellationToken);
        var attempts = await context.Set<AiAttemptRow>().AsNoTracking().ToListAsync(cancellationToken);
        var results = await context.Set<AiResultRow>().AsNoTracking().ToListAsync(cancellationToken);
        var eligibilityRecords = await context.Set<MarginEligibilityRecordRow>().AsNoTracking().ToListAsync(cancellationToken);
        var eligibilityRevisions = await context.Set<MarginEligibilityRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);

        var technicalById = technicalRows.ToDictionary(x => x.Id);
        var runById = runRows.ToDictionary(x => x.Id);
        var snapshotById = snapshots.ToDictionary(x => x.Id);
        var resultByAttempt = results.ToDictionary(x => x.AiAttemptId);
        var eligibilityByInstrument = eligibilityRecords
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(record =>
                        Leaf(
                            eligibilityRevisions.Where(x => x.MarginEligibilityRecordId == record.Id),
                            x => x.Id,
                            x => x.SupersedesId))
                    .OrderByDescending(x => x.EffectiveFromDate)
                    .FirstOrDefault());

        var items = new List<CandidateListItem>();
        foreach (var row in candidateRows)
        {
            if (!technicalById.TryGetValue(row.TechnicalAnalysisResultId, out var technical) ||
                !runById.TryGetValue(technical.AnalysisRunId, out var run) ||
                !identities.TryGetValue(technical.InstrumentId, out var identity))
            {
                continue;
            }

            snapshotById.TryGetValue(run.StrategyParameterSnapshotId, out var strategy);
            var latestAttempt = jobs.Where(x => x.CandidateResultId == row.Id)
                .SelectMany(job => attempts.Where(x => x.AiCheckJobId == job.Id))
                .OrderByDescending(x => x.AttemptNo)
                .ThenByDescending(x => x.RequestedAtUtc)
                .FirstOrDefault();
            AiAttemptStatus? aiStatus = ParseNullable<AiAttemptStatus>(latestAttempt?.Status);
            AiVerdict? verdict = latestAttempt is not null && resultByAttempt.TryGetValue(latestAttempt.Id, out var result)
                ? ParseNullable<AiVerdict>(result.Verdict)
                : null;
            eligibilityByInstrument.TryGetValue(technical.InstrumentId, out var eligibility);

            items.Add(new CandidateListItem(
                row.Id,
                technical.InstrumentId,
                identity.Code,
                identity.Name,
                Parse<PositionSide>(technical.PositionSide),
                row.Score,
                Parse<ConfidenceLevel>(row.Confidence),
                run.EvaluationBarDate,
                run.AnalyzedAtUtc,
                strategy is null ? "戦略不明" : $"{strategy.StrategyKey} {strategy.StrategyVersion}",
                row.PrimaryReason,
                aiStatus,
                verdict,
                latestAttempt?.ErrorMessage,
                technical.PositionSide == PositionSide.Short.ToString()
                    ? ParseNullable<OpenPermissionStatus>(eligibility?.ShortOpenStatus) ?? OpenPermissionStatus.Unknown
                    : null,
                eligibility?.Notes));
        }

        return items.OrderByDescending(x => x.Score).ThenBy(x => x.Code).ToList();
    }

    private static async Task<IReadOnlyList<TradeExecutionListItem>> LoadExecutionsAsync(
        SwingAdviserDbContext context,
        IReadOnlyDictionary<Guid, InstrumentIdentity> identities,
        CancellationToken cancellationToken)
    {
        var positions = await context.Set<PositionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var executionRows = await context.Set<TradeExecutionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var revisionRows = await context.Set<TradeExecutionRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var positionsById = positions.ToDictionary(x => x.Id);

        return executionRows
            .Where(x => positionsById.ContainsKey(x.PositionId))
            .Select(row =>
            {
                var position = positionsById[row.PositionId];
                var identity = identities.GetValueOrDefault(position.InstrumentId, InstrumentIdentity.Unknown);
                var revisions = revisionRows.Where(x => x.TradeExecutionId == row.Id)
                    .OrderBy(x => x.RevisionNo)
                    .Select(x => new TradeExecutionRevisionListItem(
                        x.Id,
                        x.RevisionNo,
                        Parse<ExecutionChangeKind>(x.ChangeKind),
                        Parse<RecordDisposition>(x.RecordDisposition),
                        x.ExecutedAtUtc,
                        x.Price,
                        x.Quantity,
                        x.Currency,
                        x.UserConfirmedAtUtc,
                        x.Broker,
                        x.ExternalReference,
                        x.UserNote,
                        x.CorrectionReason))
                    .ToList();
                return new TradeExecutionListItem(
                    row.Id,
                    position.Id,
                    position.InstrumentId,
                    identity.Code,
                    identity.Name,
                    Parse<PositionSide>(position.PositionSide),
                    Parse<ExecutionKind>(row.ExecutionKind),
                    Parse<ExecutionOrigin>(row.Origin),
                    revisions);
            })
            .Where(x => x.Revisions.Count != 0)
            .OrderByDescending(x => x.CurrentRevision.ExecutedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<PositionListItem>> LoadPositionsAsync(
        SwingAdviserDbContext context,
        IReadOnlyDictionary<Guid, InstrumentIdentity> identities,
        CancellationToken cancellationToken)
    {
        var positions = await context.Set<PositionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var states = await context.Set<PositionStateRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var executions = await context.Set<TradeExecutionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var executionRevisions = await context.Set<TradeExecutionRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var lots = await context.Set<MarginLotRow>().AsNoTracking().ToListAsync(cancellationToken);
        var allocations = await context.Set<LotAllocationRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var adjustments = await context.Set<PositionAdjustmentRow>().AsNoTracking().ToListAsync(cancellationToken);
        var contracts = await context.Set<MarginLotContractRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var evaluations = await context.Set<PositionEvaluationRow>().AsNoTracking().ToListAsync(cancellationToken);
        var riskBases = await context.Set<RiskBasisSnapshotRow>().AsNoTracking().ToListAsync(cancellationToken);
        var riskPlans = await context.Set<RiskPlanRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var prices = await context.Set<DailyPriceRow>().AsNoTracking().ToListAsync(cancellationToken);
        var priceRevisions = await context.Set<DailyPriceRevisionRow>().AsNoTracking().ToListAsync(cancellationToken);
        var strategies = await context.Set<StrategyParameterSnapshotRow>().AsNoTracking().ToListAsync(cancellationToken);
        var strategyById = strategies.ToDictionary(x => x.Id);
        var executionById = executions.ToDictionary(x => x.Id);

        var result = new List<PositionListItem>();
        foreach (var position in positions)
        {
            var state = Leaf(states.Where(x => x.PositionId == position.Id), x => x.Id, x => x.SupersedesId)
                .SingleOrDefault();
            if (state is null || state.Status != PositionStatus.Open.ToString())
            {
                continue;
            }

            var identity = identities.GetValueOrDefault(position.InstrumentId, InstrumentIdentity.Unknown);
            var lotItems = new List<MarginLotListItem>();
            decimal weightedBasis = 0;
            decimal totalQuantity = 0;
            var projectedReconciliationStatus = Parse<ReconciliationStatus>(state.ReconciliationStatus);
            MarginTermType termType = MarginTermType.Unknown;
            DateTimeOffset? finalRepayment = null;
            decimal? stopPrice = null;
            decimal? takeProfitPrice = null;

            foreach (var lot in lots.Where(x => x.PositionId == position.Id))
            {
                if (!executionById.TryGetValue(lot.OpeningTradeExecutionId, out var openingExecution))
                {
                    continue;
                }

                var openingRevision = Leaf(
                        executionRevisions.Where(x => x.TradeExecutionId == openingExecution.Id),
                        x => x.Id,
                        x => x.SupersedesId)
                    .SingleOrDefault();
                if (openingRevision is null || openingRevision.RecordDisposition != RecordDisposition.Effective.ToString())
                {
                    continue;
                }

                var currentLotQuantity = (decimal)openingRevision.Quantity;
                var currentBasisPrice = openingRevision.Price;
                var adjustmentLeaves = Leaf(
                        adjustments.Where(x => x.MarginLotId == lot.Id),
                        x => x.Id,
                        x => x.SupersedesId)
                    .OrderBy(x => x.EffectiveDate)
                    .ThenBy(x => x.RecordedAtUtc)
                    .ToList();
                foreach (var adjustment in adjustmentLeaves)
                {
                    if (adjustment.Status == PositionAdjustmentStatus.ReconciliationRequired.ToString() ||
                        adjustment.AfterQuantity is null || adjustment.AfterBasisPrice is null)
                    {
                        projectedReconciliationStatus = ReconciliationStatus.Required;
                        currentLotQuantity = adjustment.BeforeQuantity;
                        currentBasisPrice = adjustment.BeforeBasisPrice;
                        break;
                    }

                    if (adjustment.Status is nameof(PositionAdjustmentStatus.Applied) or nameof(PositionAdjustmentStatus.Resolved))
                    {
                        currentLotQuantity = adjustment.AfterQuantity.Value;
                        currentBasisPrice = adjustment.AfterBasisPrice.Value;
                    }
                }

                var allocated = Leaf(
                        allocations.Where(x => x.MarginLotId == lot.Id),
                        x => x.Id,
                        x => x.SupersedesId)
                    .Where(x => x.RecordDisposition == RecordDisposition.Effective.ToString())
                    .Sum(x => x.Quantity);
                var remaining = currentLotQuantity - allocated;
                if (remaining <= 0)
                {
                    continue;
                }

                totalQuantity += remaining;
                weightedBasis += currentBasisPrice * remaining;
                lotItems.Add(new MarginLotListItem(
                    lot.Id,
                    position.Id,
                    $"{identity.Code} / {openingRevision.ExecutedAtUtc.ToLocalTime():yyyy-MM-dd}建 / 残{remaining:#,0.####}株",
                    remaining,
                    openingRevision.ExecutedAtUtc));

                var contract = Leaf(contracts.Where(x => x.MarginLotId == lot.Id), x => x.Id, x => x.SupersedesId)
                    .SingleOrDefault();
                if (contract is not null)
                {
                    var parsedTerm = Parse<MarginTermType>(contract.TermType);
                    if (parsedTerm == MarginTermType.FixedDate &&
                        (finalRepayment is null || contract.FinalRepaymentAtUtc < finalRepayment))
                    {
                        termType = parsedTerm;
                        finalRepayment = contract.FinalRepaymentAtUtc;
                    }
                    else if (termType == MarginTermType.Unknown && parsedTerm == MarginTermType.NoFixedTerm)
                    {
                        termType = parsedTerm;
                    }
                }

                var basis = Leaf(riskBases.Where(x => x.MarginLotId == lot.Id), x => x.Id, x => x.SupersedesId)
                    .SingleOrDefault();
                if (basis is not null)
                {
                    var plan = Leaf(riskPlans.Where(x => x.RiskBasisSnapshotId == basis.Id), x => x.Id, x => x.SupersedesId)
                        .SingleOrDefault();
                    stopPrice ??= plan?.StopPrice ?? basis.InitialStopPrice;
                    takeProfitPrice ??= plan?.TakeProfitPrice ?? basis.InitialTakeProfitPrice;
                }
            }

            var evaluation = evaluations.Where(x => x.PositionId == position.Id)
                .OrderByDescending(x => x.EvaluationBarDate)
                .ThenByDescending(x => x.CreatedAtUtc)
                .FirstOrDefault();
            var latestPriceRow = prices.Where(x => x.InstrumentId == position.InstrumentId)
                .OrderByDescending(x => x.BarDate)
                .FirstOrDefault();
            var latestPrice = latestPriceRow is null
                ? null
                : Leaf(priceRevisions.Where(x => x.DailyPriceId == latestPriceRow.Id), x => x.Id, x => x.SupersedesId)
                    .Where(x => x.BarStatus != BarStatus.Invalid.ToString())
                    .Select(x => (decimal?)x.Close)
                    .SingleOrDefault();
            var strategyLabel = position.StrategyParameterSnapshotId is { } strategyId && strategyById.TryGetValue(strategyId, out var strategy)
                ? $"{strategy.StrategyKey} {strategy.StrategyVersion}"
                : "手動登録";

            result.Add(new PositionListItem(
                position.Id,
                position.InstrumentId,
                identity.Code,
                identity.Name,
                Parse<PositionSide>(position.PositionSide),
                totalQuantity,
                totalQuantity > 0 ? weightedBasis / totalQuantity : null,
                latestPrice,
                evaluation?.EvaluationBarDate,
                strategyLabel,
                ParseNullable<ExitDecision>(evaluation?.ExitDecision),
                evaluation?.ReasonSummary ?? "保有再評価結果なし",
                evaluation?.PricePnl,
                stopPrice,
                takeProfitPrice,
                termType,
                finalRepayment,
                projectedReconciliationStatus,
                lotItems));
        }

        return result.OrderBy(x => x.Code).ThenBy(x => x.Side).ToList();
    }

    private static async Task ValidateLotAllocationsAsync(
        SwingAdviserDbContext context,
        Guid positionId,
        IReadOnlyList<ManualLotAllocation> requested,
        CancellationToken cancellationToken)
    {
        var lots = await context.Set<MarginLotRow>()
            .Where(x => x.PositionId == positionId)
            .ToListAsync(cancellationToken);
        var requestedIds = requested.Select(x => x.MarginLotId).ToHashSet();
        if (!requestedIds.IsSubsetOf(lots.Select(x => x.Id)))
        {
            throw new InvalidOperationException("A selected lot does not belong to the position.");
        }

        var executions = await context.Set<TradeExecutionRow>()
            .Where(x => x.PositionId == positionId)
            .ToListAsync(cancellationToken);
        var executionIds = executions.Select(x => x.Id).ToHashSet();
        var revisions = await context.Set<TradeExecutionRevisionRow>()
            .Where(x => executionIds.Contains(x.TradeExecutionId))
            .ToListAsync(cancellationToken);
        var allocations = await context.Set<LotAllocationRevisionRow>()
            .Where(x => requestedIds.Contains(x.MarginLotId))
            .ToListAsync(cancellationToken);
        var adjustments = await context.Set<PositionAdjustmentRow>()
            .Where(x => requestedIds.Contains(x.MarginLotId))
            .ToListAsync(cancellationToken);

        foreach (var item in requested)
        {
            var lot = lots.Single(x => x.Id == item.MarginLotId);
            var opening = Leaf(revisions.Where(x => x.TradeExecutionId == lot.OpeningTradeExecutionId), x => x.Id, x => x.SupersedesId)
                .Single();
            var alreadyAllocated = Leaf(allocations.Where(x => x.MarginLotId == lot.Id), x => x.Id, x => x.SupersedesId)
                .Where(x => x.RecordDisposition == RecordDisposition.Effective.ToString())
                .Sum(x => x.Quantity);
            var currentQuantity = CurrentAdjustedQuantity(lot.Id, opening.Quantity, adjustments);
            if (opening.RecordDisposition != RecordDisposition.Effective.ToString() || item.Quantity > currentQuantity - alreadyAllocated)
            {
                throw new InvalidOperationException("A lot allocation exceeds its current remaining quantity.");
            }
        }
    }

    private static async Task<bool> IsPositionFullyClosedAsync(
        SwingAdviserDbContext context,
        Guid positionId,
        CancellationToken cancellationToken)
    {
        var lots = await context.Set<MarginLotRow>().Where(x => x.PositionId == positionId).ToListAsync(cancellationToken);
        foreach (var lot in lots)
        {
            var revisions = await context.Set<TradeExecutionRevisionRow>()
                .Where(x => x.TradeExecutionId == lot.OpeningTradeExecutionId)
                .ToListAsync(cancellationToken);
            var opening = Leaf(revisions, x => x.Id, x => x.SupersedesId).Single();
            var allocations = await context.Set<LotAllocationRevisionRow>()
                .Where(x => x.MarginLotId == lot.Id)
                .ToListAsync(cancellationToken);
            var adjustments = await context.Set<PositionAdjustmentRow>()
                .Where(x => x.MarginLotId == lot.Id)
                .ToListAsync(cancellationToken);
            var allocated = Leaf(allocations, x => x.Id, x => x.SupersedesId)
                .Where(x => x.RecordDisposition == RecordDisposition.Effective.ToString())
                .Sum(x => x.Quantity);
            var currentQuantity = CurrentAdjustedQuantity(lot.Id, opening.Quantity, adjustments);
            if (opening.RecordDisposition == RecordDisposition.Effective.ToString() && currentQuantity > allocated)
            {
                return false;
            }
        }

        return true;
    }

    private static decimal CurrentAdjustedQuantity(
        Guid marginLotId,
        long openingQuantity,
        IEnumerable<PositionAdjustmentRow> adjustments)
    {
        var quantity = (decimal)openingQuantity;
        foreach (var adjustment in Leaf(
                     adjustments.Where(x => x.MarginLotId == marginLotId),
                     x => x.Id,
                     x => x.SupersedesId)
                 .OrderBy(x => x.EffectiveDate)
                 .ThenBy(x => x.RecordedAtUtc))
        {
            if (adjustment.Status == PositionAdjustmentStatus.ReconciliationRequired.ToString() ||
                adjustment.AfterQuantity is null)
            {
                throw new InvalidOperationException("Lot allocation is unavailable while a corporate action requires reconciliation.");
            }

            if (adjustment.Status is nameof(PositionAdjustmentStatus.Applied) or nameof(PositionAdjustmentStatus.Resolved))
            {
                quantity = adjustment.AfterQuantity.Value;
            }
        }

        return quantity;
    }

    private static async Task AppendPositionStateAsync(
        SwingAdviserDbContext context,
        Guid positionId,
        PositionStatus? status,
        ReconciliationStatus reconciliationStatus,
        DateTimeOffset effectiveAtUtc,
        string reason,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        var states = await context.Set<PositionStateRevisionRow>()
            .Where(x => x.PositionId == positionId)
            .ToListAsync(cancellationToken);
        var leaf = Leaf(states, x => x.Id, x => x.SupersedesId).Single();
        var next = new PositionStateRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = leaf.RevisionNo + 1,
            SupersedesId = leaf.Id,
            PositionId = positionId,
            Status = (status ?? Parse<PositionStatus>(leaf.Status)).ToString(),
            ReconciliationStatus = reconciliationStatus.ToString(),
            EffectiveAtUtc = effectiveAtUtc,
            Reason = reason,
            RecordedAtUtc = recordedAtUtc,
        };
        next.ContentSha256 = Hash(new
        {
            next.PositionId,
            next.Status,
            next.ReconciliationStatus,
            next.EffectiveAtUtc,
            next.Reason,
        });
        context.Add(next);
    }

    private static TradeExecutionRevisionRow CreateExecutionRevision(
        Guid revisionId,
        Guid executionId,
        long revisionNo,
        Guid? supersedesId,
        DateTimeOffset executedAtUtc,
        decimal price,
        long quantity,
        string currency,
        ExecutionChangeKind changeKind,
        RecordDisposition disposition,
        DateTimeOffset userConfirmedAtUtc,
        string? broker,
        string? externalReference,
        string? userNote,
        string? correctionReason,
        DateTimeOffset recordedAtUtc)
    {
        var row = new TradeExecutionRevisionRow
        {
            Id = revisionId,
            RevisionNo = revisionNo,
            SupersedesId = supersedesId,
            TradeExecutionId = executionId,
            ExecutedAtUtc = executedAtUtc,
            Price = price,
            Quantity = quantity,
            Currency = currency,
            RecordDisposition = disposition.ToString(),
            ChangeKind = changeKind.ToString(),
            Broker = broker?.Trim(),
            ExternalReference = externalReference?.Trim(),
            UserNote = userNote?.Trim(),
            UserConfirmedAtUtc = userConfirmedAtUtc,
            CorrectionReason = correctionReason,
            RecordedAtUtc = recordedAtUtc,
        };
        row.ContentSha256 = Hash(new
        {
            row.TradeExecutionId,
            row.ExecutedAtUtc,
            row.Price,
            row.Quantity,
            row.Currency,
            row.RecordDisposition,
            row.ChangeKind,
            row.Broker,
            row.ExternalReference,
            row.UserNote,
            row.UserConfirmedAtUtc,
            row.CorrectionReason,
        });
        return row;
    }

    private static IReadOnlyDictionary<Guid, InstrumentIdentity> BuildIdentityMap(
        IEnumerable<InstrumentRow> instruments,
        IEnumerable<InstrumentIdentifierRow> identifiers,
        IEnumerable<InstrumentIdentifierRevisionRow> identifierRevisions,
        IEnumerable<InstrumentMasterRevisionRow> masters)
    {
        var identifierList = identifiers.ToList();
        var revisionList = identifierRevisions.ToList();
        var masterList = masters.ToList();
        return instruments.ToDictionary(
            instrument => instrument.Id,
            instrument =>
            {
                var code = identifierList.Where(x => x.InstrumentId == instrument.Id)
                    .SelectMany(identifier => Leaf(
                        revisionList.Where(x => x.InstrumentIdentifierId == identifier.Id),
                        x => x.Id,
                        x => x.SupersedesId))
                    .Where(x => x.RecordDisposition == RecordDisposition.Effective.ToString())
                    .OrderByDescending(x => x.RecordedAtUtc)
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? "コード不明";
                var name = Leaf(
                        masterList.Where(x => x.InstrumentId == instrument.Id),
                        x => x.Id,
                        x => x.SupersedesId)
                    .OrderByDescending(x => x.EffectiveFromDate)
                    .Select(x => x.Name)
                    .FirstOrDefault() ?? "銘柄名不明";
                return new InstrumentIdentity(code, name);
            });
    }

    private static IEnumerable<T> Leaf<T>(
        IEnumerable<T> rows,
        Func<T, Guid> id,
        Func<T, Guid?> supersedesId)
    {
        var materialized = rows.ToList();
        var superseded = materialized.Select(supersedesId).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        return materialized.Where(x => !superseded.Contains(id(x)));
    }

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Unknown {typeof(T).Name} value '{value}'.");

    private static T? ParseNullable<T>(string? value) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Parse<T>(value);

    private static string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private sealed record InstrumentIdentity(string Code, string Name)
    {
        public static InstrumentIdentity Unknown { get; } = new("コード不明", "銘柄名不明");
    }

    private sealed record InitialRiskInput(
        Guid? OriginCandidateResultId,
        Guid StrategyParameterSnapshotId,
        Guid AnalysisInputManifestId,
        DateOnly AtrReferenceBarDate,
        decimal FixedAtr,
        int AtrPeriod,
        string AtrAlgorithmId,
        string PriceCurrency,
        string PriceUnitBasisSha256);
}
