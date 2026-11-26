using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SingularityBot.Behavior;

/// <summary>
/// Immutable, integrity-checked configuration artifact produced by Forge Build.
/// Payload remains declarative JSON; executable code is resolved by the host.
/// </summary>
public sealed record BotDoctrineBuildArtifact
{
    public int ArtifactVersion { get; init; } = 1;
    public string DoctrineId { get; init; } = string.Empty;
    public string PayloadSha256 { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
}

/// <summary>
/// Host-side build/load boundary shared by editor integrations and bot launchers.
/// The digest detects accidental or unauthorized payload modification; it is not
/// a signature and does not replace trusted transport or host authorization.
/// </summary>
public static class BotDoctrineBuildService
{
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static BotDoctrineBuildArtifact Build(BotDoctrineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = definition.Validate();
        if (errors.Count != 0)
            throw new InvalidOperationException("Doctrine cannot be built: " + string.Join(" ", errors));

        var payload = definition.Serialize();
        return new BotDoctrineBuildArtifact
        {
            ArtifactVersion = 1,
            DoctrineId = definition.Id,
            PayloadJson = payload,
            PayloadSha256 = ComputeSha256(payload)
        };
    }

    public static string SerializeArtifact(BotDoctrineBuildArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ValidateArtifactEnvelope(artifact);
        return JsonSerializer.Serialize(artifact, ArtifactJsonOptions);
    }

    public static BotDoctrineDefinition Load(string artifactJson)
    {
        if (string.IsNullOrWhiteSpace(artifactJson))
            throw new ArgumentException("A doctrine build artifact is required.", nameof(artifactJson));
        var artifact = JsonSerializer.Deserialize<BotDoctrineBuildArtifact>(artifactJson, ArtifactJsonOptions)
            ?? throw new JsonException("Doctrine artifact JSON did not contain an artifact.");
        ValidateArtifactEnvelope(artifact);
        var actualHash = ComputeSha256(artifact.PayloadJson);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualHash),
                Convert.FromHexString(artifact.PayloadSha256)))
            throw new InvalidOperationException("Doctrine artifact payload digest does not match.");
        var definition = BotDoctrineDefinition.Parse(artifact.PayloadJson);
        if (!string.Equals(definition.Id, artifact.DoctrineId, StringComparison.Ordinal))
            throw new InvalidOperationException("Doctrine artifact identity does not match its payload.");
        return definition;
    }

    private static void ValidateArtifactEnvelope(BotDoctrineBuildArtifact artifact)
    {
        if (artifact.ArtifactVersion != 1)
            throw new InvalidOperationException($"Unsupported doctrine artifact version {artifact.ArtifactVersion}.");
        if (string.IsNullOrWhiteSpace(artifact.DoctrineId) || string.IsNullOrWhiteSpace(artifact.PayloadJson))
            throw new InvalidOperationException("Doctrine artifact identity and payload are required.");
        if (artifact.PayloadSha256.Length != 64 || !artifact.PayloadSha256.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Doctrine artifact payloadSha256 must be a 64-character hexadecimal SHA-256 digest.");
    }

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
