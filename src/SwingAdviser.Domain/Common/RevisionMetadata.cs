namespace SwingAdviser.Domain.Common;

public sealed record RevisionMetadata
{
    public RevisionMetadata(
        Guid id,
        int revisionNumber,
        Guid? supersedesId,
        Sha256Hash contentHash,
        DateTimeOffset recordedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Revision ID cannot be empty.", nameof(id));
        }

        Id = id;
        RevisionNumber = DomainGuard.Positive(revisionNumber, nameof(revisionNumber));
        SupersedesId = supersedesId;
        ContentHash = contentHash;
        RecordedAtUtc = DomainGuard.Utc(recordedAtUtc, nameof(recordedAtUtc));

        if (revisionNumber == 1 && supersedesId is not null)
        {
            throw new ArgumentException("The first revision cannot supersede another revision.", nameof(supersedesId));
        }

        if (revisionNumber > 1 && supersedesId is null)
        {
            throw new ArgumentException("A correction revision must identify its direct predecessor.", nameof(supersedesId));
        }

        if (supersedesId == id)
        {
            throw new ArgumentException("A revision cannot supersede itself.", nameof(supersedesId));
        }
    }

    public Guid Id { get; }
    public int RevisionNumber { get; }
    public Guid? SupersedesId { get; }
    public Sha256Hash ContentHash { get; }
    public DateTimeOffset RecordedAtUtc { get; }
}

public sealed record SourceRevisionMetadata
{
    public SourceRevisionMetadata(
        RevisionMetadata revision,
        Availability availability,
        DateTimeOffset firstObservedAtUtc,
        Guid? sourceArtifactId)
    {
        Revision = revision;
        Availability = availability;
        FirstObservedAtUtc = DomainGuard.Utc(firstObservedAtUtc, nameof(firstObservedAtUtc));
        SourceArtifactId = sourceArtifactId;

        if (availability.AvailableAtUtc > firstObservedAtUtc)
        {
            throw new ArgumentException("Availability cannot be later than first observation.", nameof(availability));
        }
    }

    public RevisionMetadata Revision { get; }
    public Availability Availability { get; }
    public DateTimeOffset FirstObservedAtUtc { get; }
    public Guid? SourceArtifactId { get; }
}
