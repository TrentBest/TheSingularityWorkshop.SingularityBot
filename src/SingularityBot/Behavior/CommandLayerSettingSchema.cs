namespace SingularityBot.Behavior;

/// <summary>Data shape used by the authoring surface to render a trusted setting control.</summary>
public enum CommandLayerSettingValueKind
{
    Boolean,
    Integer,
    Number,
    String,
    Enumeration,
    StringList,
    Object
}

/// <summary>Lifecycle boundary at which a setting may be changed.</summary>
public enum CommandLayerSettingMutability
{
    DefinitionOnly,
    PreMatch,
    RuntimeSafe
}

/// <summary>
/// Declarative metadata for one registered implementation setting. DefaultJson is data,
/// never an expression. Conditions use another setting ID and a JSON literal equality value;
/// arbitrary scripts and executable visibility expressions are not accepted.
/// </summary>
public sealed record CommandLayerSettingDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CommandLayerSettingValueKind ValueKind { get; init; }
    public string DefaultJson { get; init; } = "null";
    public IReadOnlyList<string> AllowedValues { get; init; } = Array.Empty<string>();
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public string Units { get; init; } = string.Empty;
    public int? Precision { get; init; }
    public bool Required { get; init; } = true;
    public int? MaximumItems { get; init; }
    public CommandLayerSettingMutability Mutability { get; init; } = CommandLayerSettingMutability.DefinitionOnly;
    public string? VisibleWhenSettingId { get; init; }
    public string? VisibleWhenValueJson { get; init; }
    public string? EnabledWhenSettingId { get; init; }
    public string? EnabledWhenValueJson { get; init; }
    public IReadOnlyList<CommandLayerSettingDescriptor> Children { get; init; } =
        Array.Empty<CommandLayerSettingDescriptor>();
}

/// <summary>Validates and snapshots trusted authoring metadata before registry publication.</summary>
public static class CommandLayerSettingSchema
{
    private const int MaximumDepth = 8;
    private const int MaximumSettingsPerObject = 64;
    private const int MaximumTextLength = 1024;

    public static IReadOnlyList<string> Validate(
        IReadOnlyList<CommandLayerSettingDescriptor> settings,
        int schemaVersion = 1)
    {
        var errors = new List<string>();
        if (schemaVersion < 1)
            errors.Add("settingsSchemaVersion: must be positive.");
        ValidateObject(settings, "$.settings", 0, errors);
        return errors.AsReadOnly();
    }

    /// <summary>Returns a deep immutable snapshot after validation.</summary>
    public static IReadOnlyList<CommandLayerSettingDescriptor> Snapshot(
        IReadOnlyList<CommandLayerSettingDescriptor> settings,
        int schemaVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = Validate(settings, schemaVersion);
        if (errors.Count != 0)
            throw new ArgumentException("Invalid command-layer settings schema: " + string.Join(" ", errors), nameof(settings));
        return Array.AsReadOnly(settings.Select(Clone).ToArray());
    }

