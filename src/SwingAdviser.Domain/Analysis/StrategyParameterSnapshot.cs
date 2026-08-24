using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public sealed record StrategyParameterSnapshot
{
    public StrategyParameterSnapshot(
        Guid id,
        string strategyKey,
        string strategyVersion,
        string schemaVersion,
        string algorithmVersion,
        string normalizedParametersJson,
        Sha256Hash parametersHash,
        DateTimeOffset capturedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(id));
        }

        Id = id;
        StrategyKey = DomainGuard.Required(strategyKey, nameof(strategyKey));
        StrategyVersion = DomainGuard.Required(strategyVersion, nameof(strategyVersion));
        SchemaVersion = DomainGuard.Required(schemaVersion, nameof(schemaVersion));
        AlgorithmVersion = DomainGuard.Required(algorithmVersion, nameof(algorithmVersion));
        NormalizedParametersJson = DomainGuard.Required(normalizedParametersJson, nameof(normalizedParametersJson));
        ParametersHash = parametersHash;
        CapturedAtUtc = DomainGuard.Utc(capturedAtUtc, nameof(capturedAtUtc));
    }

    public Guid Id { get; }
    public string StrategyKey { get; }
    public string StrategyVersion { get; }
    public string SchemaVersion { get; }
    public string AlgorithmVersion { get; }
    public string NormalizedParametersJson { get; }
    public Sha256Hash ParametersHash { get; }
    public DateTimeOffset CapturedAtUtc { get; }
}
