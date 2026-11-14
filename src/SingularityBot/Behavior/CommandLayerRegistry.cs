namespace SingularityBot.Behavior;

/// <summary>Trusted, compiled implementation metadata. No executable type names are
/// accepted from portable bot definitions.</summary>
public sealed record CommandLayerDescriptor
{
    public string ImplementationId { get; init; } = string.Empty;
    public CommandLayerKind Kind { get; init; }
    public IReadOnlyList<string> SupportedRaces { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Accepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Emits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProvidedCapabilities { get; init; } = Array.Empty<string>();
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>Resolves portable selections only against explicitly registered,
/// trusted descriptors supplied by the host application.</summary>
public sealed class CommandLayerRegistry
{
    private const int MaximumIdentifierLength = 160;
    private const int MaximumDescriptionLength = 1024;
    private const int MaximumMetadataItems = 64;

    private readonly Dictionary<string, CommandLayerDescriptor> _descriptors =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<CommandLayerDescriptor> Descriptors =>
        Array.AsReadOnly(_descriptors.Values.ToArray());

    public void Register(CommandLayerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateRequiredText(descriptor.ImplementationId, MaximumIdentifierLength,
            "An implementation id of 1 to 160 characters is required.", nameof(descriptor));
        if (!Enum.IsDefined(descriptor.Kind))
            throw new ArgumentException("A known command-layer kind is required.", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName) ||
            descriptor.DisplayName.Length > MaximumIdentifierLength)
            throw new ArgumentException("A display name of 1 to 160 characters is required.", nameof(descriptor));
        if (descriptor.Description is null || descriptor.Description.Length > MaximumDescriptionLength)
            throw new ArgumentException("Description cannot exceed 1024 characters.", nameof(descriptor));

        var snapshot = descriptor with
        {
            SupportedRaces = SnapshotIds(descriptor.SupportedRaces, "supported races", nameof(descriptor), requireItems: true),
            Accepts = SnapshotIds(descriptor.Accepts, "accepted contracts", nameof(descriptor)),
            Emits = SnapshotIds(descriptor.Emits, "emitted contracts", nameof(descriptor)),
            RequiredCapabilities = SnapshotIds(descriptor.RequiredCapabilities, "required capabilities", nameof(descriptor)),
            ProvidedCapabilities = SnapshotIds(descriptor.ProvidedCapabilities, "provided capabilities", nameof(descriptor))
        };

        if (_descriptors.ContainsKey(snapshot.ImplementationId))
            throw new InvalidOperationException($"Implementation '{snapshot.ImplementationId}' is already registered.");
        _descriptors.Add(snapshot.ImplementationId, snapshot);
    }

    public CommandLayerResolution Resolve(
        CommandLayerSelection selection,
        string actualRace,
        IEnumerable<string> availableCapabilities)
    {
        if (selection is null)
            return CommandLayerResolution.Failed("A command-layer selection is required.");
        if (!_descriptors.TryGetValue(selection.ImplementationId ?? string.Empty, out var descriptor))
            return CommandLayerResolution.Failed($"Unknown implementation '{selection.ImplementationId}'.");
        if (selection.Kind != descriptor.Kind)
            return CommandLayerResolution.Failed($"Implementation '{descriptor.ImplementationId}' does not support layer kind '{selection.Kind}'.");
        if (string.IsNullOrWhiteSpace(actualRace) ||
            !descriptor.SupportedRaces.Contains(actualRace, StringComparer.OrdinalIgnoreCase))
            return CommandLayerResolution.Failed($"Implementation '{descriptor.ImplementationId}' does not support race '{actualRace}'.");
        var available = new HashSet<string>(availableCapabilities ?? Array.Empty<string>(), StringComparer.Ordinal);
        var missing = descriptor.RequiredCapabilities.Where(capability => !available.Contains(capability)).ToArray();
        if (missing.Length > 0)
            return CommandLayerResolution.Failed(
                $"Implementation '{descriptor.ImplementationId}' requires unavailable capabilities: {string.Join(", ", missing)}.");
        return CommandLayerResolution.Resolved(descriptor);
    }

    private static IReadOnlyList<string> SnapshotIds(
        IReadOnlyList<string> values, string label, string parameterName, bool requireItems = false)
    {
        if (values is null)
            throw new ArgumentException($"The {label} collection is required.", parameterName);
        if (values.Count > MaximumMetadataItems)
            throw new ArgumentException($"At most {MaximumMetadataItems} {label} may be registered.", parameterName);
        if (requireItems && values.Count == 0)
            throw new ArgumentException($"At least one of the {label} must be registered.", parameterName);
        var copy = values.ToArray();
        if (copy.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentifierLength))
            throw new ArgumentException($"The {label} must contain non-empty identifiers of at most {MaximumIdentifierLength} characters.", parameterName);
        if (copy.Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException($"The {label} must be unique.", parameterName);
        return Array.AsReadOnly(copy);
    }

    private static void ValidateRequiredText(string value, int maximumLength, string message, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            throw new ArgumentException(message, parameterName);
    }
}

public sealed record CommandLayerResolution
{
    public bool Success { get; private init; }
    public CommandLayerDescriptor Descriptor { get; private init; }
    public string Error { get; private init; } = string.Empty;

    internal static CommandLayerResolution Resolved(CommandLayerDescriptor descriptor) =>
        new() { Success = true, Descriptor = descriptor };

    internal static CommandLayerResolution Failed(string error) =>
        new() { Success = false, Error = error };
}
