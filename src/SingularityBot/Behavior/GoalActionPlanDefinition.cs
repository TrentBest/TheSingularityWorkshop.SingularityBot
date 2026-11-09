using System.Text.Json;
using System.Text.Json.Serialization;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

public enum GoalActionRole { Rally, Scout, Contain, Engage, Fallback }

/// <summary>User-authored strategic posture, independent of production sequencing.</summary>
public sealed class GoalActionPlanDefinition
{
    public int Version { get; init; } = 1;
    public string Id { get; init; } = "containment";
    public string Name { get; init; } = "Contain and observe";
    public GoalActionRole DefaultRole { get; init; } = GoalActionRole.Contain;
    public int ScoutCount { get; init; } = 2;
    public List<SectorId> ScoutSectors { get; init; } = new();
    public bool ExcludeEnemyStartSector { get; init; } = true;
    public bool StutterStepEnabled { get; init; } = true;
    public float FallbackHealthFraction { get; init; } = 0.35f;
    public bool EngageVisibleThreats { get; init; }

    public static GoalActionPlanDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Plan JSON is required.", nameof(json));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<GoalActionPlanDefinition>(json, options)
            ?? throw new InvalidOperationException("Goal action plan JSON was empty.");
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add("$.version: only version 1 is supported.");
        if (string.IsNullOrWhiteSpace(Id)) errors.Add("$.id: a plan id is required.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("$.name: a plan name is required.");
        if (ScoutCount < 0) errors.Add("$.scoutCount: cannot be negative.");
        if (FallbackHealthFraction is < 0 or > 1) errors.Add("$.fallbackHealthFraction: must be between 0 and 1.");
        if (!Enum.IsDefined(DefaultRole)) errors.Add("$.defaultRole: unknown role.");
        if (ScoutSectors.Distinct().Count() != ScoutSectors.Count) errors.Add("$.scoutSectors: duplicate sector.");
        if (ScoutSectors.Any(s => s.Column is < 0 or > 2 || s.Row is < 0 or > 2))
            errors.Add("$.scoutSectors: sector coordinates must be within the 3x3 grid.");
        return errors;
    }
}

/// <summary>Assigns stable scout sectors and strategic roles to visible friendly Marines/Marauders.</summary>
public static class GoalActionPlanner
{
    public static IReadOnlyList<string> Apply(GoalActionPlanDefinition plan, UnitBehaviorRuntime runtime, SpatialMap map)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(map);
        var errors = plan.Validate();
        if (errors.Count != 0) return errors;

        var enemySector = map.GetSector(map.Side.EnemyStart).Id;
        var sectors = (plan.ScoutSectors.Count > 0 ? plan.ScoutSectors : map.Sectors.Select(s => s.Id).ToList())
            .Where(id => !plan.ExcludeEnemyStartSector || id != enemySector)
            .Distinct().OrderBy(id => id.Row).ThenBy(id => id.Column)
            .Take(plan.ScoutCount).ToArray();

        var candidates = runtime.Contexts.Where(c => c.IsValid && c.IsCurrentlyVisible &&
            c.Alliance == UnitAlliance.Self &&
            (c.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) ||
             c.UnitKind.Equals("Marauder", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.UnitTag).ToArray();

        foreach (var unit in candidates)
        {
            unit.GoalRole = plan.DefaultRole;
            unit.AssignedSector = null;
            unit.StutterStepEnabled = plan.StutterStepEnabled;
        }
        for (var i = 0; i < Math.Min(sectors.Length, candidates.Length); i++)
        {
            var unit = candidates[i];
            var sector = map.Sectors.Single(s => s.Id == sectors[i]);
            unit.GoalRole = GoalActionRole.Scout;
            unit.AssignedSector = sector.Id;
            unit.SectorCenterX = sector.Center.X;
            unit.SectorCenterY = sector.Center.Y;
        }
        return Array.Empty<string>();
    }
}
