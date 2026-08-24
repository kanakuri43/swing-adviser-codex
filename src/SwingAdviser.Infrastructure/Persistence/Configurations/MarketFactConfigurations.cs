using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal sealed class DailyPriceConfiguration : IEntityTypeConfiguration<DailyPriceRow>
{
    public void Configure(EntityTypeBuilder<DailyPriceRow> builder)
    {
        builder.ToTable("daily_prices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.BarDate).HasColumnName("bar_date");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.BarDate, x.Provider }).IsUnique().HasDatabaseName("ux_daily_prices_instrument_id_bar_date_provider");
    }
}

internal sealed class DailyPriceRevisionConfiguration : IEntityTypeConfiguration<DailyPriceRevisionRow>
{
    public void Configure(EntityTypeBuilder<DailyPriceRevisionRow> builder)
    {
        builder.ToTable("daily_price_revisions", table =>
        {
            table.HasCheckConstraint("ck_daily_price_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_daily_price_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_daily_price_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_daily_price_revisions_prices_positive", "CAST(open AS NUMERIC) > 0 AND CAST(high AS NUMERIC) > 0 AND CAST(low AS NUMERIC) > 0 AND CAST(close AS NUMERIC) > 0");
            table.HasCheckConstraint("ck_daily_price_revisions_ohlc_range", "CAST(high AS NUMERIC) >= CAST(open AS NUMERIC) AND CAST(high AS NUMERIC) >= CAST(close AS NUMERIC) AND CAST(high AS NUMERIC) >= CAST(low AS NUMERIC) AND CAST(low AS NUMERIC) <= CAST(open AS NUMERIC) AND CAST(low AS NUMERIC) <= CAST(close AS NUMERIC)");
            table.HasCheckConstraint("ck_daily_price_revisions_volume", "volume >= 0");
            table.HasCheckConstraint("ck_daily_price_revisions_currency", "length(currency) = 3 AND currency = upper(currency)");
            table.HasCheckConstraint("ck_daily_price_revisions_bar_status", "bar_status IN ('Provisional', 'Confirmed', 'Corrected', 'Invalid')");
        });
        ConfigurationConventions.ConfigureRevision(builder, "daily_price_revisions");
        builder.Property(x => x.DailyPriceId).HasColumnName("daily_price_id");
        builder.Property(x => x.ProviderSymbol).HasColumnName("provider_symbol");
        builder.Property(x => x.Open).HasColumnName("open");
        builder.Property(x => x.High).HasColumnName("high");
        builder.Property(x => x.Low).HasColumnName("low");
        builder.Property(x => x.Close).HasColumnName("close");
        builder.Property(x => x.Volume).HasColumnName("volume");
        builder.Property(x => x.ProviderAdjclose).HasColumnName("provider_adjclose");
        builder.Property(x => x.Currency).HasColumnName("currency");
        builder.Property(x => x.BarStatus).HasColumnName("bar_status");
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id");
        builder.HasOne<DailyPriceRow>().WithMany().HasForeignKey(x => x.DailyPriceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DailyPriceId, x.RevisionNo }).IsUnique().HasDatabaseName("ux_daily_price_revisions_daily_price_id_revision_no");
    }
}

