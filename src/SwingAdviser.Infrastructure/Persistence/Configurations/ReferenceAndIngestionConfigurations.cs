using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal sealed class InstrumentConfiguration : IEntityTypeConfiguration<InstrumentRow>
{
    public void Configure(EntityTypeBuilder<InstrumentRow> builder)
    {
        builder.ToTable("instruments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
    }
}

internal sealed class InstrumentIdentifierConfiguration : IEntityTypeConfiguration<InstrumentIdentifierRow>
{
    public void Configure(EntityTypeBuilder<InstrumentIdentifierRow> builder)
    {
        builder.ToTable("instrument_identifiers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Scheme).HasColumnName("scheme");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InstrumentId).HasDatabaseName("ix_instrument_identifiers_instrument_id");
    }
}

internal sealed class InstrumentIdentifierRevisionConfiguration : IEntityTypeConfiguration<InstrumentIdentifierRevisionRow>
{
    public void Configure(EntityTypeBuilder<InstrumentIdentifierRevisionRow> builder)
    {
        builder.ToTable("instrument_identifier_revisions", table =>
        {
            table.HasCheckConstraint("ck_instrument_identifier_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_instrument_identifier_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_instrument_identifier_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_instrument_identifier_revisions_valid_range", "valid_to_date IS NULL OR valid_from_date IS NULL OR valid_to_date >= valid_from_date");
            table.HasCheckConstraint("ck_instrument_identifier_revisions_record_disposition", "record_disposition IN ('Effective', 'Voided')");
            table.HasCheckConstraint("ck_instrument_identifier_revisions_change_kind", "change_kind IN ('Initial', 'ValidityChange', 'Correction', 'Void')");
        });
        ConfigurationConventions.ConfigureRevision(builder, "instrument_identifier_revisions");
        builder.Property(x => x.InstrumentIdentifierId).HasColumnName("instrument_identifier_id");
        builder.Property(x => x.Value).HasColumnName("value");
        builder.Property(x => x.ValidFromDate).HasColumnName("valid_from_date");
        builder.Property(x => x.ValidToDate).HasColumnName("valid_to_date");
        builder.Property(x => x.RecordDisposition).HasColumnName("record_disposition");
        builder.Property(x => x.ChangeKind).HasColumnName("change_kind");
        builder.HasOne<InstrumentIdentifierRow>().WithMany().HasForeignKey(x => x.InstrumentIdentifierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentIdentifierId, x.RevisionNo }).IsUnique().HasDatabaseName("ux_instrument_identifier_revisions_instrument_identifier_id_revision_no");
        builder.HasIndex(x => new { x.Value, x.InstrumentIdentifierId }).HasDatabaseName("ix_instrument_identifier_revisions_value_instrument_identifier_id");
    }
}

internal sealed class InstrumentMasterRevisionConfiguration : IEntityTypeConfiguration<InstrumentMasterRevisionRow>
{
    public void Configure(EntityTypeBuilder<InstrumentMasterRevisionRow> builder)
    {
        builder.ToTable("instrument_master_revisions", table =>
        {
            table.HasCheckConstraint("ck_instrument_master_revisions_revision_no", "revision_no >= 1");
            table.HasCheckConstraint("ck_instrument_master_revisions_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_instrument_master_revisions_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_instrument_master_revisions_effective_range", "effective_to_date IS NULL OR effective_to_date >= effective_from_date");
            table.HasCheckConstraint("ck_instrument_master_revisions_security_type", "security_type IN ('DomesticCommonStock', 'ETF', 'ETN', 'REIT', 'Preferred', 'Foreign', 'Other', 'Unknown')");
            table.HasCheckConstraint("ck_instrument_master_revisions_trading_unit", "trading_unit IS NULL OR trading_unit > 0");
            table.HasCheckConstraint("ck_instrument_master_revisions_currency", "length(currency) = 3 AND currency = upper(currency)");
            table.HasCheckConstraint("ck_instrument_master_revisions_listing_range", "delisting_date IS NULL OR listing_date IS NULL OR delisting_date >= listing_date");
            table.HasCheckConstraint("ck_instrument_master_revisions_listing_status", "listing_status IN ('Listed', 'DelistingScheduled', 'Delisted', 'Unknown')");
            table.HasCheckConstraint("ck_instrument_master_revisions_scan_eligibility", "scan_eligibility IN ('Eligible', 'Excluded', 'Unknown')");
            table.HasCheckConstraint("ck_instrument_master_revisions_exclusion_reason", "(scan_eligibility = 'Excluded' AND exclusion_reason IS NOT NULL) OR (scan_eligibility <> 'Excluded')");
            table.HasCheckConstraint("ck_instrument_master_revisions_change_kind", "change_kind IN ('EffectiveSnapshot', 'Correction', 'Cancellation')");
        });
        ConfigurationConventions.ConfigureRevision(builder, "instrument_master_revisions");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.EffectiveFromDate).HasColumnName("effective_from_date");
        builder.Property(x => x.EffectiveToDate).HasColumnName("effective_to_date");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.ExchangeCode).HasColumnName("exchange_code");
        builder.Property(x => x.MarketSegment).HasColumnName("market_segment");
        builder.Property(x => x.SecurityType).HasColumnName("security_type");
        builder.Property(x => x.TradingUnit).HasColumnName("trading_unit");
        builder.Property(x => x.Currency).HasColumnName("currency");
        builder.Property(x => x.ListingDate).HasColumnName("listing_date");
        builder.Property(x => x.DelistingDate).HasColumnName("delisting_date");
        builder.Property(x => x.ListingStatus).HasColumnName("listing_status");
        builder.Property(x => x.ScanEligibility).HasColumnName("scan_eligibility");
        builder.Property(x => x.ExclusionReason).HasColumnName("exclusion_reason");
        builder.Property(x => x.ChangeKind).HasColumnName("change_kind");
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstrumentId, x.Provider, x.RevisionNo }).IsUnique().HasDatabaseName("ux_instrument_master_revisions_instrument_id_provider_revision_no");
    }
}

