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
    private readonly Dictionary<string, CommandLayerDescriptor> _descriptors =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<CommandLayerDescriptor> Descriptors =>
        _descriptors.Values.ToArray();

    public void Register(CommandLayerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.ImplementationId))
            throw new ArgumentException("An implementation id is required.", nameof(descriptor));
        if (!Enum.IsDefined(descriptor.Kind))
            throw new ArgumentException("A known command-layer kind is required.", nameof(descriptor));
        if (_descriptors.ContainsKey(descriptor.ImplementationId))
            throw new InvalidOperationException($"Implementation '{descriptor.ImplementationId}' is already registered.");
        _descriptors.Add(descriptor.ImplementationId, descriptor);
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
