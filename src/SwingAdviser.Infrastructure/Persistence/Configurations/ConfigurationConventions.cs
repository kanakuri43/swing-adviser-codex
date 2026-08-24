using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Configurations;

internal static class ConfigurationConventions
{
    internal const string Sha256CheckTemplate = "length({0}) = 64 AND {0} NOT GLOB '*[^0-9a-f]*'";

    internal static void ConfigureRevision<TEntity>(EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : class, IRevisionRow
    {
        builder.ToTable(tableName, table =>
        {
            table.HasCheckConstraint(
                $"ck_{tableName}_revision_chain",
                "(revision_no = 1 AND supersedes_id IS NULL) OR (revision_no > 1 AND supersedes_id IS NOT NULL AND supersedes_id <> id)");
            table.HasCheckConstraint(
                $"ck_{tableName}_availability",
                "(availability_status IN ('Known', 'Estimated') AND available_at_utc IS NOT NULL AND available_at_utc <= first_observed_at_utc) OR (availability_status = 'Unknown' AND available_at_utc IS NULL)");
            table.HasCheckConstraint(
                $"ck_{tableName}_observation_recording_order",
                "first_observed_at_utc <= recorded_at_utc");
        });
        builder.HasKey(nameof(IRevisionRow.Id));
        builder.Property<Guid>(nameof(IRevisionRow.Id)).HasColumnName("id");
        builder.Property<long>(nameof(IRevisionRow.RevisionNo)).HasColumnName("revision_no");
        builder.Property<Guid?>(nameof(IRevisionRow.SupersedesId)).HasColumnName("supersedes_id");
        builder.Property<string>(nameof(IRevisionRow.ContentSha256)).HasColumnName("content_sha256");
        builder.Property<DateTimeOffset?>(nameof(IRevisionRow.AvailableAtUtc)).HasColumnName("available_at_utc");
        builder.Property<string>(nameof(IRevisionRow.AvailabilityStatus)).HasColumnName("availability_status");
        builder.Property<DateTimeOffset>(nameof(IRevisionRow.FirstObservedAtUtc)).HasColumnName("first_observed_at_utc");
        builder.Property<DateTimeOffset>(nameof(IRevisionRow.RecordedAtUtc)).HasColumnName("recorded_at_utc");
        builder.Property<Guid?>(nameof(IRevisionRow.SourceArtifactId)).HasColumnName("source_artifact_id");

        builder.HasOne<TEntity>()
            .WithMany()
            .HasForeignKey(nameof(IRevisionRow.SupersedesId))
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SourceArtifactRow>()
            .WithMany()
            .HasForeignKey(nameof(IRevisionRow.SourceArtifactId))
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(nameof(IRevisionRow.SupersedesId))
            .IsUnique()
            .HasFilter("\"supersedes_id\" IS NOT NULL")
            .HasDatabaseName($"ux_{tableName}_supersedes_id");
        builder.HasIndex(nameof(IRevisionRow.SourceArtifactId))
            .HasDatabaseName($"ix_{tableName}_source_artifact_id");
    }

    internal static string HashCheck(string columnName) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, Sha256CheckTemplate, columnName);
}
