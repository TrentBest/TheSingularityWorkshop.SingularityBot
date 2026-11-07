namespace TheSingularityWorkshop.SingularityBot.Gui.Contracts;

/// <summary>Read-only boundary supplied by the runtime owner when its contract is ready.</summary>
public interface IForgeRuntimeSnapshotSource
{
    event EventHandler<RuntimeSnapshotEnvelope>? SnapshotReceived;
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}

public sealed record RuntimeSnapshotEnvelope(
    string ContractVersion,
    string SessionId,
    string MapDocumentHash,
    long ObservationSequence,
    DateTimeOffset CapturedAt,
    string PayloadJson);

/// <summary>Map documents are supplied by the runtime; Forge never decodes SC2 archives or protocol DTOs.</summary>
public interface IForgeMapDocumentSource
{
    ValueTask<ForgeMapDocument?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record ForgeMapDocument(
    string SchemaVersion,
    string MapId,
    string DisplayName,
    string ContentHash,
    string ProvenanceKind,
    string CoordinateSystem,
    double OriginX,
    double OriginY,
    double Width,
    double Height,
    string PayloadJson);

public sealed record ForgeDiagnostic(string Code, string Severity, string DocumentPath, string Message, IReadOnlyList<string> RelatedIds);
public sealed record ForgeValidationResult(string ContractVersion, bool IsValid, IReadOnlyList<ForgeDiagnostic> Diagnostics);
public sealed record ForgeProposal(string BaseDefinitionHash, string ProposalId, string Rationale, string ProposedDocumentJson, string PatchJson);
public sealed record ForgeProposalDecision(string ProposalId, bool Approved, DateTimeOffset DecidedAt);

public enum MapWorkspaceSource { LiveRunCapture, ReplayCapture, OfflineImport, SyntheticFixture }