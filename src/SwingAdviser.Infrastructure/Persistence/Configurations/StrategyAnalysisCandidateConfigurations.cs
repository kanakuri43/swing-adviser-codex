using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal sealed class StrategyParameterSnapshotConfiguration : IEntityTypeConfiguration<StrategyParameterSnapshotRow>
{
    public void Configure(EntityTypeBuilder<StrategyParameterSnapshotRow> builder)
    {
        builder.ToTable("strategy_parameter_snapshots", table => table.HasCheckConstraint("ck_strategy_parameter_snapshots_sha256", ConfigurationConventions.HashCheck("parameters_sha256")));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StrategyKey).HasColumnName("strategy_key");
        builder.Property(x => x.StrategyVersion).HasColumnName("strategy_version");
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version");
        builder.Property(x => x.ParametersJson).HasColumnName("parameters_json");
        builder.Property(x => x.ParametersSha256).HasColumnName("parameters_sha256");
        builder.Property(x => x.CapturedAtUtc).HasColumnName("captured_at_utc");
        builder.Property(x => x.SourceDescription).HasColumnName("source_description");
        builder.HasIndex(x => new { x.StrategyKey, x.ParametersSha256 }).IsUnique().HasDatabaseName("ux_strategy_parameter_snapshots_strategy_key_parameters_sha256");
    }
}

