using System.Text.Json;
using System.Text.Json.Serialization;

namespace SingularityBot.Behavior;

/// <summary>
/// Declarative map-authored military plan. It describes named places, reusable
/// routes and coordinated operations; it never contains SC2 protocol actions.
/// </summary>
public sealed class MilitaryPlanDefinition
{
    public int Version { get; init; } = 1;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<MilitaryLocationDefinition> Locations { get; init; } = Array.Empty<MilitaryLocationDefinition>();
    public IReadOnlyList<MilitaryRouteDefinition> Routes { get; init; } = Array.Empty<MilitaryRouteDefinition>();
    public IReadOnlyList<MilitaryOperationDefinition> Operations { get; init; } = Array.Empty<MilitaryOperationDefinition>();

    public static MilitaryPlanDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Military plan JSON is required.", nameof(json));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return JsonSerializer.Deserialize<MilitaryPlanDefinition>(json, options)
            ?? throw new InvalidOperationException("Military plan JSON did not contain a plan.");
    }
}

public sealed record MilitaryLocationDefinition(
    string Id,
    string Name,
    float X,
    float Y,
    float Radius = 0);

public sealed record MilitaryWaypointDefinition(
    string Id,
    float X,
    float Y);

[Flags]
public enum MilitaryRouteConstraints
{
    None = 0,
    AvoidKnownThreats = 1,
    AvoidContact = 2,
    PreferPathableTerrain = 4
}

public sealed record MilitaryRouteDefinition(
    string Id,
    string Name,
    IReadOnlyList<MilitaryWaypointDefinition> Waypoints,
    MilitaryRouteConstraints Constraints = MilitaryRouteConstraints.PreferPathableTerrain);

public enum MilitaryForceRole
{
    MainForce,
    Vanguard,
    Screen,
    Flank,
    Harass,
    Escort,
    Reserve,
    Scout,
    Siege
}

public enum MilitaryCompletionCondition
{
    AtObjective,
    TargetNeutralized,
    AllAssignedUnitsArrived,
    ManualRelease
}

public sealed record MilitaryOperationDefinition(
    string Id,
    string Name,
    MilitaryForceRole Role,
    string RouteId,
    string ObjectiveLocationId,
    int RequiredUnitCount = 1,
    IReadOnlyList<string> DependsOn = null,
    string RequiredAbility = null,
    string SynchronizationGroup = null,
    MilitaryCompletionCondition Completion = MilitaryCompletionCondition.AtObjective);

public static class MilitaryPlanValidator
{
    private const MilitaryRouteConstraints KnownConstraints =
        MilitaryRouteConstraints.AvoidKnownThreats |
        MilitaryRouteConstraints.AvoidContact |
        MilitaryRouteConstraints.PreferPathableTerrain;

