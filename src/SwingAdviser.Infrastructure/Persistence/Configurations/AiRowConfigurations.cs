using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal sealed class PromptTemplateSnapshotRowConfiguration : IEntityTypeConfiguration<PromptTemplateSnapshotRow>
{
    public void Configure(EntityTypeBuilder<PromptTemplateSnapshotRow> builder)
    {
        builder.ToTable("prompt_template_snapshots", table =>
            table.HasCheckConstraint("ck_prompt_template_snapshots_template_sha256", HashCheck("template_sha256")));
        builder.HasKey(row => row.Id);
        builder.HasIndex(row => row.TemplateSha256).IsUnique();
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class AiProfileSnapshotRowConfiguration : IEntityTypeConfiguration<AiProfileSnapshotRow>
{
    public void Configure(EntityTypeBuilder<AiProfileSnapshotRow> builder)
    {
        builder.ToTable("ai_profile_snapshots", table =>
        {
            table.HasCheckConstraint("ck_ai_profile_snapshots_timeout_seconds", "timeout_seconds > 0");
            table.HasCheckConstraint("ck_ai_profile_snapshots_profile_sha256", HashCheck("profile_sha256"));
        });
        builder.HasKey(row => row.Id);
        builder.HasIndex(row => row.ProfileSha256).IsUnique();
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class AiCheckJobRowConfiguration : IEntityTypeConfiguration<AiCheckJobRow>
{
    public void Configure(EntityTypeBuilder<AiCheckJobRow> builder)
    {
        builder.ToTable("ai_check_jobs", table =>
        {
            table.HasCheckConstraint("ck_ai_check_jobs_request_origin", "request_origin IN ('User', 'Automatic')");
            table.HasCheckConstraint("ck_ai_check_jobs_candidate_side", "candidate_side IN ('Long', 'Short')");
            table.HasCheckConstraint("ck_ai_check_jobs_input_sha256", HashCheck("input_sha256"));
            table.HasCheckConstraint("ck_ai_check_jobs_technical_manifest_sha256", HashCheck("technical_manifest_sha256"));
            table.HasCheckConstraint("ck_ai_check_jobs_strategy_snapshot_sha256", HashCheck("strategy_snapshot_sha256"));
            table.HasCheckConstraint("ck_ai_check_jobs_automatic_configuration_sha256", $"automatic_configuration_sha256 IS NULL OR ({HashCheck("automatic_configuration_sha256")})");
            table.HasCheckConstraint("ck_ai_check_jobs_automatic_fields", "(request_origin = 'Automatic' AND automatic_selection_rank > 0 AND selection_policy_version IS NOT NULL AND automatic_configuration_json IS NOT NULL AND automatic_configuration_sha256 IS NOT NULL) OR (request_origin = 'User' AND automatic_selection_rank IS NULL AND selection_policy_version IS NULL AND automatic_configuration_json IS NULL AND automatic_configuration_sha256 IS NULL)");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<CandidateResultRow>().WithMany().HasForeignKey(row => row.CandidateResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromptTemplateSnapshotRow>().WithMany().HasForeignKey(row => row.PromptTemplateSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AiProfileSnapshotRow>().WithMany().HasForeignKey(row => row.AiProfileSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.CandidateResultId, row.InputSha256, row.AiProfileSnapshotId, row.PromptTemplateSnapshotId }).IsUnique();
        builder.HasIndex(row => row.PromptTemplateSnapshotId);
        builder.HasIndex(row => row.AiProfileSnapshotId);
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class AiJobRequestEventRowConfiguration : IEntityTypeConfiguration<AiJobRequestEventRow>
{
    public void Configure(EntityTypeBuilder<AiJobRequestEventRow> builder)
    {
        builder.ToTable("ai_job_request_events", table =>
        {
            table.HasCheckConstraint("ck_ai_job_request_events_event_kind", "event_kind IN ('InitialRequest', 'PriorityPromotion', 'RetryRequest', 'RecheckRequest')");
            table.HasCheckConstraint("ck_ai_job_request_events_request_origin", "request_origin IN ('User', 'Automatic')");
            table.HasCheckConstraint("ck_ai_job_request_events_ordinal", "ordinal > 0");
            table.HasCheckConstraint("ck_ai_job_request_events_initial_kind", "(ordinal = 1 AND event_kind = 'InitialRequest') OR (ordinal > 1 AND event_kind <> 'InitialRequest')");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<AiCheckJobRow>().WithMany().HasForeignKey(row => row.AiCheckJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AiCheckJobId, row.Ordinal }).IsUnique();
    }
}

internal sealed class AiAttemptRowConfiguration : IEntityTypeConfiguration<AiAttemptRow>
{
    public void Configure(EntityTypeBuilder<AiAttemptRow> builder)
    {
        builder.ToTable("ai_attempts", table =>
        {
            table.HasCheckConstraint("ck_ai_attempts_attempt_no", "attempt_no > 0");
            table.HasCheckConstraint("ck_ai_attempts_attempt_kind", "attempt_kind IN ('Initial', 'Retry', 'Recheck')");
            table.HasCheckConstraint("ck_ai_attempts_request_origin", "request_origin IN ('User', 'Automatic')");
            table.HasCheckConstraint("ck_ai_attempts_status", "status IN ('Queued', 'Running', 'Succeeded', 'Failed', 'TimedOut', 'InsufficientInformation', 'Cancelled')");
            table.HasCheckConstraint("ck_ai_attempts_timeout_seconds", "timeout_seconds > 0");
            table.HasCheckConstraint("ck_ai_attempts_error_kind", "error_kind IS NULL OR error_kind IN ('CliFailure', 'Timeout', 'Cancelled', 'Interrupted', 'InvalidResponse', 'ParseFailure', 'Unknown')");
            table.HasCheckConstraint("ck_ai_attempts_raw_response_sha256", $"raw_response_sha256 IS NULL OR ({HashCheck("raw_response_sha256")})");
            table.HasCheckConstraint("ck_ai_attempts_initial_kind", "(attempt_no = 1 AND attempt_kind = 'Initial') OR (attempt_no > 1 AND attempt_kind IN ('Retry', 'Recheck'))");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<AiCheckJobRow>().WithMany().HasForeignKey(row => row.AiCheckJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AiCheckJobId, row.AttemptNo }).IsUnique();
        builder.HasIndex(row => row.AiCheckJobId)
            .IsUnique()
            .HasFilter("status IN ('Queued', 'Running')");
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class AiAttemptEventRowConfiguration : IEntityTypeConfiguration<AiAttemptEventRow>
{
    public void Configure(EntityTypeBuilder<AiAttemptEventRow> builder)
    {
        const string states = "'Queued', 'Running', 'Succeeded', 'Failed', 'TimedOut', 'InsufficientInformation', 'Cancelled'";
        builder.ToTable("ai_attempt_events", table =>
        {
            table.HasCheckConstraint("ck_ai_attempt_events_from_status", $"from_status IS NULL OR from_status IN ({states})");
            table.HasCheckConstraint("ck_ai_attempt_events_to_status", $"to_status IN ({states})");
            table.HasCheckConstraint("ck_ai_attempt_events_ordinal", "ordinal > 0");
            table.HasCheckConstraint("ck_ai_attempt_events_initial", "(ordinal = 1 AND from_status IS NULL AND to_status = 'Queued') OR (ordinal > 1 AND from_status IS NOT NULL)");
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<AiAttemptRow>().WithMany().HasForeignKey(row => row.AiAttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AiAttemptId, row.Ordinal }).IsUnique();
    }
}

internal sealed class AiResultRowConfiguration : IEntityTypeConfiguration<AiResultRow>
{
    public void Configure(EntityTypeBuilder<AiResultRow> builder)
    {
        builder.ToTable("ai_results", table =>
        {
            table.HasCheckConstraint("ck_ai_results_verdict", "verdict IS NULL OR verdict IN ('Bullish', 'Neutral', 'Bearish')");
            table.HasCheckConstraint("ck_ai_results_confidence", "confidence IS NULL OR confidence IN ('High', 'Medium', 'Low')");
            table.HasCheckConstraint("ck_ai_results_structured_result_sha256", HashCheck("structured_result_sha256"));
        });
        builder.HasKey(row => row.Id);
        builder.HasOne<AiAttemptRow>().WithMany().HasForeignKey(row => row.AiAttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.AiAttemptId).IsUnique();
    }

    private static string HashCheck(string column) =>
        $"length({column}) = 64 AND {column} NOT GLOB '*[^0-9a-f]*'";
}

internal sealed class AiResultSourceRowConfiguration : IEntityTypeConfiguration<AiResultSourceRow>
{
    public void Configure(EntityTypeBuilder<AiResultSourceRow> builder)
    {
        builder.ToTable("ai_result_sources", table =>
            table.HasCheckConstraint("ck_ai_result_sources_ordinal", "ordinal >= 0"));
        builder.HasKey(row => row.Id);
        builder.HasOne<AiResultRow>().WithMany().HasForeignKey(row => row.AiResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.AiResultId, row.Ordinal }).IsUnique();
    }
}
