using SingularityBot.Observation;

namespace SingularityBot.Spatial;

/// <summary>
/// Derives expansion anchors from observed neutral resource clusters. It never
/// guesses map-specific coordinates: every candidate is grounded in a distinct
/// mineral line on the friendly half of the map.
/// </summary>
public static class ExpansionSitePlanner
{
    public static IReadOnlyList<MapPoint> FindCommandCenterCandidates(
        NormalizedObservation observation,
        SpatialMap map)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(map);

        var resources = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible &&
                unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag) &&
                (IsMineral(unit.UnitKind) || IsGeyser(unit.UnitKind)))
            .ToArray();
        var townHalls = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag) &&
                IsTownHall(unit.UnitKind))
            .ToArray();
        if (resources.Length == 0 || townHalls.Length == 0)
            return Array.Empty<MapPoint>();

        var unvisited = resources.ToDictionary(unit => unit.Tag);
        var clusters = new List<UnitObservationFacts[]>();
        while (unvisited.Count > 0)
        {
            var first = unvisited.Values.OrderBy(unit => unit.Tag).First();
            unvisited.Remove(first.Tag);
            var cluster = new List<UnitObservationFacts> { first };
            var frontier = new Queue<UnitObservationFacts>();
            frontier.Enqueue(first);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var neighbor in unvisited.Values
                    .Where(unit => DistanceSquared(current, unit) <= 100f)
                    .ToArray())
                {
                    unvisited.Remove(neighbor.Tag);
                    cluster.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
            clusters.Add(cluster.ToArray());
        }

        var friendlyStart = map.Side.FriendlyStart;
        var occupiedAnchors = townHalls.Select(unit => new MapPoint(unit.PositionX, unit.PositionY)).ToArray();
        var sites = clusters
            .Select(cluster => cluster.Where(unit => IsMineral(unit.UnitKind)).ToArray())
            .Where(minerals => minerals.Length >= 4)
            .Select(minerals =>
            {
                var center = new MapPoint(
                    minerals.Average(unit => unit.PositionX),
                    minerals.Average(unit => unit.PositionY));
                var distance = center.DistanceTo(friendlyStart);
                return new { Center = center, Distance = distance };
            })
            .Where(site => site.Distance >= 14f && map.Side.Assess(site.Center) == MapSide.Friendly)
            .Where(site => occupiedAnchors.All(anchor => anchor.DistanceSquaredTo(site.Center) >= 144f))
            .OrderBy(site => site.Distance)
            .Take(4)
            .ToArray();

        var candidates = new List<MapPoint>();
        foreach (var site in sites)
        {
            var towardResources = (site.Center - friendlyStart).Normalize();
            // Place the town hall behind its mineral line, toward the friendly
            // start, then let SC2's placement query choose a legal footprint.
            var anchor = site.Center - towardResources * 7f;
            var lateral = new MapPoint(-towardResources.Y, towardResources.X);
            foreach (var offset in new (float Forward, float Side)[]
            {
                (0, 0), (-2, 0), (2, 0), (0, 2), (0, -2),
                (-2, 2), (-2, -2), (2, 2), (2, -2)
            })
            {
                var point = anchor + towardResources * offset.Forward + lateral * offset.Side;
                if (map.Bounds.Contains(point))
                    candidates.Add(point);
            }
            // A single nearest mineral line may have no legal town-hall footprint.
            // Include several ranked expansions in the same SC2 placement query
            // so one blocked cluster cannot stall the entire production plan.
            if (candidates.Count >= 36)
                break;
        }
        return candidates;
    }

    private static bool IsMineral(string kind) =>
        kind.Contains("MineralField", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeyser(string kind) =>
        kind.Contains("VespeneGeyser", StringComparison.OrdinalIgnoreCase) ||
        kind.Contains("RichVespene", StringComparison.OrdinalIgnoreCase);

    private static bool IsTownHall(string kind) =>
        kind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase);

    private static float DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return dx * dx + dy * dy;
    }
}
