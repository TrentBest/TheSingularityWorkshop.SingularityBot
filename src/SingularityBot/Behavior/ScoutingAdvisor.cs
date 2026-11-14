using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Assigns individual non-worker combat units to distinct strategic scouting
/// points. Assignments remain stable until a unit reaches its current objective.
/// The advisor only reasons from observed map geometry and visible neutral
/// intelligence objects; it does not issue commands itself.
/// </summary>
public sealed class ScoutingAdvisor
{
    private const float ArrivalRadius = 4f;
    private readonly Dictionary<ulong, MapPoint> _assignments = new();
    private readonly HashSet<string> _visitedObjectives = new(StringComparer.Ordinal);
    private readonly HashSet<string> _publishedPostingIds = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<ulong, MapPoint> Assignments => _assignments;

    public IReadOnlyDictionary<ulong, MapPoint> Plan(
        NormalizedObservation observation,
        SpatialMap map,
        IReadOnlyDictionary<uint, CombatWeaponProfile> weaponsByUnitType)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(weaponsByUnitType);

        var combatUnits = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible && unit.HasPosition &&
                !observation.DeadUnitTags.Contains(unit.Tag) &&
                !unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                weaponsByUnitType.TryGetValue(unit.UnitTypeId, out var weapon) &&
                weapon.IsUsable)
            .OrderBy(unit => unit.Tag)
            .ToArray();

        var liveTags = combatUnits.Select(unit => unit.Tag).ToHashSet();
        foreach (var stale in _assignments.Keys.Where(tag => !liveTags.Contains(tag)).ToArray())
            _assignments.Remove(stale);

        foreach (var unit in combatUnits)
        {
            if (!_assignments.TryGetValue(unit.Tag, out var target))
                continue;
            if (DistanceSquared(unit.PositionX, unit.PositionY, target.X, target.Y) <= ArrivalRadius * ArrivalRadius)
            {
                _visitedObjectives.Add(PointKey(target));
                _assignments.Remove(unit.Tag);
            }
        }

        var objectives = BuildObjectives(observation, map)
            .Where(point => !_visitedObjectives.Contains(PointKey(point)))
            .ToArray();
        var reserved = _assignments.Values
            .Select(PointKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var unit in combatUnits)
        {
            if (_assignments.ContainsKey(unit.Tag))
                continue;

            var target = objectives
                .Where(point => !reserved.Contains(PointKey(point)))
                .OrderBy(point => DistanceSquared(unit.PositionX, unit.PositionY, point.X, point.Y))
                .ThenBy(point => point.X)
                .ThenBy(point => point.Y)
                .Select(point => (MapPoint?)point)
                .FirstOrDefault();

            if (target is not MapPoint objective)
                continue;

            _assignments[unit.Tag] = objective;
            reserved.Add(PointKey(objective));
        }

        return new Dictionary<ulong, MapPoint>(_assignments);
    }

    /// <summary>
    /// Publishes current scout assignments as Intelligence-owned strategic jobs.
    /// The board is the handoff to the planner; this method never issues SC2 actions.
    /// </summary>
    public IReadOnlyList<StrategicJobPosting> PublishJobs(
        NormalizedObservation observation,
        SpatialMap map,
        IReadOnlyDictionary<uint, CombatWeaponProfile> weaponsByUnitType,
        AdvisorJobBoard board,
        int priority = 60)
    {
        ArgumentNullException.ThrowIfNull(board);
        var assignments = Plan(observation, map, weaponsByUnitType);
        var currentIds = new HashSet<string>(StringComparer.Ordinal);
        var postings = new List<StrategicJobPosting>(assignments.Count);
        foreach (var (unitTag, point) in assignments.OrderBy(item => item.Key))
        {
            var id = $"scout:unit:{unitTag}";
            currentIds.Add(id);
            postings.Add(board.Upsert(
                AdvisorDomain.Intelligence, id, StrategicJobKind.Scout,
                $"{point.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                1, priority, observation.GameLoop,
                constraints: new Dictionary<string, string>
                {
                    ["unitTag"] = unitTag.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        foreach (var staleId in _publishedPostingIds.Except(currentIds, StringComparer.Ordinal).ToArray())
            board.Withdraw(AdvisorDomain.Intelligence, staleId);
        _publishedPostingIds.Clear();
        _publishedPostingIds.UnionWith(currentIds);
        return postings;
    }

    private static IReadOnlyList<MapPoint> BuildObjectives(
        NormalizedObservation observation,
        SpatialMap map)
    {
        var objectives = new List<MapPoint>();
        foreach (var unit in observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible &&
            unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag)))
        {
            var kind = unit.UnitKind;
            if (kind.Contains("Watchtower", StringComparison.OrdinalIgnoreCase) ||
                kind.Contains("XelNaga", StringComparison.OrdinalIgnoreCase) ||
                kind.Contains("Destructible", StringComparison.OrdinalIgnoreCase) ||
                kind.Contains("Debris", StringComparison.OrdinalIgnoreCase) ||
                kind.Contains("Rock", StringComparison.OrdinalIgnoreCase) ||
                kind.Contains("Blocker", StringComparison.OrdinalIgnoreCase))
                objectives.Add(new MapPoint(unit.PositionX, unit.PositionY));
        }

        // Sector centers provide map-wide exploration targets even when no
        // special neutral object is visible. Exclude the home sector and
        // prioritize the enemy half through distance ordering at assignment.
        foreach (var sector in map.Sectors)
        {
            var center = sector.Center;
            if (map.Side.Assess(center) != MapSide.Friendly &&
                objectives.All(point => point.DistanceSquaredTo(center) > 16f))
                objectives.Add(center);
        }

        return objectives
            .Where(map.Bounds.Contains)
            .Distinct()
            .ToArray();
    }

    private static string PointKey(MapPoint point) =>
        $"{point.X:R}:{point.Y:R}";

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