    public static IReadOnlyList<string> Validate(MilitaryPlanDefinition plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = new List<string>();
        var locations = plan.Locations ?? Array.Empty<MilitaryLocationDefinition>();
        var routes = plan.Routes ?? Array.Empty<MilitaryRouteDefinition>();
        var operations = plan.Operations ?? Array.Empty<MilitaryOperationDefinition>();
        if (plan.Version != 1) errors.Add("$.version: only version 1 is supported.");
        if (string.IsNullOrWhiteSpace(plan.Id)) errors.Add("$.id: a stable plan id is required.");
        if (string.IsNullOrWhiteSpace(plan.Name)) errors.Add("$.name: a plan name is required.");
        if (locations.Count == 0) errors.Add("$.locations: at least one named location is required.");
        if (routes.Count == 0) errors.Add("$.routes: at least one named route is required.");
        if (operations.Count == 0) errors.Add("$.operations: at least one operation is required.");

        var locationIds = UniqueIds(locations.Select(item => item?.Id), "$.locations", errors);
        var routeIds = UniqueIds(routes.Select(item => item?.Id), "$.routes", errors);
        var operationIds = UniqueIds(operations.Select(item => item?.Id), "$.operations", errors);

        for (var index = 0; index < locations.Count; index++)
        {
            var location = locations[index];
            var path = $"$.locations[{index}]";
            if (location is null)
            {
                errors.Add($"{path}: a location cannot be null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(location.Name)) errors.Add($"{path}.name: a display name is required.");
            if (!float.IsFinite(location.X) || !float.IsFinite(location.Y))
                errors.Add($"{path}: coordinates must be finite.");
            if (!float.IsFinite(location.Radius) || location.Radius < 0)
                errors.Add($"{path}.radius: must be finite and non-negative.");
        }

        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            var path = $"$.routes[{index}]";
            if (route is null)
            {
                errors.Add($"{path}: a route cannot be null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(route.Name)) errors.Add($"{path}.name: a display name is required.");
            if (route.Waypoints is null || route.Waypoints.Count < 2)
            {
                errors.Add($"{path}.waypoints: a route requires at least two named waypoints.");
                continue;
            }
            if ((route.Constraints & ~KnownConstraints) != 0)
                errors.Add($"{path}.constraints: contains unsupported route constraints.");

            _ = UniqueIds(route.Waypoints.Select(point => point?.Id), $"{path}.waypoints", errors);
            for (var pointIndex = 0; pointIndex < route.Waypoints.Count; pointIndex++)
            {
                var point = route.Waypoints[pointIndex];
                if (point is null)
                {
                    errors.Add($"{path}.waypoints[{pointIndex}]: a waypoint cannot be null.");
                    continue;
                }
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                    errors.Add($"{path}.waypoints[{pointIndex}]: coordinates must be finite.");
            }
        }

        for (var index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            var path = $"$.operations[{index}]";
            if (operation is null)
            {
                errors.Add($"{path}: an operation cannot be null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(operation.Name)) errors.Add($"{path}.name: a display name is required.");
            if (!Enum.IsDefined(operation.Role)) errors.Add($"{path}.role: unknown force role.");
            if (!Enum.IsDefined(operation.Completion)) errors.Add($"{path}.completion: unknown completion condition.");
            if (operation.RequiredUnitCount < 1)
                errors.Add($"{path}.requiredUnitCount: must be at least 1.");
            if (string.IsNullOrWhiteSpace(operation.RouteId) || !routeIds.Contains(operation.RouteId))
                errors.Add($"{path}.routeId: references an unknown route.");
            if (string.IsNullOrWhiteSpace(operation.ObjectiveLocationId) ||
                !locationIds.Contains(operation.ObjectiveLocationId))
                errors.Add($"{path}.objectiveLocationId: references an unknown location.");
            if (operation.RequiredAbility is not null && string.IsNullOrWhiteSpace(operation.RequiredAbility))
                errors.Add($"{path}.requiredAbility: use null when no ability is required.");
            foreach (var dependency in operation.DependsOn ?? Array.Empty<string>())
            {
                if (dependency == operation.Id)
                    errors.Add($"{path}.dependsOn: an operation cannot depend on itself.");
                else if (!operationIds.Contains(dependency))
                    errors.Add($"{path}.dependsOn: unknown operation '{dependency}'.");
            }
        }

        foreach (var operation in operations.Where(item => item is not null))
        {
            var route = routes.FirstOrDefault(item => item is not null && item.Id == operation.RouteId);
            var objective = locations.FirstOrDefault(item => item is not null && item.Id == operation.ObjectiveLocationId);
            var finalWaypoint = route?.Waypoints?.LastOrDefault();
            if (objective is null || finalWaypoint is null)
                continue;

            var dx = finalWaypoint.X - objective.X;
            var dy = finalWaypoint.Y - objective.Y;
            var arrivalRadius = objective.Radius > 0 ? objective.Radius : 1.5f;
            if (dx * dx + dy * dy > arrivalRadius * arrivalRadius)
                errors.Add($"$.operations[{operation.Id}].routeId: route '{route.Id}' does not terminate inside objective '{objective.Id}'.");
        }

        if (operationIds.Count == operations.Count && HasDependencyCycle(operations))
            errors.Add("$.operations: dependency graph contains a cycle.");
        return errors;
    }

    private static HashSet<string> UniqueIds(IEnumerable<string> ids, string path, List<string> errors)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                errors.Add($"{path}[{index}].id: a stable id is required.");
            else if (!result.Add(id))
                errors.Add($"{path}[{index}].id: duplicate id '{id}'.");
            index++;
        }
        return result;
    }

    private static bool HasDependencyCycle(IReadOnlyList<MilitaryOperationDefinition> operations)
    {
        var byId = operations.GroupBy(item => item.Id, StringComparer.Ordinal)
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
