using System.Text.Json;
using System.Text.Json.Serialization;

namespace SingularityBot.Behavior;

public sealed class ProductionPlanDefinition
{
    public string SchemaVersion { get; init; } = "0.1";
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Race { get; init; } = string.Empty;
    public IReadOnlyList<ProductionStepDefinition> Steps { get; init; } = Array.Empty<ProductionStepDefinition>();

    public static ProductionPlanDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Production plan JSON is required.", nameof(json));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return JsonSerializer.Deserialize<ProductionPlanDefinition>(json, options)
            ?? throw new InvalidOperationException("Production plan JSON did not contain a plan.");
    }
}

public sealed record ProductionStepDefinition(
    string Id,
    string Target,
    string Producer,
    int DesiredCount,
    int EarliestGameLoop,
    int Priority,
    IReadOnlyList<string> DependsOn = null,
    ProductionLane Lane = ProductionLane.Units);

/// <summary>Separates structure and unit assembly intent without creating separate schedulers.</summary>
public enum ProductionLane
{
    Buildings,
    Units
}

public static class ProductionPlanValidator
{
    public static IReadOnlyList<string> Validate(ProductionPlanDefinition plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = new List<string>();

        if (plan.SchemaVersion != "0.1")
            errors.Add("$.schemaVersion: only version '0.1' is supported.");
        if (string.IsNullOrWhiteSpace(plan.Id))
            errors.Add("$.id: a stable plan id is required.");
        if (string.IsNullOrWhiteSpace(plan.Name))
            errors.Add("$.name: a display name is required.");
        if (plan.Steps.Count == 0)
            errors.Add("$.steps: at least one production step is required.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < plan.Steps.Count; index++)
        {
            var step = plan.Steps[index];
            var path = $"$.steps[{index}]";
            if (string.IsNullOrWhiteSpace(step.Id))
                errors.Add($"{path}.id: a stable step id is required.");
            else if (!ids.Add(step.Id))
                errors.Add($"{path}.id: duplicate step id '{step.Id}'.");
            if (string.IsNullOrWhiteSpace(step.Target))
                errors.Add($"{path}.target: a game-data unit name is required.");
            if (string.IsNullOrWhiteSpace(step.Producer))
                errors.Add($"{path}.producer: a producer unit name is required.");
            if (step.DesiredCount < 1)
                errors.Add($"{path}.desiredCount: must be at least 1.");
            if (step.EarliestGameLoop < 0)
                errors.Add($"{path}.earliestGameLoop: cannot be negative.");
            if (step.Priority is < 0 or > 100)
                errors.Add($"{path}.priority: must be between 0 and 100.");
            if (!Enum.IsDefined(step.Lane))
                errors.Add($"{path}.lane: unsupported production lane.");
        }

        var knownIds = plan.Steps.Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var step in plan.Steps)
        {
            foreach (var dependency in step.DependsOn ?? Array.Empty<string>())
            {
                if (dependency == step.Id)
                    errors.Add($"$.steps[{step.Id}].dependsOn: a step cannot depend on itself.");
                else if (!knownIds.Contains(dependency))
                    errors.Add($"$.steps[{step.Id}].dependsOn: unknown step '{dependency}'.");
            }
        }

        if (ids.Count == plan.Steps.Count && HasDependencyCycle(plan.Steps))
            errors.Add("$.steps: dependency graph contains a cycle.");

        return errors;
    }

    private static bool HasDependencyCycle(IReadOnlyList<ProductionStepDefinition> steps)
    {
        var byId = steps.GroupBy(step => step.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            if (visiting.Contains(id)) return true;
            if (!visited.Add(id)) return false;
            visiting.Add(id);
            foreach (var dependency in byId[id].DependsOn ?? Array.Empty<string>())
                if (byId.ContainsKey(dependency) && Visit(dependency))
                    return true;
            visiting.Remove(id);
            return false;
        }

        return byId.Keys.Any(Visit);
    }
}
