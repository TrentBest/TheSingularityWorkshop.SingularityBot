namespace SingularityBot.Behavior;

/// <summary>
/// Logical responsibility of a replaceable command layer. These are composition
/// slots, not mandatory military ranks; a bot may omit intermediate echelons.
/// </summary>
public enum CommandLayerKind
{
    Strategic,
    Army,
    TaskForce,
    Platoon,
    Squad,
    Unit
}

/// <summary>
/// A selected implementation for one command-layer slot. ImplementationId
/// selects compiled behavior; PresentationProfileId selects editor-facing
/// language/visual treatment. They are deliberately independent.
/// </summary>
public sealed record CommandLayerSelection
{
    public string SlotId { get; init; } = string.Empty;
    public CommandLayerKind Kind { get; init; }
    public string ImplementationId { get; init; } = string.Empty;
    public string PresentationProfileId { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public IReadOnlyList<string> Accepts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Emits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiresCapabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProvidesCapabilities { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Ordered, declarative command-layer composition. It selects registered
/// implementations; it never loads executable code from a bot definition.
/// Layers are ordered from strategic intent toward individual unit execution.
/// </summary>
public sealed class CommandLayerComposition
{
    public const int MaximumEnabledLayers = 5;
    public int Version { get; init; } = 1;
    public IReadOnlyList<CommandLayerSelection> Layers { get; init; } =
        Array.Empty<CommandLayerSelection>();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1)
            errors.Add("$.version: only version 1 is supported.");
        if (Layers is null)
        {
            errors.Add("$.layers: is required.");
            return errors.AsReadOnly();
        }
        if (Layers.Count > 32)
            errors.Add("$.layers: at most 32 command-layer selections are allowed.");
        if (Layers.Count(layer => layer is not null && layer.Enabled) > MaximumEnabledLayers)
            errors.Add($"$.layers: at most {MaximumEnabledLayers} enabled command layers are allowed.");

        var slots = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Layers.Count; index++)
        {
            var layer = Layers[index];
            var path = $"$.layers[{index}]";
            if (layer is null)
            {
                errors.Add($"{path}: cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(layer.SlotId) || layer.SlotId.Length > 128)
                errors.Add($"{path}.slotId: a stable slot id of 1 to 128 characters is required.");
            else if (!slots.Add(layer.SlotId))
                errors.Add($"{path}.slotId: duplicate slot id '{layer.SlotId}'.");

            if (!Enum.IsDefined(layer.Kind))
                errors.Add($"{path}.kind: unknown command-layer kind.");
            if (string.IsNullOrWhiteSpace(layer.ImplementationId) || layer.ImplementationId.Length > 160)
                errors.Add($"{path}.implementationId: a registered implementation id of 1 to 160 characters is required.");
            if (string.IsNullOrWhiteSpace(layer.PresentationProfileId) || layer.PresentationProfileId.Length > 160)
                errors.Add($"{path}.presentationProfileId: a presentation profile id of 1 to 160 characters is required.");

            ValidateIds(layer.Accepts, $"{path}.accepts", errors);
            ValidateIds(layer.Emits, $"{path}.emits", errors);
            ValidateIds(layer.RequiresCapabilities, $"{path}.requiresCapabilities", errors);
            ValidateIds(layer.ProvidesCapabilities, $"{path}.providesCapabilities", errors);
        }

        // The authoring order is the command direction: strategic -> tactical -> unit.
        // Where both adjacent layers declare contracts, require a real hand-off.
        for (var index = 0; index + 1 < Layers.Count; index++)
        {
            var upper = Layers[index];
            var lower = Layers[index + 1];
            if (upper is null || lower is null || !upper.Enabled || !lower.Enabled)
                continue;
            if (upper.Emits.Count == 0 || lower.Accepts.Count == 0)
                continue;
            if (!upper.Emits.Intersect(lower.Accepts, StringComparer.Ordinal).Any())
                errors.Add($"$.layers[{index}]: emits no contract accepted by the next layer '{lower.SlotId}'.");
        }

        return errors.AsReadOnly();
    }

    private static void ValidateIds(IReadOnlyList<string> values, string path, List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"{path}: cannot be null.");
            return;
        }
        if (values.Count > 64)
            errors.Add($"{path}: at most 64 identifiers are allowed.");
        if (values.Any(string.IsNullOrWhiteSpace))
            errors.Add($"{path}: identifiers cannot be empty.");
        if (values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal).Count() != values.Count(value => !string.IsNullOrWhiteSpace(value)))
            errors.Add($"{path}: identifiers must be unique.");
    }
}
