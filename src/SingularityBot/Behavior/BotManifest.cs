#nullable enable

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SingularityBot.Behavior;

/// <summary>
/// Validated, declarative composition of compiled advisor definitions.
/// The manifest selects behavior; it never supplies executable code.
/// </summary>
public sealed class BotManifest
{
    private static readonly HashSet<string> SupportedDefinitions = new(StringComparer.Ordinal)
    {
        "advisor.military.v1",
        "advisor.economy.v1",
        "advisor.research.v1",
        "advisor.base-layout.v1",
        "advisor.intelligence.v1"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public int SchemaVersion { get; init; }
    public BotManifestBotSettings Bot { get; init; } = new();
    public IReadOnlyList<AdvisorManifestNode> Advisors { get; init; } = Array.Empty<AdvisorManifestNode>();

    public static BotManifest LoadEmbeddedDefault(Assembly? assembly = null)
    {
        assembly ??= typeof(BotManifest).Assembly;
        const string resourceName = "SingularityBot.Manifest.default.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Required embedded manifest resource '{resourceName}' was not found.");

        BotManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BotManifest>(stream, JsonOptions)
                ?? throw new JsonException("The embedded manifest was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The embedded default bot manifest is invalid JSON or contains unsupported properties.", exception);
        }

        var errors = manifest.Validate();
        if (errors.Count != 0)
            throw new InvalidOperationException("The embedded default bot manifest failed validation: " + string.Join(" ", errors));

        return manifest;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (SchemaVersion != 1)
            errors.Add("$.schemaVersion: only version 1 is supported.");
        if (Bot is null)
            errors.Add("$.bot: is required.");
        else
        {
            if (Bot.Race is not ("Terran" or "Protoss" or "Zerg"))
                errors.Add("$.bot.race: must be one of the supported playable races (Terran, Protoss, Zerg).");
            if (string.IsNullOrWhiteSpace(Bot.StrategyProfile))
                errors.Add("$.bot.strategyProfile: is required.");
            if (!Bot.DeterministicScheduling)
                errors.Add("$.bot.deterministicScheduling: must be true.");
        }

        if (Advisors is null || Advisors.Count == 0)
        {
            errors.Add("$.advisors: at least one advisor is required.");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var groups = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Advisors.Count; index++)
            ValidateNode(Advisors[index], $"$.advisors[{index}]", 0, ids, groups, errors);

        return errors;
    }

    private static void ValidateNode(
        AdvisorManifestNode? node,
        string path,
        int depth,
        HashSet<string> ids,
        HashSet<string> groups,
        List<string> errors)
    {
        if (node is null)
        {
            errors.Add($"{path}: advisor node cannot be null.");
            return;
        }
        if (depth > 8)
        {
            errors.Add($"{path}: advisor nesting exceeds the maximum depth of 8.");
            return;
        }
        if (string.IsNullOrWhiteSpace(node.Id) || node.Id.Length > 64 ||
            !char.IsAsciiLetterLower(node.Id[0]) ||
            node.Id.Any(character => !(char.IsAsciiLetterLower(character) ||
                                      char.IsAsciiDigit(character) ||
                                      character is '.' or '-')))
            errors.Add($"{path}.id: must start with a lowercase letter and contain only lowercase letters, digits, dots, or hyphens (maximum 64 characters).");
        else if (!ids.Add(node.Id))
            errors.Add($"{path}.id: duplicate advisor id '{node.Id}'.");

        if (!SupportedDefinitions.Contains(node.Definition))
            errors.Add($"{path}.definition: '{node.Definition}' is not a compiled advisor definition.");

        if (string.IsNullOrWhiteSpace(node.ProcessGroup) || node.ProcessGroup.Length > 96)
            errors.Add($"{path}.processGroup: a non-empty process group name of at most 96 characters is required.");
        else if (!groups.Add(node.ProcessGroup))
            errors.Add($"{path}.processGroup: process group '{node.ProcessGroup}' has more than one owner.");

        if (node.Priority is < 0 or > 1000)
            errors.Add($"{path}.priority: must be between 0 and 1000.");

        if (node.Children is null)
            return;
        if (node.Children.Count > 64)
        {
            errors.Add($"{path}.children: at most 64 child advisors are allowed.");
            return;
        }

        for (var index = 0; index < node.Children.Count; index++)
            ValidateNode(node.Children[index], $"{path}.children[{index}]", depth + 1, ids, groups, errors);
    }
}

public sealed class BotManifestBotSettings
{
    public string Race { get; init; } = "Terran";
    public string StrategyProfile { get; init; } = string.Empty;
    public bool DeterministicScheduling { get; init; }
}

public sealed class AdvisorManifestNode
{
    public string Id { get; init; } = string.Empty;
    public string Definition { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string ProcessGroup { get; init; } = string.Empty;
    public int Priority { get; init; }
    public JsonElement Settings { get; init; }
    public IReadOnlyList<AdvisorManifestNode> Children { get; init; } = Array.Empty<AdvisorManifestNode>();
}
