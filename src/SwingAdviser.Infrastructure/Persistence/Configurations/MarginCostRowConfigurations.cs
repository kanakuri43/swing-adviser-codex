using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal sealed class MarginCostItemRowConfiguration : IEntityTypeConfiguration<MarginCostItemRow>
{
    public void Configure(EntityTypeBuilder<MarginCostItemRow> builder)
    {
        builder.ToTable("margin_cost_items", table =>
        {
            table.HasCheckConstraint("ck_margin_cost_items_cost_type", "cost_type IN ('BuyerInterest', 'StockLendingFee', 'Backwardation', 'DividendEquivalent', 'BrokerSpecific', 'Other')");
            table.HasCheckConstraint("ck_margin_cost_items_period", "period_end_date >= period_start_date");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<MarginLotRow>().WithMany().HasForeignKey(row => row.MarginLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.MarginLotId, row.CostType, row.OccurrenceKey }).IsUnique();
        builder.HasIndex(row => new { row.MarginLotId, row.BrokerStatementLineId })
            .IsUnique()
            .HasFilter("broker_statement_line_id IS NOT NULL");
    }
}

internal sealed class MarginCostObservationRowConfiguration : IEntityTypeConfiguration<MarginCostObservationRow>
{
    public void Configure(EntityTypeBuilder<MarginCostObservationRow> builder)
    {
        builder.ToTable("margin_cost_observations", table =>
        {
            table.HasCheckConstraint("ck_margin_cost_observations_revision_no", "revision_no > 0");
            table.HasCheckConstraint("ck_margin_cost_observations_valuation_kind", "valuation_kind IN ('Estimate', 'Confirmed')");
            table.HasCheckConstraint("ck_margin_cost_observations_direction", "direction IN ('Charge', 'Credit')");
            table.HasCheckConstraint("ck_margin_cost_observations_amount_status", "amount_status IN ('KnownAmount', 'KnownZero', 'NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable')");
            table.HasCheckConstraint("ck_margin_cost_observations_amount", "(amount_status = 'KnownAmount' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) <> 0 AND currency IS NOT NULL) OR (amount_status = 'KnownZero' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) = 0 AND currency IS NOT NULL) OR (amount_status IN ('NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable') AND amount IS NULL AND currency IS NULL)");
            table.HasCheckConstraint("ck_margin_cost_observations_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
            table.HasCheckConstraint("ck_margin_cost_observations_included_days", "included_days IS NULL OR included_days >= 0");
            table.HasCheckConstraint("ck_margin_cost_observations_source_kind", "source_kind IN ('ApplicationEstimate', 'PublishedMarketData', 'BrokerStatement', 'UserEntry')");
            table.HasCheckConstraint("ck_margin_cost_observations_content_sha256", HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_margin_cost_observations_revision_chain", "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
            table.HasCheckConstraint("ck_margin_cost_observations_reconciliation_kind", "reconciles_estimate_id IS NULL OR valuation_kind = 'Confirmed'");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<MarginCostItemRow>().WithMany().HasForeignKey(row => row.MarginCostItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginCostObservationRow>().WithMany().HasForeignKey(row => row.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginCostObservationRow>().WithMany().HasForeignKey(row => row.ReconcilesEstimateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarginLotContractRevisionRow>().WithMany().HasForeignKey(row => row.MarginLotContractRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PublishedMarginCostRevisionRow>().WithMany().HasForeignKey(row => row.PublishedMarginCostRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(row => row.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.MarginCostItemId, row.ValuationKind, row.RevisionNo }).IsUnique();
        builder.HasIndex(row => row.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(row => row.ReconcilesEstimateId);
        builder.HasIndex(row => row.MarginLotContractRevisionId);
        builder.HasIndex(row => row.PublishedMarginCostRevisionId);
        builder.HasIndex(row => row.SourceArtifactId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class MarginCostAmountComponentRowConfiguration : IEntityTypeConfiguration<MarginCostAmountComponentRow>
{
    public void Configure(EntityTypeBuilder<MarginCostAmountComponentRow> builder)
    {
        builder.ToTable("margin_cost_amount_components", table =>
        {
            table.HasCheckConstraint("ck_margin_cost_amount_components_component_type", "component_type IN ('Gross', 'TaxEquivalent', 'Net', 'BrokerBooked', 'Other')");
            table.HasCheckConstraint("ck_margin_cost_amount_components_direction", "direction IN ('Charge', 'Credit')");
            table.HasCheckConstraint("ck_margin_cost_amount_components_amount_status", "amount_status IN ('KnownAmount', 'KnownZero', 'NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable')");
            table.HasCheckConstraint("ck_margin_cost_amount_components_amount", "(amount_status = 'KnownAmount' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) <> 0 AND currency IS NOT NULL) OR (amount_status = 'KnownZero' AND amount IS NOT NULL AND CAST(amount AS NUMERIC) = 0 AND currency IS NOT NULL) OR (amount_status IN ('NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown', 'NotApplicable') AND amount IS NULL AND currency IS NULL)");
            table.HasCheckConstraint("ck_margin_cost_amount_components_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
            table.HasCheckConstraint("ck_margin_cost_amount_components_ordinal", "ordinal >= 0");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<MarginCostObservationRow>().WithMany().HasForeignKey(row => row.MarginCostObservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.MarginCostObservationId, row.ComponentType }).IsUnique();
        builder.HasIndex(row => new { row.MarginCostObservationId, row.Ordinal }).IsUnique();
    }
}
