using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal sealed class PositionRowConfiguration : IEntityTypeConfiguration<PositionRow>
{
    public void Configure(EntityTypeBuilder<PositionRow> builder)
    {
        builder.ToTable("positions", table =>
            table.HasCheckConstraint("ck_positions_position_side", "position_side IN ('Long', 'Short')"));
        builder.HasKey(row => row.Id);
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(row => row.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StrategyParameterSnapshotRow>().WithMany().HasForeignKey(row => row.StrategyParameterSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CandidateResultRow>().WithMany().HasForeignKey(row => row.OriginCandidateResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.InstrumentId);
        builder.HasIndex(row => row.StrategyParameterSnapshotId);
        builder.HasIndex(row => row.OriginCandidateResultId);
    }
}

internal sealed class PositionStateRevisionRowConfiguration : IEntityTypeConfiguration<PositionStateRevisionRow>
{
    public void Configure(EntityTypeBuilder<PositionStateRevisionRow> builder)
    {
        builder.ToTable("position_state_revisions", table =>
        {
            table.HasCheckConstraint("ck_position_state_revisions_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_position_state_revisions_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_position_state_revisions_status", "status IN ('Open', 'Closed', 'Archived')");
            table.HasCheckConstraint("ck_position_state_revisions_reconciliation_status", "reconciliation_status IN ('Clear', 'Required', 'InProgress', 'Resolved')");
            table.HasCheckConstraint("ck_position_state_revisions_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<PositionStateRevisionRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PositionRow>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.PositionId, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class TradeExecutionRowConfiguration : IEntityTypeConfiguration<TradeExecutionRow>
{
    public void Configure(EntityTypeBuilder<TradeExecutionRow> builder)
    {
        builder.ToTable("trade_executions", table =>
        {
            table.HasCheckConstraint("ck_trade_executions_execution_kind", "execution_kind IN ('Open', 'Close')");
            table.HasCheckConstraint("ck_trade_executions_origin", "origin = 'UserConfirmed'");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<PositionRow>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CandidateResultRow>().WithMany().HasForeignKey(row => row.CandidateContextId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.PositionId);
        builder.HasIndex(row => row.CandidateContextId);
    }
}

internal sealed class TradeExecutionRevisionRowConfiguration : IEntityTypeConfiguration<TradeExecutionRevisionRow>
{
    public void Configure(EntityTypeBuilder<TradeExecutionRevisionRow> builder)
    {
        builder.ToTable("trade_execution_revisions", table =>
        {
            table.HasCheckConstraint("ck_trade_execution_revisions_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_trade_execution_revisions_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_trade_execution_revisions_price", "CAST(price AS NUMERIC) > 0");
            table.HasCheckConstraint("ck_trade_execution_revisions_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_trade_execution_revisions_currency", "length(currency) = 3 AND currency = upper(currency)");
            table.HasCheckConstraint("ck_trade_execution_revisions_record_disposition", "record_disposition IN ('Effective', 'Voided')");
            table.HasCheckConstraint("ck_trade_execution_revisions_change_kind", "change_kind IN ('Initial', 'Correction', 'Void')");
            table.HasCheckConstraint("ck_trade_execution_revisions_disposition_change", "(change_kind = 'Void' AND record_disposition = 'Voided') OR (change_kind IN ('Initial', 'Correction') AND record_disposition = 'Effective')");
            table.HasCheckConstraint("ck_trade_execution_revisions_correction_reason", "(revision_no = 1 AND record_disposition = 'Effective' AND change_kind = 'Initial') OR (revision_no > 1 AND correction_reason IS NOT NULL)");
            table.HasCheckConstraint("ck_trade_execution_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND change_kind = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND change_kind IN ('Correction', 'Void'))");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<TradeExecutionRevisionRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(row => row.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRow>().WithMany().HasForeignKey(row => row.TradeExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.TradeExecutionId, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.SourceArtifactId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class MarginLotRowConfiguration : IEntityTypeConfiguration<MarginLotRow>
{
    public void Configure(EntityTypeBuilder<MarginLotRow> builder)
    {
        builder.ToTable("margin_lots");
        builder.HasKey(row => row.Id);
        builder.HasOne<PositionRow>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRow>().WithMany().HasForeignKey(row => row.OpeningTradeExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRevisionRow>().WithMany().HasForeignKey(row => row.InitialOpeningTradeExecutionRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.PositionId);
        builder.HasIndex(row => row.OpeningTradeExecutionId).IsUnique();
        builder.HasIndex(row => row.InitialOpeningTradeExecutionRevisionId).IsUnique();
    }
}

internal sealed class MarginLotContractRevisionRowConfiguration : IEntityTypeConfiguration<MarginLotContractRevisionRow>
{
    public void Configure(EntityTypeBuilder<MarginLotContractRevisionRow> builder)
    {
        builder.ToTable("margin_lot_contract_revisions", table =>
        {
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_margin_type", "margin_type IN ('Standardized', 'General', 'Unknown')");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_term_type", "term_type IN ('FixedDate', 'NoFixedTerm', 'Unknown')");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_term_deadline", "(term_type = 'FixedDate' AND final_repayment_at_utc IS NOT NULL) OR (term_type IN ('NoFixedTerm', 'Unknown') AND final_repayment_at_utc IS NULL)");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_effective_dates", "effective_to_date IS NULL OR effective_to_date >= effective_from_date");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_currency", "length(contract_currency) = 3 AND contract_currency = upper(contract_currency)");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_change_kind", "change_kind IN ('Initial', 'ContractAmendment', 'InputCorrection')");
            table.HasCheckConstraint("ck_margin_lot_contract_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND change_kind = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND change_kind IN ('ContractAmendment', 'InputCorrection'))");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<MarginLotContractRevisionRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(row => row.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginLotRow>().WithMany().HasForeignKey(row => row.MarginLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRevisionRow>().WithMany().HasForeignKey(row => row.OpeningTradeExecutionRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.MarginLotId, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.SourceArtifactId);
        builder.HasIndex(row => row.OpeningTradeExecutionRevisionId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class LotAllocationRevisionRowConfiguration : IEntityTypeConfiguration<LotAllocationRevisionRow>
{
    public void Configure(EntityTypeBuilder<LotAllocationRevisionRow> builder)
    {
        builder.ToTable("lot_allocation_revisions", table =>
        {
            table.HasCheckConstraint("ck_lot_allocation_revisions_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_lot_allocation_revisions_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_lot_allocation_revisions_record_disposition", "record_disposition IN ('Effective', 'Voided')");
            table.HasCheckConstraint("ck_lot_allocation_revisions_change_kind", "change_kind IN ('Initial', 'Correction', 'Void')");
            table.HasCheckConstraint("ck_lot_allocation_revisions_disposition_change", "(change_kind = 'Void' AND record_disposition = 'Voided') OR (change_kind IN ('Initial', 'Correction') AND record_disposition = 'Effective')");
            table.HasCheckConstraint("ck_lot_allocation_revisions_correction_reason", "(revision_no = 1 AND record_disposition = 'Effective' AND change_kind = 'Initial') OR (revision_no > 1 AND correction_reason IS NOT NULL)");
            table.HasCheckConstraint("ck_lot_allocation_revisions_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_lot_allocation_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND change_kind = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND change_kind IN ('Correction', 'Void'))");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<LotAllocationRevisionRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRow>().WithMany().HasForeignKey(row => row.ClosingTradeExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRevisionRow>().WithMany().HasForeignKey(row => row.ClosingTradeExecutionRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginLotRow>().WithMany().HasForeignKey(row => row.MarginLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AllocationKey, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.ClosingTradeExecutionId);
        builder.HasIndex(row => row.ClosingTradeExecutionRevisionId);
        builder.HasIndex(row => row.MarginLotId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class PositionAdjustmentRowConfiguration : IEntityTypeConfiguration<PositionAdjustmentRow>
{
    public void Configure(EntityTypeBuilder<PositionAdjustmentRow> builder)
    {
        builder.ToTable("position_adjustments", table =>
        {
            table.HasCheckConstraint("ck_position_adjustments_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_position_adjustments_status", "status IN ('Applied', 'ReconciliationRequired', 'Resolved', 'Reversed')");
            table.HasCheckConstraint("ck_position_adjustments_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_position_adjustments_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<PositionAdjustmentRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PositionRow>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginLotRow>().WithMany().HasForeignKey(row => row.MarginLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CorporateActionRevisionRow>().WithMany().HasForeignKey(row => row.CorporateActionRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AdjustmentKey, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.PositionId);
        builder.HasIndex(row => row.MarginLotId);
        builder.HasIndex(row => row.CorporateActionRevisionId);
        builder.HasIndex(row => new { row.MarginLotId, row.CorporateActionRevisionId })
            .IsUnique()
            .HasFilter("revision_no = 1");
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class RiskBasisSnapshotRowConfiguration : IEntityTypeConfiguration<RiskBasisSnapshotRow>
{
    public void Configure(EntityTypeBuilder<RiskBasisSnapshotRow> builder)
    {
        builder.ToTable("risk_basis_snapshots", table =>
        {
            table.HasCheckConstraint("ck_risk_basis_snapshots_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_risk_basis_snapshots_fixed_atr", "CAST(fixed_atr AS NUMERIC) > 0");
            table.HasCheckConstraint("ck_risk_basis_snapshots_atr_period", "atr_period > 0");
            table.HasCheckConstraint("ck_risk_basis_snapshots_price_currency", "price_currency IS NULL OR length(price_currency) = 3");
            table.HasCheckConstraint("ck_risk_basis_snapshots_price_unit_basis_sha256", "price_unit_basis_sha256 IS NULL OR (length(price_unit_basis_sha256) = 64 AND price_unit_basis_sha256 NOT GLOB '*[^0-9a-f]*')");
            table.HasCheckConstraint("ck_risk_basis_snapshots_partial_fraction", "CAST(partial_take_profit_fraction AS NUMERIC) > 0 AND CAST(partial_take_profit_fraction AS NUMERIC) <= 1");
            table.HasCheckConstraint("ck_risk_basis_snapshots_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_risk_basis_snapshots_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<RiskBasisSnapshotRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginLotRow>().WithMany().HasForeignKey(row => row.MarginLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRevisionRow>().WithMany().HasForeignKey(row => row.OpeningTradeExecutionRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CandidateResultRow>().WithMany().HasForeignKey(row => row.OriginCandidateResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StrategyParameterSnapshotRow>().WithMany().HasForeignKey(row => row.StrategyParameterSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnalysisInputManifestRow>().WithMany().HasForeignKey(row => row.AnalysisInputManifestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.MarginLotId, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.OpeningTradeExecutionRevisionId);
        builder.HasIndex(row => row.OriginCandidateResultId);
        builder.HasIndex(row => row.StrategyParameterSnapshotId);
        builder.HasIndex(row => row.AnalysisInputManifestId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class RiskPlanRevisionRowConfiguration : IEntityTypeConfiguration<RiskPlanRevisionRow>
{
    public void Configure(EntityTypeBuilder<RiskPlanRevisionRow> builder)
    {
        builder.ToTable("risk_plan_revisions", table =>
        {
            table.HasCheckConstraint("ck_risk_plan_revisions_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_risk_plan_revisions_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_risk_plan_revisions_plan_reason", "plan_reason IN ('Initial', 'PartialExitBreakeven', 'CorporateActionConversion', 'UserCorrection')");
            table.HasCheckConstraint("ck_risk_plan_revisions_cost_adjusted", "is_cost_adjusted = 0");
            table.HasCheckConstraint("ck_risk_plan_revisions_triggers", "(plan_reason = 'PartialExitBreakeven' AND trigger_trade_execution_id IS NOT NULL AND trigger_lot_allocation_revision_id IS NOT NULL AND trigger_position_adjustment_id IS NULL) OR (plan_reason = 'CorporateActionConversion' AND trigger_position_adjustment_id IS NOT NULL AND trigger_trade_execution_id IS NULL AND trigger_lot_allocation_revision_id IS NULL) OR (plan_reason IN ('Initial', 'UserCorrection') AND trigger_trade_execution_id IS NULL AND trigger_lot_allocation_revision_id IS NULL AND trigger_position_adjustment_id IS NULL)");
            table.HasCheckConstraint("ck_risk_plan_revisions_revision_kind", "(revision_no = 1 AND supersedes_id IS NULL AND plan_reason = 'Initial') OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id AND plan_reason <> 'Initial')");
        });
        builder.HasKey(row => row.Id);
        builder.Property(row => row.IsCostAdjusted).HasDefaultValue(false);
        builder.HasOne<RiskPlanRevisionRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RiskBasisSnapshotRow>().WithMany().HasForeignKey(row => row.RiskBasisSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TradeExecutionRow>().WithMany().HasForeignKey(row => row.TriggerTradeExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LotAllocationRevisionRow>().WithMany().HasForeignKey(row => row.TriggerLotAllocationRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PositionAdjustmentRow>().WithMany().HasForeignKey(row => row.TriggerPositionAdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.RiskBasisSnapshotId, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.TriggerTradeExecutionId);
        builder.HasIndex(row => row.TriggerLotAllocationRevisionId);
        builder.HasIndex(row => row.TriggerPositionAdjustmentId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class PositionEvaluationInputManifestRowConfiguration : IEntityTypeConfiguration<PositionEvaluationInputManifestRow>
{
    public void Configure(EntityTypeBuilder<PositionEvaluationInputManifestRow> builder)
    {
        builder.ToTable("position_evaluation_input_manifests", table =>
            table.HasCheckConstraint("ck_position_evaluation_input_manifests_manifest_sha256", HashCheck("manifest_sha256")));
        builder.HasKey(row => row.Id);
        builder.HasOne<AnalysisRunRow>().WithMany().HasForeignKey(row => row.AnalysisRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PositionRow>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnalysisInputManifestRow>().WithMany().HasForeignKey(row => row.AnalysisInputManifestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DailyPriceRevisionRow>().WithMany().HasForeignKey(row => row.CurrentPriceRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AnalysisRunId, row.PositionId }).IsUnique();
        builder.HasIndex(row => row.PositionId);
        builder.HasIndex(row => row.AnalysisInputManifestId);
        builder.HasIndex(row => row.CurrentPriceRevisionId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class PositionEvaluationRowConfiguration : IEntityTypeConfiguration<PositionEvaluationRow>
{
    public void Configure(EntityTypeBuilder<PositionEvaluationRow> builder)
    {
        builder.ToTable("position_evaluations", table =>
        {
            table.HasCheckConstraint("ck_position_evaluations_outcome", "evaluation_outcome IN ('Evaluated', 'InsufficientHistory', 'HistoryIncomplete', 'InvalidData', 'PointInTimeUnverified', 'ReconciliationRequired', 'IncompletePositionData', 'IntradaySequenceUnknown', 'Failed')");
            table.HasCheckConstraint("ck_position_evaluations_exit_decision", "exit_decision IS NULL OR exit_decision IN ('Hold', 'TakeProfit', 'StopLoss', 'Exit')");
            table.HasCheckConstraint("ck_position_evaluations_outcome_decision", "(evaluation_outcome = 'Evaluated' AND exit_decision IS NOT NULL) OR (evaluation_outcome <> 'Evaluated' AND exit_decision IS NULL)");
            table.HasCheckConstraint("ck_position_evaluations_current_quantity", "current_quantity IS NULL OR CAST(current_quantity AS NUMERIC) >= 0");
            table.HasCheckConstraint("ck_position_evaluations_partial_exit_status", "partial_exit_status IN ('NotApplicable', 'Candidate', 'NotFeasible')");
            table.HasCheckConstraint("ck_position_evaluations_partial_exit_quantity", "(partial_exit_status = 'Candidate' AND partial_exit_quantity > 0) OR (partial_exit_status IN ('NotApplicable', 'NotFeasible') AND partial_exit_quantity IS NULL)");
            table.HasCheckConstraint("ck_position_evaluations_fail_closed_partial_exit", "evaluation_outcome = 'Evaluated' OR (partial_exit_status = 'NotApplicable' AND partial_exit_quantity IS NULL)");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<AnalysisRunRow>().WithMany().HasForeignKey(row => row.AnalysisRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PositionRow>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PositionEvaluationInputManifestRow>().WithMany().HasForeignKey(row => row.PositionEvaluationInputManifestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AnalysisRunId, row.PositionId }).IsUnique();
        builder.HasIndex(row => row.PositionId);
        builder.HasIndex(row => row.PositionEvaluationInputManifestId).IsUnique();
    }
}