internal sealed class MarketCalendarVersionConfiguration : IEntityTypeConfiguration<MarketCalendarVersionRow>
{
    public void Configure(EntityTypeBuilder<MarketCalendarVersionRow> builder)
    {
        builder.ToTable("market_calendar_versions", table => table.HasCheckConstraint("ck_market_calendar_versions_sha256", ConfigurationConventions.HashCheck("content_sha256")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MarketCode).HasColumnName("market_code");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.VersionName).HasColumnName("version_name");
        builder.Property(x => x.TimeZoneId).HasColumnName("time_zone_id");
        builder.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version");
        builder.Property(x => x.ContentSha256).HasColumnName("content_sha256");
        builder.Property(x => x.SourceArtifactId).HasColumnName("source_artifact_id");
        builder.Property(x => x.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(x => x.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MarketCode, x.ContentSha256 }).IsUnique().HasDatabaseName("ux_market_calendar_versions_market_code_content_sha256");
        builder.HasIndex(x => new { x.MarketCode, x.VersionName }).IsUnique().HasDatabaseName("ux_market_calendar_versions_market_code_version_name");
        builder.HasIndex(x => x.SourceArtifactId).HasDatabaseName("ix_market_calendar_versions_source_artifact_id");
    }
}

internal sealed class MarketCalendarDayConfiguration : IEntityTypeConfiguration<MarketCalendarDayRow>
{
    public void Configure(EntityTypeBuilder<MarketCalendarDayRow> builder)
    {
        builder.ToTable("market_calendar_days", table => table.HasCheckConstraint("ck_market_calendar_days_session_status", "session_status IN ('Open', 'Closed', 'HalfDay', 'UnscheduledClosure', 'Unknown')"));
        builder.HasKey(x => new { x.MarketCalendarVersionId, x.TradingDate });
        builder.Property(x => x.TradingDate).HasColumnName("trading_date");
        builder.Property(x => x.SessionStatus).HasColumnName("session_status");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.MarketCalendarVersionId).HasColumnName("market_calendar_version_id");
        builder.Property(x => x.SourceArtifactId).HasColumnName("source_artifact_id");
        builder.Property(x => x.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.HasOne<MarketCalendarVersionRow>().WithMany().HasForeignKey(x => x.MarketCalendarVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(x => x.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SourceArtifactId).HasDatabaseName("ix_market_calendar_days_source_artifact_id");
    }
}

internal sealed class SourceArtifactConfiguration : IEntityTypeConfiguration<SourceArtifactRow>
{
    public void Configure(EntityTypeBuilder<SourceArtifactRow> builder)
    {
        builder.ToTable("source_artifacts", table =>
        {
            table.HasCheckConstraint("ck_source_artifacts_availability_status", "availability_status IN ('Known', 'Estimated', 'Unknown')");
            table.HasCheckConstraint("ck_source_artifacts_sha256", ConfigurationConventions.HashCheck("content_sha256"));
            table.HasCheckConstraint("ck_source_artifacts_retention_status", "retention_status IN ('RetainedInline', 'RetainedExternal', 'HashOnly')");
            table.HasCheckConstraint("ck_source_artifacts_retention_payload", "(retention_status = 'RetainedInline' AND content_blob IS NOT NULL AND external_location IS NULL) OR (retention_status = 'RetainedExternal' AND content_blob IS NULL AND external_location IS NOT NULL) OR (retention_status = 'HashOnly' AND content_blob IS NULL AND external_location IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.DatasetKind).HasColumnName("dataset_kind");
        builder.Property(x => x.SourceUri).HasColumnName("source_uri");
        builder.Property(x => x.RetrievedAtUtc).HasColumnName("retrieved_at_utc");
        builder.Property(x => x.SourcePublishedAtUtc).HasColumnName("source_published_at_utc");
        builder.Property(x => x.AvailableAtUtc).HasColumnName("available_at_utc");
        builder.Property(x => x.AvailabilityStatus).HasColumnName("availability_status");
        builder.Property(x => x.ContentSha256).HasColumnName("content_sha256");
        builder.Property(x => x.MediaType).HasColumnName("media_type");
        builder.Property(x => x.RetentionStatus).HasColumnName("retention_status");
        builder.Property(x => x.ContentBlob).HasColumnName("content_blob");
        builder.Property(x => x.ExternalLocation).HasColumnName("external_location");
        builder.Property(x => x.ContentEncoding).HasColumnName("content_encoding");
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json");
        builder.HasIndex(x => new { x.Provider, x.DatasetKind, x.ContentSha256 }).IsUnique().HasDatabaseName("ux_source_artifacts_provider_dataset_kind_content_sha256");
    }
}

internal sealed class DataUpdateRunConfiguration : IEntityTypeConfiguration<DataUpdateRunRow>
{
    public void Configure(EntityTypeBuilder<DataUpdateRunRow> builder)
    {
        builder.ToTable("data_update_runs", table =>
        {
            table.HasCheckConstraint("ck_data_update_runs_status", "status IN ('Queued', 'Running', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled')");
            table.HasCheckConstraint("ck_data_update_runs_requested_count", "requested_count IS NULL OR requested_count >= 0");
            table.HasCheckConstraint("ck_data_update_runs_counts", "success_count >= 0 AND failure_count >= 0 AND unchanged_count >= 0");
            table.HasCheckConstraint("ck_data_update_runs_sha256", ConfigurationConventions.HashCheck("configuration_sha256"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DatasetKind).HasColumnName("dataset_kind");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc");
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.RequestedCount).HasColumnName("requested_count");
        builder.Property(x => x.SuccessCount).HasColumnName("success_count").HasDefaultValue(0L);
        builder.Property(x => x.FailureCount).HasColumnName("failure_count").HasDefaultValue(0L);
        builder.Property(x => x.UnchangedCount).HasColumnName("unchanged_count").HasDefaultValue(0L);
        builder.Property(x => x.ConfigurationSnapshotJson).HasColumnName("configuration_snapshot_json");
        builder.Property(x => x.ConfigurationSha256).HasColumnName("configuration_sha256");
        builder.Property(x => x.Summary).HasColumnName("summary");
    }
}

internal sealed class DataUpdateItemConfiguration : IEntityTypeConfiguration<DataUpdateItemRow>
{
    public void Configure(EntityTypeBuilder<DataUpdateItemRow> builder)
    {
        builder.ToTable("data_update_items", table =>
        {
            table.HasCheckConstraint("ck_data_update_items_attempt_no", "item_attempt_no >= 1");
            table.HasCheckConstraint("ck_data_update_items_outcome", "outcome IN ('Inserted', 'Corrected', 'Unchanged', 'Skipped', 'Failed')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DataUpdateRunId).HasColumnName("data_update_run_id");
        builder.Property(x => x.SourceArtifactId).HasColumnName("source_artifact_id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.ItemKey).HasColumnName("item_key");
        builder.Property(x => x.ItemAttemptNo).HasColumnName("item_attempt_no");
        builder.Property(x => x.Outcome).HasColumnName("outcome");
        builder.Property(x => x.ResolvedEntityType).HasColumnName("resolved_entity_type");
        builder.Property(x => x.ResolvedRevisionId).HasColumnName("resolved_revision_id");
        builder.Property(x => x.ObservedAtUtc).HasColumnName("observed_at_utc");
        builder.HasOne<DataUpdateRunRow>().WithMany().HasForeignKey(x => x.DataUpdateRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>().WithMany().HasForeignKey(x => x.SourceArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DataUpdateRunId, x.ItemKey, x.ItemAttemptNo }).IsUnique().HasDatabaseName("ux_data_update_items_data_update_run_id_item_key_item_attempt_no");
        builder.HasIndex(x => x.SourceArtifactId).HasDatabaseName("ix_data_update_items_source_artifact_id");
        builder.HasIndex(x => x.InstrumentId).HasDatabaseName("ix_data_update_items_instrument_id");
    }
}

internal sealed class DataUpdateFailureConfiguration : IEntityTypeConfiguration<DataUpdateFailureRow>
{
    public void Configure(EntityTypeBuilder<DataUpdateFailureRow> builder)
    {
        builder.ToTable("data_update_failures", table => table.HasCheckConstraint("ck_data_update_failures_error_kind", "error_kind IN ('Http', 'RateLimit', 'Timeout', 'InvalidData', 'MissingData', 'ProviderChanged', 'Cancelled', 'DatabaseLocked', 'Unknown')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DataUpdateRunId).HasColumnName("data_update_run_id");
        builder.Property(x => x.DataUpdateItemId).HasColumnName("data_update_item_id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.ItemKey).HasColumnName("item_key");
        builder.Property(x => x.ErrorKind).HasColumnName("error_kind");
        builder.Property(x => x.Message).HasColumnName("message");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.HasOne<DataUpdateRunRow>().WithMany().HasForeignKey(x => x.DataUpdateRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DataUpdateItemRow>().WithMany().HasForeignKey(x => x.DataUpdateItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DataUpdateRunId).HasDatabaseName("ix_data_update_failures_data_update_run_id");
        builder.HasIndex(x => x.DataUpdateItemId).HasDatabaseName("ix_data_update_failures_data_update_item_id");
        builder.HasIndex(x => x.InstrumentId).HasDatabaseName("ix_data_update_failures_instrument_id");
    }
}