internal sealed class PriceHistoryAssessmentConfiguration : IEntityTypeConfiguration<PriceHistoryAssessmentRow>
{
    public void Configure(EntityTypeBuilder<PriceHistoryAssessmentRow> builder)
    {
        builder.ToTable("price_history_assessments", table =>
        {
            table.HasCheckConstraint("ck_price_history_assessments_bar_range", "last_valid_bar_date IS NULL OR first_valid_bar_date IS NULL OR last_valid_bar_date >= first_valid_bar_date");
            table.HasCheckConstraint("ck_price_history_assessments_valid_bar_count", "valid_bar_count >= 0");
            table.HasCheckConstraint("ck_price_history_assessments_completeness_status", "completeness_status IN ('CompleteFromListing', 'Incomplete', 'Unverified', 'Invalid')");
            table.HasCheckConstraint("ck_price_history_assessments_reason", "completeness_status = 'CompleteFromListing' OR reason IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.FirstValidBarDate).HasColumnName("first_valid_bar_date");
        builder.Property(x => x.LastValidBarDate).HasColumnName("last_valid_bar_date");
        builder.Property(x => x.ValidBarCount).HasColumnName("valid_bar_count");
        builder.Property(x => x.CompletenessStatus).HasColumnName("completeness_status");
        builder.Property(x => x.ListingDateEvidence).HasColumnName("listing_date_evidence");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.AssessedAtUtc).HasColumnName("assessed_at_utc");
        builder.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version");
        builder.Property(x => x.SourceArtifactId).HasColumnName("source_artifact_id");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(x => x.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InstrumentId).HasDatabaseName("ix_price_history_assessments_instrument_id");
        builder.HasIndex(x => x.SourceArtifactId).HasDatabaseName("ix_price_history_assessments_source_artifact_id");
    }
}

internal sealed class PriceRevisionSetConfiguration : IEntityTypeConfiguration<PriceRevisionSetRow>
{
    public void Configure(EntityTypeBuilder<PriceRevisionSetRow> builder)
    {
        builder.ToTable("price_revision_sets", table =>
        {
            table.HasCheckConstraint("ck_price_revision_sets_bar_count", "bar_count >= 0");
            table.HasCheckConstraint("ck_price_revision_sets_bar_range", "last_bar_date IS NULL OR first_bar_date IS NULL OR last_bar_date >= first_bar_date");
            table.HasCheckConstraint("ck_price_revision_sets_empty_range", "(bar_count = 0 AND first_bar_date IS NULL AND last_bar_date IS NULL) OR (bar_count > 0 AND first_bar_date IS NOT NULL AND last_bar_date IS NOT NULL)");
            table.HasCheckConstraint("ck_price_revision_sets_sha256", ConfigurationConventions.HashCheck("set_sha256"));
            table.HasCheckConstraint("ck_price_revision_sets_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.ParentSetId).HasColumnName("parent_set_id");
        builder.Property(x => x.FirstBarDate).HasColumnName("first_bar_date");
        builder.Property(x => x.LastBarDate).HasColumnName("last_bar_date");
        builder.Property(x => x.BarCount).HasColumnName("bar_count");
        builder.Property(x => x.SetSha256).HasColumnName("set_sha256");
        builder.Property(x => x.SelectorVersion).HasColumnName("selector_version");
        builder.Property(x => x.SelectedAvailableCutoffAtUtc).HasColumnName("selected_available_cutoff_at_utc");
        builder.Property(x => x.SelectedRecordedCutoffAtUtc).HasColumnName("selected_recorded_cutoff_at_utc");
        builder.Property(x => x.PointInTimeStatus).HasColumnName("point_in_time_status");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PriceRevisionSetRow>().WithMany().HasForeignKey(x => x.ParentSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.SetSha256 }).IsUnique().HasDatabaseName("ux_price_revision_sets_instrument_id_provider_set_sha256");
        builder.HasIndex(x => x.ParentSetId).HasDatabaseName("ix_price_revision_sets_parent_set_id");
    }
}

internal sealed class PriceRevisionSetChangeConfiguration : IEntityTypeConfiguration<PriceRevisionSetChangeRow>
{
    public void Configure(EntityTypeBuilder<PriceRevisionSetChangeRow> builder)
    {
        builder.ToTable("price_revision_set_changes", table =>
        {
            table.HasCheckConstraint("ck_price_revision_set_changes_operation", "operation IN ('Add', 'Replace', 'Remove')");
            table.HasCheckConstraint("ck_price_revision_set_changes_revisions", "(operation = 'Add' AND daily_price_revision_id IS NOT NULL AND replaced_daily_price_revision_id IS NULL) OR (operation = 'Replace' AND daily_price_revision_id IS NOT NULL AND replaced_daily_price_revision_id IS NOT NULL) OR (operation = 'Remove' AND daily_price_revision_id IS NULL AND replaced_daily_price_revision_id IS NOT NULL)");
            table.HasCheckConstraint("ck_price_revision_set_changes_ordinal", "ordinal >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PriceRevisionSetId).HasColumnName("price_revision_set_id");
        builder.Property(x => x.Operation).HasColumnName("operation");
        builder.Property(x => x.DailyPriceRevisionId).HasColumnName("daily_price_revision_id");
        builder.Property(x => x.ReplacedDailyPriceRevisionId).HasColumnName("replaced_daily_price_revision_id");
        builder.Property(x => x.BarDate).HasColumnName("bar_date");
        builder.Property(x => x.Ordinal).HasColumnName("ordinal");
        builder.HasOne<PriceRevisionSetRow>().WithMany().HasForeignKey(x => x.PriceRevisionSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DailyPriceRevisionRow>().WithMany().HasForeignKey(x => x.DailyPriceRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DailyPriceRevisionRow>().WithMany().HasForeignKey(x => x.ReplacedDailyPriceRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PriceRevisionSetId, x.Ordinal }).IsUnique().HasDatabaseName("ux_price_revision_set_changes_price_revision_set_id_ordinal");
        builder.HasIndex(x => new { x.PriceRevisionSetId, x.BarDate }).IsUnique().HasDatabaseName("ux_price_revision_set_changes_price_revision_set_id_bar_date");
        builder.HasIndex(x => x.DailyPriceRevisionId).HasDatabaseName("ix_price_revision_set_changes_daily_price_revision_id");
        builder.HasIndex(x => x.ReplacedDailyPriceRevisionId).HasDatabaseName("ix_price_revision_set_changes_replaced_daily_price_revision_id");
    }
}

internal sealed class CorporateActionConfiguration : IEntityTypeConfiguration<CorporateActionRow>
{
    public void Configure(EntityTypeBuilder<CorporateActionRow> builder)
    {
        builder.ToTable("corporate_actions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id");
        builder.Property(x => x.DerivedEventKey).HasColumnName("derived_event_key");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.SourceEventId }).IsUnique().HasFilter("\"source_event_id\" IS NOT NULL").HasDatabaseName("ux_corporate_actions_instrument_id_provider_source_event_id");
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.DerivedEventKey }).IsUnique().HasFilter("\"source_event_id\" IS NULL").HasDatabaseName("ux_corporate_actions_instrument_id_provider_derived_event_key");
    }
}

internal sealed class CorporateActionRevisionConfiguration : IEntityTypeConfiguration<CorporateActionRevisionRow>
{
    public void Configure(EntityTypeBuilder<CorporateActionRevisionRow> builder)
    {
        builder.ToTable("corporate_action_revisions", table =>
        {
            table.HasCheckConstraint("ck_corporate_action_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_corporate_action_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_corporate_action_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_corporate_action_revisions_action_type", "action_type IN ('Split', 'Consolidation', 'CashDividend', 'Unsupported')");
            table.HasCheckConstraint("ck_corporate_action_revisions_status", "status IN ('Announced', 'Confirmed', 'Corrected', 'Cancelled')");
            table.HasCheckConstraint("ck_corporate_action_revisions_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
            table.HasCheckConstraint("ck_corporate_action_revisions_details", "(action_type IN ('Split', 'Consolidation') AND ratio_numerator > 0 AND ratio_denominator > 0 AND cash_amount_per_share IS NULL AND currency IS NULL) OR (action_type = 'CashDividend' AND ratio_numerator IS NULL AND ratio_denominator IS NULL AND cash_amount_per_share IS NOT NULL AND CAST(cash_amount_per_share AS NUMERIC) >= 0 AND currency IS NOT NULL) OR (action_type = 'Unsupported' AND ratio_numerator IS NULL AND ratio_denominator IS NULL AND cash_amount_per_share IS NULL AND currency IS NULL)");
            table.HasCheckConstraint("ck_corporate_action_revisions_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
        });
        ConfigurationConventions.ConfigureRevision(builder, "corporate_action_revisions");
        builder.Property(x => x.CorporateActionId).HasColumnName("corporate_action_id");
        builder.Property(x => x.ActionType).HasColumnName("action_type");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        builder.Property(x => x.AnnouncedAtUtc).HasColumnName("announced_at_utc");
        builder.Property(x => x.RatioNumerator).HasColumnName("ratio_numerator");
        builder.Property(x => x.RatioDenominator).HasColumnName("ratio_denominator");
        builder.Property(x => x.CashAmountPerShare).HasColumnName("cash_amount_per_share");
        builder.Property(x => x.Currency).HasColumnName("currency");
        builder.Property(x => x.PointInTimeStatus).HasColumnName("point_in_time_status");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.HasOne<CorporateActionRow>().WithMany().HasForeignKey(x => x.CorporateActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CorporateActionId, x.RevisionNo }).IsUnique().HasDatabaseName("ux_corporate_action_revisions_corporate_action_id_revision_no");
    }
}

internal sealed class MarginEligibilityRecordConfiguration : IEntityTypeConfiguration<MarginEligibilityRecordRow>
{
    public void Configure(EntityTypeBuilder<MarginEligibilityRecordRow> builder)
    {
        builder.ToTable("margin_eligibility_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.SourceRecordKey).HasColumnName("source_record_key");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.SourceRecordKey }).IsUnique().HasDatabaseName("ux_margin_eligibility_records_instrument_id_provider_source_record_key");
    }
}

internal sealed class MarginEligibilityRevisionConfiguration : IEntityTypeConfiguration<MarginEligibilityRevisionRow>
{
    public void Configure(EntityTypeBuilder<MarginEligibilityRevisionRow> builder)
    {
        builder.ToTable("margin_eligibility_revisions", table =>
        {
            table.HasCheckConstraint("ck_margin_eligibility_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_margin_eligibility_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_margin_eligibility_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_margin_eligibility_revisions_effective_range", "effective_to_date IS NULL OR effective_to_date >= effective_from_date");
            table.HasCheckConstraint("ck_margin_eligibility_revisions_standardized_margin_status", "standardized_margin_status IN ('Eligible', 'Ineligible', 'Restricted', 'Unknown')");
            table.HasCheckConstraint("ck_margin_eligibility_revisions_loan_stock_status", "loan_stock_status IN ('Eligible', 'Ineligible', 'Restricted', 'Unknown')");
            table.HasCheckConstraint("ck_margin_eligibility_revisions_long_open_status", "long_open_status IN ('Allowed', 'Prohibited', 'Restricted', 'Unknown')");
            table.HasCheckConstraint("ck_margin_eligibility_revisions_short_open_status", "short_open_status IN ('Allowed', 'Prohibited', 'Restricted', 'Unknown')");
        });
        ConfigurationConventions.ConfigureRevision(builder, "margin_eligibility_revisions");
        builder.Property(x => x.MarginEligibilityRecordId).HasColumnName("margin_eligibility_record_id");
        builder.Property(x => x.EffectiveFromDate).HasColumnName("effective_from_date");
        builder.Property(x => x.EffectiveToDate).HasColumnName("effective_to_date");
        builder.Property(x => x.StandardizedMarginStatus).HasColumnName("standardized_margin_status");
        builder.Property(x => x.LoanStockStatus).HasColumnName("loan_stock_status");
        builder.Property(x => x.LongOpenStatus).HasColumnName("long_open_status");
        builder.Property(x => x.ShortOpenStatus).HasColumnName("short_open_status");
        builder.Property(x => x.RegulationCodesJson).HasColumnName("regulation_codes_json");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.HasOne<MarginEligibilityRecordRow>().WithMany().HasForeignKey(x => x.MarginEligibilityRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MarginEligibilityRecordId, x.RevisionNo }).IsUnique().HasDatabaseName("ux_margin_eligibility_revisions_margin_eligibility_record_id_revision_no");
    }
}

internal sealed class PublishedMarginCostConfiguration : IEntityTypeConfiguration<PublishedMarginCostRow>
{
    public void Configure(EntityTypeBuilder<PublishedMarginCostRow> builder)
    {
        builder.ToTable("published_margin_costs", table => table.HasCheckConstraint("ck_published_margin_costs_cost_type", "cost_type IN ('Backwardation')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.CostType).HasColumnName("cost_type");
        builder.Property(x => x.SourceRecordKey).HasColumnName("source_record_key");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.CostType, x.SourceRecordKey }).IsUnique().HasDatabaseName("ux_published_margin_costs_instrument_id_provider_cost_type_source_record_key");
    }
}

internal sealed class PublishedMarginCostRevisionConfiguration : IEntityTypeConfiguration<PublishedMarginCostRevisionRow>
{
    public void Configure(EntityTypeBuilder<PublishedMarginCostRevisionRow> builder)
    {
        builder.ToTable("published_margin_cost_revisions", table =>
        {
            table.HasCheckConstraint("ck_published_margin_cost_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_published_margin_cost_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_published_margin_cost_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_published_margin_cost_revisions_period", "period_end_date IS NULL OR period_start_date IS NULL OR period_end_date >= period_start_date");
            table.HasCheckConstraint("ck_published_margin_cost_revisions_included_days", "included_days IS NULL OR included_days >= 0");
            table.HasCheckConstraint("ck_published_margin_cost_revisions_publication_status", "publication_status IN ('KnownAmount', 'KnownZero', 'NotOccurred', 'Unpublished', 'FetchFailed', 'Unknown')");
            table.HasCheckConstraint("ck_published_margin_cost_revisions_amount", "(publication_status = 'KnownAmount' AND amount_per_share IS NOT NULL AND CAST(amount_per_share AS NUMERIC) > 0 AND currency IS NOT NULL) OR (publication_status = 'KnownZero' AND amount_per_share IS NOT NULL AND CAST(amount_per_share AS NUMERIC) = 0 AND currency IS NOT NULL) OR (publication_status NOT IN ('KnownAmount', 'KnownZero') AND amount_per_share IS NULL AND currency IS NULL)");
            table.HasCheckConstraint("ck_published_margin_cost_revisions_currency", "currency IS NULL OR (length(currency) = 3 AND currency = upper(currency))");
        });
        ConfigurationConventions.ConfigureRevision(builder, "published_margin_cost_revisions");
        builder.Property(x => x.PublishedMarginCostId).HasColumnName("published_margin_cost_id");
        builder.Property(x => x.ApplicationDate).HasColumnName("application_date");
        builder.Property(x => x.PeriodStartDate).HasColumnName("period_start_date");
        builder.Property(x => x.PeriodEndDate).HasColumnName("period_end_date");
        builder.Property(x => x.IncludedDays).HasColumnName("included_days");
        builder.Property(x => x.PublicationStatus).HasColumnName("publication_status");
        builder.Property(x => x.AmountPerShare).HasColumnName("amount_per_share");
        builder.Property(x => x.Currency).HasColumnName("currency");
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(x => x.Unit).HasColumnName("unit");
        builder.HasOne<PublishedMarginCostRow>().WithMany().HasForeignKey(x => x.PublishedMarginCostId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PublishedMarginCostId, x.RevisionNo }).IsUnique().HasDatabaseName("ux_published_margin_cost_revisions_published_margin_cost_id_revision_no");
    }
}

internal sealed class FundamentalRecordConfiguration : IEntityTypeConfiguration<FundamentalRecordRow>
{
    public void Configure(EntityTypeBuilder<FundamentalRecordRow> builder)
    {
        builder.ToTable("fundamental_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.SourceRecordKey).HasColumnName("source_record_key");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.SourceRecordKey }).IsUnique().HasDatabaseName("ux_fundamental_records_instrument_id_provider_source_record_key");
    }
}

internal sealed class FundamentalRevisionConfiguration : IEntityTypeConfiguration<FundamentalRevisionRow>
{
    public void Configure(EntityTypeBuilder<FundamentalRevisionRow> builder)
    {
        builder.ToTable("fundamental_revisions", table =>
        {
            table.HasCheckConstraint("ck_fundamental_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_fundamental_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_fundamental_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_fundamental_revisions_market_cap", "market_cap IS NULL OR CAST(market_cap AS NUMERIC) >= 0");
            table.HasCheckConstraint("ck_fundamental_revisions_currency", "(market_cap IS NULL AND currency IS NULL) OR (market_cap IS NOT NULL AND currency IS NOT NULL AND length(currency) = 3 AND currency = upper(currency))");
        });
        ConfigurationConventions.ConfigureRevision(builder, "fundamental_revisions");
        builder.Property(x => x.FundamentalRecordId).HasColumnName("fundamental_record_id");
        builder.Property(x => x.AsOfDate).HasColumnName("as_of_date");
        builder.Property(x => x.FiscalPeriodEndDate).HasColumnName("fiscal_period_end_date");
        builder.Property(x => x.Per).HasColumnName("per");
        builder.Property(x => x.Pbr).HasColumnName("pbr");
        builder.Property(x => x.MarketCap).HasColumnName("market_cap");
        builder.Property(x => x.Currency).HasColumnName("currency");
        builder.Property(x => x.MissingFieldsJson).HasColumnName("missing_fields_json");
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json");
        builder.HasOne<FundamentalRecordRow>().WithMany().HasForeignKey(x => x.FundamentalRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FundamentalRecordId, x.RevisionNo }).IsUnique().HasDatabaseName("ux_fundamental_revisions_fundamental_record_id_revision_no");
    }
}