    private static void ValidateObject(
        IReadOnlyList<CommandLayerSettingDescriptor> settings,
        string path,
        int depth,
        List<string> errors)
    {
        if (settings is null)
        {
            errors.Add(path + ": is required.");
            return;
        }
        if (depth > MaximumDepth)
        {
            errors.Add(path + ": nested settings exceed the maximum depth of " + MaximumDepth + ".");
            return;
        }
        if (settings.Count > MaximumSettingsPerObject)
            errors.Add(path + ": at most " + MaximumSettingsPerObject + " settings are allowed.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setting in settings)
        {
            if (setting is null)
            {
                errors.Add(path + ": setting descriptors cannot be null.");
                continue;
            }

            var settingPath = path + "." + setting.Id;
            if (string.IsNullOrWhiteSpace(setting.Id) || setting.Id.Length > 160 ||
                !char.IsAsciiLetterLower(setting.Id[0]) ||
                setting.Id.Any(character => !(char.IsAsciiLetterLower(character) ||
                    char.IsAsciiDigit(character) || character is '.' or '-' or '_')))
                errors.Add(path + ": setting IDs must start with a lowercase letter and contain only lowercase letters, digits, dots, hyphens, or underscores (maximum 160 characters).");
            else if (!ids.Add(setting.Id))
                errors.Add(settingPath + ": duplicate setting ID.");

            if (string.IsNullOrWhiteSpace(setting.DisplayName) || setting.DisplayName.Length > 160)
                errors.Add(settingPath + ": a display name of 1 to 160 characters is required.");
            if (setting.Description is null || setting.Description.Length > MaximumTextLength)
                errors.Add(settingPath + ": description cannot exceed " + MaximumTextLength + " characters.");
            if (!Enum.IsDefined(setting.ValueKind))
                errors.Add(settingPath + ": unknown value kind.");
            if (!Enum.IsDefined(setting.Mutability))
                errors.Add(settingPath + ": unknown mutability.");

            System.Text.Json.JsonDocument? defaultDocument = null;
            try
            {
                defaultDocument = System.Text.Json.JsonDocument.Parse(setting.DefaultJson);
                if (!MatchesKind(setting.ValueKind, defaultDocument.RootElement))
                    errors.Add(settingPath + ": default JSON value does not match " + setting.ValueKind + ".");
            }
            catch (System.Text.Json.JsonException)
            {
                errors.Add(settingPath + ": defaultJson must contain one valid JSON value.");
            }

            ValidateConstraints(setting, settingPath, defaultDocument?.RootElement, errors);
            ValidateCondition(setting.VisibleWhenSettingId, setting.VisibleWhenValueJson, settingPath + ".visibleWhen", errors);
            ValidateCondition(setting.EnabledWhenSettingId, setting.EnabledWhenValueJson, settingPath + ".enabledWhen", errors);

            if (setting.ValueKind == CommandLayerSettingValueKind.Object)
            {
                ValidateObject(setting.Children, settingPath + ".children", depth + 1, errors);
                if (setting.AllowedValues.Count != 0)
                    errors.Add(settingPath + ": object settings cannot declare allowedValues.");
            }
            else if (setting.Children is { Count: > 0 })
                errors.Add(settingPath + ": only object settings may declare child settings.");

            if (setting.ValueKind is not (CommandLayerSettingValueKind.Enumeration or CommandLayerSettingValueKind.StringList) &&
                setting.AllowedValues.Count != 0)
                errors.Add(settingPath + ": allowedValues is only valid for enumeration or string-list settings.");

            defaultDocument?.Dispose();
        }

        foreach (var setting in settings.Where(setting => setting is not null))
        {
            CheckConditionReference(setting.VisibleWhenSettingId, setting.Id + ".visibleWhen", ids, errors);
            CheckConditionReference(setting.EnabledWhenSettingId, setting.Id + ".enabledWhen", ids, errors);
        }
    }

    private static void ValidateConstraints(
        CommandLayerSettingDescriptor setting,
        string path,
        System.Text.Json.JsonElement? defaultValue,
        List<string> errors)
    {
        if (setting.Minimum.HasValue && !double.IsFinite(setting.Minimum.Value) ||
            setting.Maximum.HasValue && !double.IsFinite(setting.Maximum.Value))
            errors.Add(path + ": numeric bounds must be finite.");
        if (setting.Minimum.HasValue && setting.Maximum.HasValue && setting.Minimum > setting.Maximum)
            errors.Add(path + ": minimum cannot exceed maximum.");
        if (setting.Precision is < 0 or > 12)
            errors.Add(path + ": precision must be between 0 and 12.");
        if (setting.MaximumItems is < 0 or > 4096)
            errors.Add(path + ": maximumItems must be between 0 and 4096.");
        if (setting.ValueKind is not (CommandLayerSettingValueKind.Integer or CommandLayerSettingValueKind.Number) &&
            (setting.Minimum.HasValue || setting.Maximum.HasValue || setting.Precision.HasValue || !string.IsNullOrEmpty(setting.Units)))
            errors.Add(path + ": numeric bounds, units, and precision require an integer or number setting.");
        if (setting.ValueKind != CommandLayerSettingValueKind.StringList && setting.MaximumItems.HasValue)
            errors.Add(path + ": maximumItems is only valid for string-list settings.");

        if (setting.AllowedValues is null ||
            setting.AllowedValues.Any(string.IsNullOrWhiteSpace) ||
            setting.AllowedValues.Distinct(StringComparer.Ordinal).Count() != setting.AllowedValues.Count)
            errors.Add(path + ": allowedValues must contain unique non-empty values.");
        if (setting.ValueKind == CommandLayerSettingValueKind.Enumeration && setting.AllowedValues.Count == 0)
            errors.Add(path + ": enumeration settings require allowedValues.");
        if (setting.ValueKind == CommandLayerSettingValueKind.StringList && setting.MaximumItems is null)
            errors.Add(path + ": string-list settings require a bounded maximumItems value.");

        if (defaultValue.HasValue)
        {
            var value = defaultValue.Value;
            if (setting.ValueKind == CommandLayerSettingValueKind.Enumeration &&
                value.ValueKind == System.Text.Json.JsonValueKind.String &&
                !setting.AllowedValues.Contains(value.GetString()!, StringComparer.Ordinal))
                errors.Add(path + ": default value is not in allowedValues.");
            if (setting.ValueKind == CommandLayerSettingValueKind.Integer && value.ValueKind == System.Text.Json.JsonValueKind.Number &&
                value.TryGetInt64(out var integer) &&
                (setting.Minimum.HasValue && integer < setting.Minimum || setting.Maximum.HasValue && integer > setting.Maximum))
                errors.Add(path + ": default integer is outside its declared range.");
            if (setting.ValueKind == CommandLayerSettingValueKind.Number && value.ValueKind == System.Text.Json.JsonValueKind.Number &&
                value.TryGetDouble(out var number) &&
                (setting.Minimum.HasValue && number < setting.Minimum || setting.Maximum.HasValue && number > setting.Maximum))
                errors.Add(path + ": default number is outside its declared range.");
            if (setting.ValueKind == CommandLayerSettingValueKind.StringList && value.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var items = value.EnumerateArray().ToArray();
                if (items.Length > setting.MaximumItems)
                    errors.Add(path + ": default list exceeds maximumItems.");
                if (items.Any(item => item.ValueKind != System.Text.Json.JsonValueKind.String))
                    errors.Add(path + ": string-list defaults must contain only strings.");
                if (setting.AllowedValues.Count > 0 && items.Any(item => item.ValueKind == System.Text.Json.JsonValueKind.String &&
                    !setting.AllowedValues.Contains(item.GetString()!, StringComparer.Ordinal)))
                    errors.Add(path + ": default list contains a value outside allowedValues.");
            }
        }
    }

    private static bool MatchesKind(CommandLayerSettingValueKind kind, System.Text.Json.JsonElement value) => kind switch
    {
        CommandLayerSettingValueKind.Boolean => value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False,
        CommandLayerSettingValueKind.Integer => value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt64(out _),
        CommandLayerSettingValueKind.Number => value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
        CommandLayerSettingValueKind.String or CommandLayerSettingValueKind.Enumeration => value.ValueKind == System.Text.Json.JsonValueKind.String,
        CommandLayerSettingValueKind.StringList => value.ValueKind == System.Text.Json.JsonValueKind.Array,
        CommandLayerSettingValueKind.Object => value.ValueKind == System.Text.Json.JsonValueKind.Object,
        _ => false
    };

    private static void ValidateCondition(string? settingId, string? valueJson, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(settingId) != string.IsNullOrWhiteSpace(valueJson))
            errors.Add(path + ": setting ID and comparison JSON value must be supplied together.");
        if (valueJson is null)
            return;
        try { using var document = System.Text.Json.JsonDocument.Parse(valueJson); }
        catch (System.Text.Json.JsonException) { errors.Add(path + ": comparison value must be valid JSON."); }
    }

    private static void CheckConditionReference(string? targetId, string path, HashSet<string> ids, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(targetId) && !ids.Contains(targetId))
            errors.Add(path + ": references an unknown setting ID '" + targetId + "'.");
    }

    private static CommandLayerSettingDescriptor Clone(CommandLayerSettingDescriptor setting) => setting with
    {
        AllowedValues = Array.AsReadOnly(setting.AllowedValues.ToArray()),
        Children = Array.AsReadOnly(setting.Children.Select(Clone).ToArray())
    };
}
