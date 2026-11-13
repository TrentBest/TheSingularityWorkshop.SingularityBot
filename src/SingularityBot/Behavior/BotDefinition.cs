using System.Text.Json;
using System.Text.Json.Serialization;

namespace SingularityBot.Behavior;

/// <summary>
/// The versioned, portable definition of a SingularityBot. It composes existing
/// bot-owned plans and runtime declarations; it is not a second execution engine.
/// </summary>
public sealed class BotDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Race { get; init; } = "Terran";
    public string StrategyProfile { get; init; } = string.Empty;
    public BotManifest Runtime { get; init; } = new();
    public ProductionPlanDefinition Opening { get; init; } = new();
    public GoalActionPlanDefinition DefaultGoals { get; init; } = new();
    public IReadOnlyList<StrategyTransitionDefinition> StrategyTransitions { get; init; } =
        Array.Empty<StrategyTransitionDefinition>();
    public BotAiPolicy Ai { get; init; } = new();

    /// <summary>Parses a strict JSON definition. Unknown properties are rejected.</summary>
    public static BotDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Bot definition JSON is required.", nameof(json));

        var definition = JsonSerializer.Deserialize<BotDefinition>(json, JsonOptions)
            ?? throw new JsonException("Bot definition JSON did not contain a definition.");
        var errors = definition.Validate();
        if (errors.Count != 0)
            throw new InvalidOperationException("Bot definition is invalid: " + string.Join(" ", errors));
        return definition;
    }

    /// <summary>Serializes the portable definition as readable JSON.</summary>
    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Returns deterministic, path-qualified semantic validation errors.</summary>
    public IReadOnlyList<string> Validate() => BotDefinitionValidator.Validate(this);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

/// <summary>
/// Explicit budget and trigger policy for optional model consultations.
/// Model calls are strategic interruptions, never part of the frame heartbeat.
/// </summary>
public sealed class BotAiPolicy
{
    public bool Enabled { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int MaxOnStartCalls { get; init; } = 1;
    public int MaxInMatchCalls { get; init; } = 3;
    public int TimeoutMilliseconds { get; init; } = 15000;
    public int MinimumGameLoopSpacing { get; init; } = 448;
    public IReadOnlyList<string> AllowedTriggers { get; init; } = Array.Empty<string>();
}

/// <summary>Validates a complete authoring artifact against the currently supported runtime.</summary>
public static class BotDefinitionValidator
{
    public static IReadOnlyList<string> Validate(BotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();

        if (definition.SchemaVersion != 1)
            errors.Add("$.schemaVersion: only version 1 is supported.");
        if (string.IsNullOrWhiteSpace(definition.Id))
            errors.Add("$.id: a stable bot id is required.");
        if (string.IsNullOrWhiteSpace(definition.Name))
            errors.Add("$.name: a display name is required.");
        if (!string.Equals(definition.Race, "Terran", StringComparison.Ordinal))
            errors.Add("$.race: only Terran is currently supported by this runtime.");
        if (string.IsNullOrWhiteSpace(definition.StrategyProfile))
            errors.Add("$.strategyProfile: a strategy profile is required.");

        if (definition.Runtime is null)
            errors.Add("$.runtime: the compiled advisor manifest is required.");
        else
        {
            errors.AddRange(definition.Runtime.Validate().Select(error => "$.runtime" + error.TrimStart('$')));
            if (definition.Runtime.Bot is not null)
            {
                if (!string.Equals(definition.Runtime.Bot.Race, definition.Race, StringComparison.Ordinal))
                    errors.Add("$.runtime.bot.race: must match $.race.");
                if (!string.Equals(definition.Runtime.Bot.StrategyProfile, definition.StrategyProfile, StringComparison.Ordinal))
                    errors.Add("$.runtime.bot.strategyProfile: must match $.strategyProfile.");
            }
        }

        if (definition.Opening is null)
            errors.Add("$.opening: an opening production plan is required.");
        else
            errors.AddRange(ProductionPlanValidator.Validate(definition.Opening)
                .Select(error => "$.opening" + error.TrimStart('$')));

        if (definition.DefaultGoals is null)
            errors.Add("$.defaultGoals: a default goal/action plan is required.");
        else
            errors.AddRange(definition.DefaultGoals.Validate()
                .Select(error => "$.defaultGoals" + error.TrimStart('$')));

        var transitions = definition.StrategyTransitions ?? Array.Empty<StrategyTransitionDefinition>();
        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < transitions.Count; index++)
        {
            var transition = transitions[index];
            if (transition is null)
            {
                errors.Add($"$.strategyTransitions[{index}]: cannot be null.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(transition.Id) && !transitionIds.Add(transition.Id))
                errors.Add($"$.strategyTransitions[{index}].id: duplicate transition id '{transition.Id}'.");
            errors.AddRange(transition.Validate()
                .Select(error => $"$.strategyTransitions[{index}]: {error}"));
        }

        ValidateAiPolicy(definition.Ai, errors);
        return errors.AsReadOnly();
    }

    private static void ValidateAiPolicy(BotAiPolicy policy, List<string> errors)
    {
        if (policy is null)
        {
            errors.Add("$.ai: policy is required.");
            return;
        }

        if (policy.MaxOnStartCalls is < 0 or > 1)
            errors.Add("$.ai.maxOnStartCalls: must be between 0 and 1.");
        if (policy.MaxInMatchCalls is < 0 or > 3)
            errors.Add("$.ai.maxInMatchCalls: must be between 0 and 3.");
        if (policy.TimeoutMilliseconds is < 1000 or > 120000)
            errors.Add("$.ai.timeoutMilliseconds: must be between 1000 and 120000.");
        if (policy.MinimumGameLoopSpacing < 0)
            errors.Add("$.ai.minimumGameLoopSpacing: cannot be negative.");

        if (policy.Enabled && (string.IsNullOrWhiteSpace(policy.Provider) ||
                               string.IsNullOrWhiteSpace(policy.Model)))
            errors.Add("$.ai: enabled consultations require a provider and model identifier.");

        var triggers = policy.AllowedTriggers ?? Array.Empty<string>();
        if (triggers.Any(string.IsNullOrWhiteSpace))
            errors.Add("$.ai.allowedTriggers: trigger ids cannot be empty.");
        if (triggers.Distinct(StringComparer.Ordinal).Count() != triggers.Count)
            errors.Add("$.ai.allowedTriggers: trigger ids must be unique.");
        if (triggers.Count > 8)
            errors.Add("$.ai.allowedTriggers: at most 8 trigger ids are allowed.");
        if (!policy.Enabled && (policy.MaxOnStartCalls != 0 || policy.MaxInMatchCalls != 0))
            errors.Add("$.ai: disabled policy must set both call budgets to zero.");
    }
}
