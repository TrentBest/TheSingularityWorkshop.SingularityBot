using System.Text.Json;
using System.Text.Json.Serialization;
using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// Portable, data-only doctrine artifact emitted by Forge. It selects trusted
/// compiled registrations; it never contains executable code.
/// </summary>
public sealed class BotDoctrineDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public int SchemaVersion { get; init; } = 2;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public BotDoctrinePresentation Presentation { get; init; } = new();
    public IReadOnlyList<BotDoctrineSetting> Settings { get; init; } = Array.Empty<BotDoctrineSetting>();
    public IReadOnlyList<BotDoctrineServiceRequirement> Services { get; init; } = Array.Empty<BotDoctrineServiceRequirement>();
    public IReadOnlyList<BotDoctrineMethodBinding> Methods { get; init; } = Array.Empty<BotDoctrineMethodBinding>();
    public IReadOnlyList<BotDoctrinePolicy> Policies { get; init; } = Array.Empty<BotDoctrinePolicy>();
    public IReadOnlyList<BotRaceInitializationPlan> InitializationPlans { get; init; } = Array.Empty<BotRaceInitializationPlan>();
    public IReadOnlyList<BotDoctrineMatchup> Matchups { get; init; } = Array.Empty<BotDoctrineMatchup>();

    public IReadOnlyList<string> Validate() => BotDoctrineValidator.Validate(this);

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static BotDoctrineDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Doctrine JSON is required.", nameof(json));
        var definition = JsonSerializer.Deserialize<BotDoctrineDefinition>(json, JsonOptions)
            ?? throw new JsonException("Doctrine JSON did not contain a definition.");
        var errors = definition.Validate();
        if (errors.Count != 0)
            throw new InvalidOperationException("Bot doctrine is invalid: " + string.Join(" ", errors));
        return definition;
    }

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

public sealed record BotDoctrinePresentation
{
    public string ProfileId { get; init; } = "presentation.default";
    public string DisplayName { get; init; } = "Doctrine";
    public string PrimaryRoleLabel { get; init; } = "Supreme Commander";
    public string SubordinateRoleLabel { get; init; } = "Commander";
}

public enum BotDoctrineValueType { Boolean, Integer, Number, String, StringChoice }
public enum BotDoctrineMethodScope { Bot, Commander, Unit }
public enum BotDoctrineNodeKind { Commander, UnitFsm }
public enum BotDoctrineComparison { LessThan, LessThanOrEqual, Equal, GreaterThanOrEqual, GreaterThan }
public enum BotDoctrineInitializationPhase { Identify, Resolve, RegisterServices, ConstructControllers, BindEvents, DiscoverUnits, Activate }

/// <summary>One typed bot-wide editor setting; ValueJson contains data, never code.</summary>
public sealed record BotDoctrineSetting
{
    public string Id { get; init; } = string.Empty;
    public BotDoctrineValueType ValueType { get; init; }
    public string ValueJson { get; init; } = "null";
    public string Label { get; init; } = string.Empty;
    public string EditorControl { get; init; } = "text";
    public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();
}

public sealed record BotDoctrineServiceRequirement
{
    public string ServiceId { get; init; } = string.Empty;
    public bool Required { get; init; } = true;
}