internal sealed class AnalysisRunConfiguration : IEntityTypeConfiguration<AnalysisRunRow>
{
    public void Configure(EntityTypeBuilder<AnalysisRunRow> builder)
    {
        builder.ToTable("analysis_runs", table =>
        {
            table.HasCheckConstraint("ck_analysis_runs_run_mode", "run_mode IN ('Daily', 'Manual', 'Backtest')");
            table.HasCheckConstraint("ck_analysis_runs_status", "status IN ('Queued', 'Running', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled')");
            table.HasCheckConstraint("ck_analysis_runs_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
            table.HasCheckConstraint("ck_analysis_runs_counts", "total_count >= 0 AND success_count >= 0 AND failure_count >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EvaluationBarDate).HasColumnName("evaluation_bar_date");
        builder.Property(x => x.AnalyzedAtUtc).HasColumnName("analyzed_at_utc");
        builder.Property(x => x.RecordedCutoffAtUtc).HasColumnName("recorded_cutoff_at_utc");
        builder.Property(x => x.RunMode).HasColumnName("run_mode");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.StrategyParameterSnapshotId).HasColumnName("strategy_parameter_snapshot_id");
        builder.Property(x => x.PointInTimeStatus).HasColumnName("point_in_time_status");
        builder.Property(x => x.PriceSelectorVersion).HasColumnName("price_selector_version");
        builder.Property(x => x.AdjustmentEngineVersion).HasColumnName("adjustment_engine_version");
        builder.Property(x => x.IndicatorEngineVersion).HasColumnName("indicator_engine_version");
        builder.Property(x => x.CandidateEngineVersion).HasColumnName("candidate_engine_version");
        builder.Property(x => x.MarketCalendarVersionId).HasColumnName("market_calendar_version_id");
        builder.Property(x => x.ApplicationVersion).HasColumnName("application_version");
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.TotalCount).HasColumnName("total_count").HasDefaultValue(0L);
        builder.Property(x => x.SuccessCount).HasColumnName("success_count").HasDefaultValue(0L);
        builder.Property(x => x.FailureCount).HasColumnName("failure_count").HasDefaultValue(0L);
        builder.Property(x => x.Summary).HasColumnName("summary");
        builder.HasOne<StrategyParameterSnapshotRow>().WithMany().HasForeignKey(x => x.StrategyParameterSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MarketCalendarVersionRow>().WithMany().HasForeignKey(x => x.MarketCalendarVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.StrategyParameterSnapshotId).HasDatabaseName("ix_analysis_runs_strategy_parameter_snapshot_id");
        builder.HasIndex(x => x.MarketCalendarVersionId).HasDatabaseName("ix_analysis_runs_market_calendar_version_id");
    }
}

internal sealed class AnalysisInputManifestConfiguration : IEntityTypeConfiguration<AnalysisInputManifestRow>
{
    public void Configure(EntityTypeBuilder<AnalysisInputManifestRow> builder)
    {
        builder.ToTable("analysis_input_manifests", table =>
        {
            table.HasCheckConstraint("ck_analysis_input_manifests_bar_counts", "bar_count >= 0 AND required_bar_count >= 0");
            table.HasCheckConstraint("ck_analysis_input_manifests_bar_range", "last_bar_date IS NULL OR first_bar_date IS NULL OR last_bar_date >= first_bar_date");
            table.HasCheckConstraint("ck_analysis_input_manifests_empty_range", "(bar_count = 0 AND first_bar_date IS NULL AND last_bar_date IS NULL) OR (bar_count > 0 AND first_bar_date IS NOT NULL AND last_bar_date IS NOT NULL)");
            table.HasCheckConstraint("ck_analysis_input_manifests_history_status", "history_status IN ('Complete', 'InsufficientHistory', 'HistoryIncomplete', 'Invalid')");
            table.HasCheckConstraint("ck_analysis_input_manifests_point_in_time_status", "point_in_time_status IN ('Verified', 'Unverified')");
            table.HasCheckConstraint("ck_analysis_input_manifests_selection_basis", "selection_basis IN ('ObservedAt', 'SourceAvailableAt')");
            table.HasCheckConstraint("ck_analysis_input_manifests_price_set_sha256", ConfigurationConventions.HashCheck("price_revision_set_sha256"));
            table.HasCheckConstraint("ck_analysis_input_manifests_action_set_sha256", ConfigurationConventions.HashCheck("corporate_action_set_sha256"));
            table.HasCheckConstraint("ck_analysis_input_manifests_manifest_sha256", ConfigurationConventions.HashCheck("manifest_sha256"));
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AnalysisRunId).HasColumnName("analysis_run_id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.PriceProvider).HasColumnName("price_provider");
        builder.Property(x => x.PriceRevisionSetId).HasColumnName("price_revision_set_id");
        builder.Property(x => x.FirstBarDate).HasColumnName("first_bar_date");
        builder.Property(x => x.LastBarDate).HasColumnName("last_bar_date");
        builder.Property(x => x.BarCount).HasColumnName("bar_count");
        builder.Property(x => x.RequiredBarCount).HasColumnName("required_bar_count");
        builder.Property(x => x.HistoryStatus).HasColumnName("history_status");
        builder.Property(x => x.PointInTimeStatus).HasColumnName("point_in_time_status");
        builder.Property(x => x.SelectionBasis).HasColumnName("selection_basis");
        builder.Property(x => x.SelectionRuleVersion).HasColumnName("selection_rule_version");
        builder.Property(x => x.SelectedRecordedCutoffAtUtc).HasColumnName("selected_recorded_cutoff_at_utc");
        builder.Property(x => x.SelectedAvailableCutoffAtUtc).HasColumnName("selected_available_cutoff_at_utc");
        builder.Property(x => x.PriceRevisionSetSha256).HasColumnName("price_revision_set_sha256");
        builder.Property(x => x.CorporateActionSetSha256).HasColumnName("corporate_action_set_sha256");
        builder.Property(x => x.ManifestSha256).HasColumnName("manifest_sha256");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<AnalysisRunRow>().WithMany().HasForeignKey(x => x.AnalysisRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PriceRevisionSetRow>().WithMany().HasForeignKey(x => x.PriceRevisionSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AnalysisRunId, x.InstrumentId, x.PriceProvider }).IsUnique().HasDatabaseName("ux_analysis_input_manifests_analysis_run_id_instrument_id_price_provider");
        builder.HasIndex(x => x.InstrumentId).HasDatabaseName("ix_analysis_input_manifests_instrument_id");
        builder.HasIndex(x => x.PriceRevisionSetId).HasDatabaseName("ix_analysis_input_manifests_price_revision_set_id");
    }
}

internal sealed class AnalysisActionApplicationConfiguration : IEntityTypeConfiguration<AnalysisActionApplicationRow>
{
    public void Configure(EntityTypeBuilder<AnalysisActionApplicationRow> builder)
    {
        builder.ToTable("analysis_action_applications", table =>
        {
            table.HasCheckConstraint("ck_analysis_action_applications_status", "application_status IN ('Applied', 'ExcludedNotEffective', 'ExcludedUnavailable', 'Unsupported', 'ReconciliationRequired')");
            table.HasCheckConstraint("ck_analysis_action_applications_factors", "(price_factor IS NULL OR CAST(price_factor AS NUMERIC) > 0) AND (volume_factor IS NULL OR CAST(volume_factor AS NUMERIC) > 0) AND (cumulative_price_factor IS NULL OR CAST(cumulative_price_factor AS NUMERIC) > 0) AND (cumulative_volume_factor IS NULL OR CAST(cumulative_volume_factor AS NUMERIC) > 0)");
            table.HasCheckConstraint("ck_analysis_action_applications_ordinal", "ordinal >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AnalysisInputManifestId).HasColumnName("analysis_input_manifest_id");
        builder.Property(x => x.CorporateActionRevisionId).HasColumnName("corporate_action_revision_id");
        builder.Property(x => x.ApplicationStatus).HasColumnName("application_status");
        builder.Property(x => x.ReferencePriceRevisionId).HasColumnName("reference_price_revision_id");
        builder.Property(x => x.PriceFactor).HasColumnName("price_factor");
        builder.Property(x => x.VolumeFactor).HasColumnName("volume_factor");
        builder.Property(x => x.CumulativePriceFactor).HasColumnName("cumulative_price_factor");
        builder.Property(x => x.CumulativeVolumeFactor).HasColumnName("cumulative_volume_factor");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.Ordinal).HasColumnName("ordinal");
        builder.HasOne<AnalysisInputManifestRow>().WithMany().HasForeignKey(x => x.AnalysisInputManifestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CorporateActionRevisionRow>().WithMany().HasForeignKey(x => x.CorporateActionRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DailyPriceRevisionRow>().WithMany().HasForeignKey(x => x.ReferencePriceRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AnalysisInputManifestId, x.CorporateActionRevisionId }).IsUnique().HasDatabaseName("ux_analysis_action_applications_analysis_input_manifest_id_corporate_action_revision_id");
        builder.HasIndex(x => new { x.AnalysisInputManifestId, x.Ordinal }).IsUnique().HasDatabaseName("ux_analysis_action_applications_analysis_input_manifest_id_ordinal");
        builder.HasIndex(x => x.CorporateActionRevisionId).HasDatabaseName("ix_analysis_action_applications_corporate_action_revision_id");
        builder.HasIndex(x => x.ReferencePriceRevisionId).HasDatabaseName("ix_analysis_action_applications_reference_price_revision_id");
    }
}

internal sealed class TechnicalAnalysisResultConfiguration : IEntityTypeConfiguration<TechnicalAnalysisResultRow>
{
    public void Configure(EntityTypeBuilder<TechnicalAnalysisResultRow> builder)
    {
        builder.ToTable("technical_analysis_results", table =>
        {
            table.HasCheckConstraint("ck_technical_analysis_results_position_side", "position_side IN ('Long', 'Short')");
            table.HasCheckConstraint("ck_technical_analysis_results_signal_purpose", "signal_purpose = 'Entry'");
            table.HasCheckConstraint("ck_technical_analysis_results_outcome", "outcome IN ('Candidate', 'NotCandidate', 'InsufficientHistory', 'HistoryIncomplete', 'InvalidData', 'PointInTimeUnverified', 'ReconciliationRequired', 'Failed')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AnalysisRunId).HasColumnName("analysis_run_id");
        builder.Property(x => x.AnalysisInputManifestId).HasColumnName("analysis_input_manifest_id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.PositionSide).HasColumnName("position_side");
        builder.Property(x => x.SignalPurpose).HasColumnName("signal_purpose");
        builder.Property(x => x.Outcome).HasColumnName("outcome");
        builder.Property(x => x.ReasonSummary).HasColumnName("reason_summary");
        builder.Property(x => x.ReasonsJson).HasColumnName("reasons_json");
        builder.Property(x => x.CalculationStartBarDate).HasColumnName("calculation_start_bar_date");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<AnalysisRunRow>().WithMany().HasForeignKey(x => x.AnalysisRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnalysisInputManifestRow>().WithMany().HasForeignKey(x => x.AnalysisInputManifestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstrumentRow>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AnalysisRunId, x.InstrumentId, x.PositionSide, x.SignalPurpose }).IsUnique().HasDatabaseName("ux_technical_analysis_results_analysis_run_id_instrument_id_position_side_signal_purpose");
        builder.HasIndex(x => x.AnalysisInputManifestId).HasDatabaseName("ix_technical_analysis_results_analysis_input_manifest_id");
        builder.HasIndex(x => x.InstrumentId).HasDatabaseName("ix_technical_analysis_results_instrument_id");
    }
}

internal sealed class IndicatorResultConfiguration : IEntityTypeConfiguration<IndicatorResultRow>
{
    public void Configure(EntityTypeBuilder<IndicatorResultRow> builder)
    {
        builder.ToTable("indicator_results", table =>
        {
            table.HasCheckConstraint("ck_indicator_results_input_sha256", ConfigurationConventions.HashCheck("input_sha256"));
            table.HasCheckConstraint("ck_indicator_results_ordinal", "ordinal >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TechnicalAnalysisResultId).HasColumnName("technical_analysis_result_id");
        builder.Property(x => x.IndicatorKey).HasColumnName("indicator_key");
        builder.Property(x => x.AlgorithmId).HasColumnName("algorithm_id");
        builder.Property(x => x.ParametersJson).HasColumnName("parameters_json");
        builder.Property(x => x.ValuesJson).HasColumnName("values_json");
        builder.Property(x => x.CalculationStartBarDate).HasColumnName("calculation_start_bar_date");
        builder.Property(x => x.InputSha256).HasColumnName("input_sha256");
        builder.Property(x => x.Ordinal).HasColumnName("ordinal");
        builder.HasOne<TechnicalAnalysisResultRow>().WithMany().HasForeignKey(x => x.TechnicalAnalysisResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TechnicalAnalysisResultId, x.IndicatorKey }).IsUnique().HasDatabaseName("ux_indicator_results_technical_analysis_result_id_indicator_key");
        builder.HasIndex(x => new { x.TechnicalAnalysisResultId, x.Ordinal }).IsUnique().HasDatabaseName("ux_indicator_results_technical_analysis_result_id_ordinal");
    }
}

internal sealed class CandidateResultConfiguration : IEntityTypeConfiguration<CandidateResultRow>
{
    public void Configure(EntityTypeBuilder<CandidateResultRow> builder)
    {
        builder.ToTable("candidate_results", table =>
        {
            table.HasCheckConstraint("ck_candidate_results_score", "score BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_candidate_results_confidence", "confidence IN ('High', 'Medium', 'Low')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TechnicalAnalysisResultId).HasColumnName("technical_analysis_result_id");
        builder.Property(x => x.Score).HasColumnName("score");
        builder.Property(x => x.Confidence).HasColumnName("confidence");
        builder.Property(x => x.PrimaryReason).HasColumnName("primary_reason");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne<TechnicalAnalysisResultRow>().WithMany().HasForeignKey(x => x.TechnicalAnalysisResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TechnicalAnalysisResultId).IsUnique().HasDatabaseName("ux_candidate_results_technical_analysis_result_id");
    }
}

internal sealed class CandidateScoreComponentConfiguration : IEntityTypeConfiguration<CandidateScoreComponentRow>
{
    public void Configure(EntityTypeBuilder<CandidateScoreComponentRow> builder)
    {
        builder.ToTable("candidate_score_components", table =>
        {
            table.HasCheckConstraint("ck_candidate_score_components_matched", "matched IN (0, 1)");
            table.HasCheckConstraint("ck_candidate_score_components_ordinal", "ordinal >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateResultId).HasColumnName("candidate_result_id");
        builder.Property(x => x.ComponentKey).HasColumnName("component_key");
        builder.Property(x => x.Matched).HasColumnName("matched");
        builder.Property(x => x.RawValueJson).HasColumnName("raw_value_json");
        builder.Property(x => x.Weight).HasColumnName("weight");
        builder.Property(x => x.AwardedScore).HasColumnName("awarded_score");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.Ordinal).HasColumnName("ordinal");
        builder.HasOne<CandidateResultRow>().WithMany().HasForeignKey(x => x.CandidateResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CandidateResultId, x.ComponentKey }).IsUnique().HasDatabaseName("ux_candidate_score_components_candidate_result_id_component_key");
        builder.HasIndex(x => new { x.CandidateResultId, x.Ordinal }).IsUnique().HasDatabaseName("ux_candidate_score_components_candidate_result_id_ordinal");
    }
}