public sealed record BotDoctrineMethodBinding
{
    public string EventId { get; init; } = string.Empty;
    public string MethodId { get; init; } = string.Empty;
    public BotDoctrineMethodScope Scope { get; init; }
    public string NodeId { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
}

public sealed record BotDoctrinePolicy
{
    public string Id { get; init; } = string.Empty;
    public string MetricId { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public BotDoctrineComparison Comparison { get; init; }
    public double Threshold { get; init; }
    public string Effect { get; init; } = string.Empty;
}

public sealed record BotRaceInitializationPlan
{
    public Race Race { get; init; }
    public IReadOnlyList<BotDoctrineInitializationStep> Steps { get; init; } = Array.Empty<BotDoctrineInitializationStep>();
}

public sealed record BotDoctrineInitializationStep
{
    public string Id { get; init; } = string.Empty;
    public BotDoctrineInitializationPhase Phase { get; init; }
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ServiceIds { get; init; } = Array.Empty<string>();
}

public sealed record BotDoctrineMatchup
{
    public Race OwnRace { get; init; }
    public Race OpponentRace { get; init; }
    public IReadOnlyList<BotDoctrineNode> PrimaryCommanders { get; init; } = Array.Empty<BotDoctrineNode>();
}

public sealed record BotDoctrineNode
{
    public string NodeId { get; init; } = string.Empty;
    public BotDoctrineNodeKind Kind { get; init; }
    public string ImplementationId { get; init; } = string.Empty;
    public string PresentationProfileId { get; init; } = string.Empty;
    public BotDoctrineMethodScope Scope { get; init; }
    public IReadOnlyList<string> RequiresCapabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ProvidesCapabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EmitsIntents { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AcceptsIntents { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BotDoctrineNode> Children { get; init; } = Array.Empty<BotDoctrineNode>();
}

public static class BotDoctrineValidator
{
    private static readonly Race[] Races = { Race.Terran, Race.Zerg, Race.Protoss };
    private const int MaximumNodes = 2048;
    private const int MaximumDepth = 12;

    public static IReadOnlyList<string> Validate(BotDoctrineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        if (definition.SchemaVersion != 2) errors.Add("$.schemaVersion: only version 2 is supported.");
        Required(definition.Id, "$.id", 128, errors);
        Required(definition.Name, "$.name", 160, errors);
        if (definition.Presentation is null) errors.Add("$.presentation: is required.");
        else
        {
            Required(definition.Presentation.ProfileId, "$.presentation.profileId", 128, errors);
            Required(definition.Presentation.DisplayName, "$.presentation.displayName", 160, errors);
            Required(definition.Presentation.PrimaryRoleLabel, "$.presentation.primaryRoleLabel", 80, errors);
            Required(definition.Presentation.SubordinateRoleLabel, "$.presentation.subordinateRoleLabel", 80, errors);
        }

        if (definition.Settings is null) errors.Add("$.settings: is required.");
        var settingIds = new HashSet<string>(StringComparer.Ordinal);
        var settings = definition.Settings ?? Array.Empty<BotDoctrineSetting>();
        CheckCount(settings, "$.settings", 128, errors);
        for (var i = 0; i < settings.Count; i++)
        {
            var s = settings[i];
            var p = $"$.settings[{i}]";
            if (s is null) { errors.Add($"{p}: cannot be null."); continue; }
            Unique(s.Id, p + ".id", settingIds, 128, errors);
            Required(s.Label, p + ".label", 160, errors);
            if (!Enum.IsDefined(s.ValueType)) errors.Add($"{p}.valueType: unknown value type.");
            if (s.EditorControl is not ("toggle" or "dropdown" or "text" or "slider" or "number"))
                errors.Add($"{p}.editorControl: unsupported control.");
            try
            {
                if (string.IsNullOrWhiteSpace(s.ValueJson))
                {
                    errors.Add($"{p}.valueJson: is required.");
                    continue;
                }
                using var value = JsonDocument.Parse(s.ValueJson);
                var kind = value.RootElement.ValueKind;
                var valid = s.ValueType switch
                {
                    BotDoctrineValueType.Boolean => kind is JsonValueKind.True or JsonValueKind.False,
                    BotDoctrineValueType.Integer => kind == JsonValueKind.Number && value.RootElement.TryGetInt64(out _),
                    BotDoctrineValueType.Number => kind == JsonValueKind.Number && double.IsFinite(value.RootElement.GetDouble()),
                    BotDoctrineValueType.String or BotDoctrineValueType.StringChoice => kind == JsonValueKind.String,
                    _ => false
                };
                if (!valid) errors.Add($"{p}.valueJson: does not match declared valueType.");
                if (s.ValueType == BotDoctrineValueType.StringChoice)
                {
                    var choices = s.Choices ?? Array.Empty<string>();
                    var selected = kind == JsonValueKind.String ? value.RootElement.GetString() : null;
                    if (choices.Count == 0 || choices.Any(string.IsNullOrWhiteSpace) ||
                        choices.Distinct(StringComparer.Ordinal).Count() != choices.Count ||
                        !choices.Contains(selected, StringComparer.Ordinal))
                        errors.Add($"{p}.choices: must contain unique non-empty values and include the selected string value.");
                }
            }
            catch (JsonException) { errors.Add($"{p}.valueJson: must be valid JSON data."); }
        }

        if (definition.Services is null) errors.Add("$.services: is required.");
        var serviceIds = new HashSet<string>(StringComparer.Ordinal);
        var declaredServices = definition.Services ?? Array.Empty<BotDoctrineServiceRequirement>();
        CheckCount(declaredServices, "$.services", 128, errors);
        for (var i = 0; i < declaredServices.Count; i++)
        {
            var service = declaredServices[i];
            if (service is null) { errors.Add($"$.services[{i}]: cannot be null."); continue; }
            Unique(service.ServiceId, $"$.services[{i}].serviceId", serviceIds, 160, errors);
        }

        if (definition.Policies is null) errors.Add("$.policies: is required.");
        var policyIds = new HashSet<string>(StringComparer.Ordinal);
        var policies = definition.Policies ?? Array.Empty<BotDoctrinePolicy>();
        CheckCount(policies, "$.policies", 256, errors);
        for (var i = 0; i < policies.Count; i++)
        {
            var policy = policies[i];
            var p = $"$.policies[{i}]";
            if (policy is null) { errors.Add($"{p}: cannot be null."); continue; }
            Unique(policy.Id, p + ".id", policyIds, 128, errors);
            Required(policy.MetricId, p + ".metricId", 160, errors);
            Required(policy.Unit, p + ".unit", 48, errors);
            Required(policy.Effect, p + ".effect", 160, errors);
            if (!Enum.IsDefined(policy.Comparison) || !double.IsFinite(policy.Threshold))
                errors.Add($"{p}: comparison and finite threshold are required.");
        }

        if (definition.Methods is null) errors.Add("$.methods: is required.");
        var methodKeys = new HashSet<string>(StringComparer.Ordinal);
        var methods = definition.Methods ?? Array.Empty<BotDoctrineMethodBinding>();
        CheckCount(methods, "$.methods", 512, errors);
        for (var i = 0; i < methods.Count; i++)
        {
            var method = methods[i];
            var p = $"$.methods[{i}]";
            if (method is null) { errors.Add($"{p}: cannot be null."); continue; }
            Required(method.EventId, p + ".eventId", 160, errors);
            Required(method.MethodId, p + ".methodId", 160, errors);
            if (!Enum.IsDefined(method.Scope)) errors.Add($"{p}.scope: unknown scope.");
            if (method.Scope != BotDoctrineMethodScope.Bot && string.IsNullOrWhiteSpace(method.NodeId))
                errors.Add($"{p}.nodeId: is required for commander- and unit-scoped methods.");
            if (!string.IsNullOrEmpty(method.PolicyId) && !policyIds.Contains(method.PolicyId))
                errors.Add($"{p}.policyId: references an unknown policy.");
            if (!methodKeys.Add($"{method.EventId}|{method.MethodId}|{method.NodeId}"))
                errors.Add($"{p}: duplicate event/method/node binding.");
        }

        var plans = definition.InitializationPlans ?? Array.Empty<BotRaceInitializationPlan>();
        if (plans.Count != 3) errors.Add("$.initializationPlans: exactly one plan per playable race is required.");
        var planRaces = new HashSet<Race>();
        for (var i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            var p = $"$.initializationPlans[{i}]";
            if (plan is null) { errors.Add($"{p}: cannot be null."); continue; }
            if (!Races.Contains(plan.Race) || !planRaces.Add(plan.Race)) errors.Add($"{p}.race: must be a unique concrete playable race.");
            ValidateInitialization(plan, p, serviceIds, errors);
        }
        foreach (var race in Races)
            if (!planRaces.Contains(race)) errors.Add($"$.initializationPlans: missing {race} plan.");

        var matchups = definition.Matchups ?? Array.Empty<BotDoctrineMatchup>();
        if (matchups.Count != 9) errors.Add("$.matchups: exactly nine concrete matchups are required.");
        var matchupKeys = new HashSet<(Race, Race)>();
        var totalNodes = 0;
        for (var i = 0; i < matchups.Count; i++)
        {
            var matchup = matchups[i];
            var p = $"$.matchups[{i}]";
            if (matchup is null) { errors.Add($"{p}: cannot be null."); continue; }
            if (!Races.Contains(matchup.OwnRace) || !Races.Contains(matchup.OpponentRace) ||
                !matchupKeys.Add((matchup.OwnRace, matchup.OpponentRace)))
                errors.Add($"{p}: must identify a unique concrete own-race/opponent-race pair.");
            if (matchup.PrimaryCommanders is null || matchup.PrimaryCommanders.Count == 0)
                errors.Add($"{p}.primaryCommanders: at least one primary commander is required.");
            else
            {
                var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var root in matchup.PrimaryCommanders)
                {
                    if (root is not null && root.Kind != BotDoctrineNodeKind.Commander)
                        errors.Add($"{p}.primaryCommanders: every root must be a Commander node.");
                    ValidateNode(root, p + ".primaryCommanders", 1, nodeIds, ref totalNodes, errors, null);
                }
            }
        }
        foreach (var own in Races)
        foreach (var opponent in Races)
            if (!matchupKeys.Contains((own, opponent))) errors.Add($"$.matchups: missing {own}-vs-{opponent}.");
        if (totalNodes > MaximumNodes) errors.Add($"$.matchups: at most {MaximumNodes} hierarchy nodes are allowed.");

        return errors.AsReadOnly();
    }

    private static void ValidateInitialization(BotRaceInitializationPlan plan, string path, HashSet<string> services, List<string> errors)
    {
        var steps = plan.Steps ?? Array.Empty<BotDoctrineInitializationStep>();
        if (steps.Count == 0) { errors.Add($"{path}.steps: at least one initialization step is required."); return; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var completed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (step, index) in steps.Select((value, index) => (value, index)))
        {
            var p = $"{path}.steps[{index}]";
            if (step is null) { errors.Add($"{p}: cannot be null."); continue; }
            Unique(step.Id, p + ".id", ids, 128, errors);
            if (!Enum.IsDefined(step.Phase)) errors.Add($"{p}.phase: unknown phase.");
            foreach (var dependency in step.DependsOn ?? Array.Empty<string>())
                if (!completed.Contains(dependency)) errors.Add($"{p}.dependsOn: dependency '{dependency}' must appear earlier.");
            foreach (var service in step.ServiceIds ?? Array.Empty<string>())
                if (!services.Contains(service)) errors.Add($"{p}.serviceIds: unknown service '{service}'.");
            completed.Add(step.Id);
        }
        var phases = steps.Where(s => s is not null).Select(s => s.Phase).ToArray();
        if (!phases.SequenceEqual(phases.OrderBy(x => x))) errors.Add($"{path}.steps: initialization phases must be ordered.");
        if (phases.Length == 0 || phases[^1] != BotDoctrineInitializationPhase.Activate)
            errors.Add($"{path}.steps: final phase must be Activate.");
    }

    private static void ValidateNode(BotDoctrineNode node, string path, int depth, HashSet<string> ids, ref int count, List<string> errors, BotDoctrineNode parent)
    {
        count++;
        var p = $"{path}[{count - 1}]";
        if (node is null) { errors.Add($"{p}: cannot be null."); return; }
        Unique(node.NodeId, p + ".nodeId", ids, 128, errors);
        Required(node.ImplementationId, p + ".implementationId", 160, errors);
        Required(node.PresentationProfileId, p + ".presentationProfileId", 160, errors);
        if (!Enum.IsDefined(node.Kind) || !Enum.IsDefined(node.Scope)) errors.Add($"{p}: unknown node kind or scope.");
        var children = node.Children ?? Array.Empty<BotDoctrineNode>();
        ValidateNodeIds(node.RequiresCapabilities, p + ".requiresCapabilities", errors);
        ValidateNodeIds(node.ProvidesCapabilities, p + ".providesCapabilities", errors);
        ValidateNodeIds(node.EmitsIntents, p + ".emitsIntents", errors);
        ValidateNodeIds(node.AcceptsIntents, p + ".acceptsIntents", errors);
        if (parent is not null && parent.EmitsIntents is { Count: > 0 } && node.AcceptsIntents is { Count: > 0 } &&
            !parent.EmitsIntents.Intersect(node.AcceptsIntents, StringComparer.Ordinal).Any())
            errors.Add($"{p}: parent emits no intent accepted by this child.");
        if (depth > MaximumDepth) errors.Add($"{p}: hierarchy exceeds maximum depth {MaximumDepth}.");
        if (node.Kind == BotDoctrineNodeKind.UnitFsm && children.Count != 0)
            errors.Add($"{p}.children: unit FSM nodes must be leaves.");
        if (node.Kind == BotDoctrineNodeKind.Commander && children.Any(child => child is null || child.Kind != BotDoctrineNodeKind.UnitFsm && child.Kind != BotDoctrineNodeKind.Commander))
            errors.Add($"{p}.children: only commander or unit-FSM nodes are permitted.");
        if (node.Kind == BotDoctrineNodeKind.UnitFsm && node.Scope != BotDoctrineMethodScope.Unit)
            errors.Add($"{p}.scope: unit FSM leaves require Unit scope.");
        if (node.Kind == BotDoctrineNodeKind.Commander && node.Scope == BotDoctrineMethodScope.Unit)
            errors.Add($"{p}.scope: commander nodes cannot have Unit scope.");
        if (depth <= MaximumDepth)
            foreach (var child in children) ValidateNode(child, path + ".children", depth + 1, ids, ref count, errors, node);
    }

    private static void ValidateNodeIds(IReadOnlyList<string> values, string path, List<string> errors)
    {
        if (values is null) { errors.Add($"{path}: cannot be null."); return; }
        if (values.Count > 64 || values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            errors.Add($"{path}: must contain at most 64 unique non-empty identifiers.");
    }

    private static void CheckCount<T>(IReadOnlyList<T> list, string path, int max, List<string> errors)
    {
        if (list is null) errors.Add($"{path}: is required.");
        else if (list.Count > max) errors.Add($"{path}: at most {max} entries are allowed.");
    }
    private static void Required(string value, string path, int max, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) errors.Add($"{path}: a value of 1 to {max} characters is required.");
    }
    private static void Unique(string value, string path, HashSet<string> seen, int max, List<string> errors)
    {
        Required(value, path, max, errors);
        if (!string.IsNullOrWhiteSpace(value) && !seen.Add(value)) errors.Add($"{path}: duplicate id '{value}'.");
    }
}
