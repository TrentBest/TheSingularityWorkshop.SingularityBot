using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Translates named military operation intents into conservative SC2 move or
/// point-ability commands. It validates authored route segments against the
/// cached pathing snapshot and visible enemy weapon envelopes.
/// </summary>
public sealed class Sc2MilitaryOperationActionTranslator
{
    private const float ArrivalRadiusSquared = 4f;
    private const float UnknownThreatClearance = 8f;
    private readonly Dictionary<(string OperationId, ulong UnitTag, string RouteId), int> _waypointIndices = new();

    public MilitaryOperationTranslation Translate(
        MilitaryOperationIntent intent,
        MilitaryPlanDefinition plan,
        IReadOnlyCollection<ulong> assignedUnitTags,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        SpatialTerrain terrain,
        SpatialTopology topology)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(assignedUnitTags);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        var operation = plan.Operations.FirstOrDefault(item =>
            string.Equals(item.Id, intent.OperationId, StringComparison.Ordinal));
        var route = plan.Routes.FirstOrDefault(item =>
            string.Equals(item.Id, intent.RouteId, StringComparison.Ordinal));
        var location = plan.Locations.FirstOrDefault(item =>
            string.Equals(item.Id, intent.ObjectiveLocationId, StringComparison.Ordinal));
        var errors = MilitaryPlanValidator.Validate(plan);
        if (errors.Count != 0)
            return Blocked("Military plan failed validation: " + string.Join(" ", errors));
        if (operation is null || route is null || location is null)
            return Blocked("Operation, route or objective location is missing from the supplied plan.");
        if (!string.Equals(operation.RouteId, intent.RouteId, StringComparison.Ordinal) ||
            !string.Equals(operation.ObjectiveLocationId, intent.ObjectiveLocationId, StringComparison.Ordinal) ||
            !string.Equals(operation.RequiredAbility, intent.RequiredAbility, StringComparison.Ordinal) ||
            operation.RequiredUnitCount != intent.RequiredUnitCount ||
            operation.Role != intent.Role)
            return Blocked("The operation intent does not match the validated plan definition.");

        var unitsByTag = observation.Units
            .Where(unit => unit.IsLiveVisible && unit.HasPosition &&
                unit.Alliance == UnitAlliance.Self && !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);
        var assigned = assignedUnitTags.Distinct().OrderBy(tag => tag)
            .Where(unitsByTag.ContainsKey).Select(tag => unitsByTag[tag]).ToArray();
        if (assigned.Length < intent.RequiredUnitCount)
            return Blocked($"Only {assigned.Length} of {intent.RequiredUnitCount} required assigned units are currently visible.");

        if (!string.IsNullOrWhiteSpace(intent.RequiredAbility))
            return TranslatePointAbility(intent, location, assigned, gameData, terrain);

        if (gameData.MoveAbilityId is not > 0)
            return Blocked("The authoritative generic Move ability is unavailable.");
        if (!ValidateRoute(route, assigned, observation, gameData, terrain, topology, out var reason))
            return Blocked(reason);

        var actions = new List<SC2APIProtocol.Action>();
        var moveAbilityId = checked((int)gameData.MoveAbilityId.Value);
        foreach (var unit in assigned)
        {
            var key = (intent.OperationId, unit.Tag, route.Id);
            var waypointIndex = _waypointIndices.TryGetValue(key, out var previous) ? previous : 0;
            while (waypointIndex < route.Waypoints.Count)
            {
                var waypoint = route.Waypoints[waypointIndex];
                if (DistanceSquared(unit.PositionX, unit.PositionY, waypoint.X, waypoint.Y) > ArrivalRadiusSquared)
                    break;
                waypointIndex++;
            }
            _waypointIndices[key] = waypointIndex;
            if (waypointIndex >= route.Waypoints.Count)
                continue;

            var target = route.Waypoints[waypointIndex];
            if (HasPositionOrderNear(unit, moveAbilityId, target.X, target.Y))
                continue;
            actions.Add(PositionCommand(unit.Tag, moveAbilityId, target.X, target.Y));
        }

        return new MilitaryOperationTranslation(actions, false, string.Empty);
    }

    public void Reset(string operationId)
    {
        foreach (var key in _waypointIndices.Keys.Where(key =>
                     string.Equals(key.OperationId, operationId, StringComparison.Ordinal)).ToArray())
            _waypointIndices.Remove(key);
    }

    private static MilitaryOperationTranslation TranslatePointAbility(
        MilitaryOperationIntent intent,
        MilitaryLocationDefinition location,
        IReadOnlyList<UnitObservationFacts> assigned,
        Sc2GameDataCatalog gameData,
        SpatialTerrain terrain)
    {
        var matches = gameData.FindAbilities(intent.RequiredAbility);
        if (matches.Count != 1)
            return Blocked($"Required ability '{intent.RequiredAbility}' did not resolve to exactly one authoritative SC2 ability.");
        var ability = matches[0];
        if (!ability.Available || ability.Target is not (AbilityData.Types.Target.Point or
            AbilityData.Types.Target.PointOrUnit or AbilityData.Types.Target.PointOrNone))
            return Blocked($"Required ability '{intent.RequiredAbility}' is unavailable or does not accept a point target.");
        if (terrain is null ||
            !terrain.TryIsPathable(new MapPoint(location.X, location.Y), out var pathable) ||
            !pathable)
            return Blocked("The ability objective is not confirmed pathable by the cached terrain snapshot.");

        var actions = new List<SC2APIProtocol.Action>();
        foreach (var unit in assigned)
        {
            var distanceSquared = DistanceSquared(unit.PositionX, unit.PositionY, location.X, location.Y);
            var arrivalRadius = location.Radius > 0 ? location.Radius : 1.5f;
            if (distanceSquared <= arrivalRadius * arrivalRadius)
                continue;
            if (ability.CastRange > 0 && distanceSquared > ability.CastRange * ability.CastRange)
                return Blocked($"Assigned unit {unit.Tag} is outside the authoritative cast range for '{intent.RequiredAbility}'.");
            if (HasPositionOrderNear(unit, checked((int)ability.AbilityId), location.X, location.Y))
                continue;
            actions.Add(PositionCommand(unit.Tag, checked((int)ability.AbilityId), location.X, location.Y));
        }

        return new MilitaryOperationTranslation(actions, false, string.Empty);
    }

    private static bool ValidateRoute(
        MilitaryRouteDefinition route,
        IReadOnlyList<UnitObservationFacts> assigned,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        SpatialTerrain terrain,
        SpatialTopology topology,
        out string reason)
    {
        reason = string.Empty;
        var requiresPathing = (route.Constraints & MilitaryRouteConstraints.PreferPathableTerrain) != 0;
        if (requiresPathing && (terrain?.Pathing is null || topology is null))
        {
            reason = "Route requires pathing data, but the cached pathing snapshot or topology is unavailable.";
            return false;
        }

        if (requiresPathing)
        {
            var points = route.Waypoints.Select(point => new MapPoint(point.X, point.Y)).ToArray();
            for (var index = 0; index < points.Length; index++)
            {
                if (!terrain.TryIsPathable(points[index], out var pathable) || !pathable ||
                    !topology.TryGetRegionId(points[index], out _))
                {
                    reason = $"Route waypoint '{route.Waypoints[index].Id}' is not confirmed pathable.";
                    return false;
                }

                if (index > 0 && !IsPathableSegment(points[index - 1], points[index], terrain, topology))
                {
                    reason = $"Route segment '{route.Waypoints[index - 1].Id}' to '{route.Waypoints[index].Id}' is not continuously pathable.";
                    return false;
                }
            }

            foreach (var unit in assigned)
            {
                var start = new MapPoint(unit.PositionX, unit.PositionY);
                if (!IsPathableSegment(start, points[0], terrain, topology))
                {
                    reason = $"Assigned unit {unit.Tag} cannot reach the route's first waypoint through confirmed pathable cells.";
                    return false;
                }
            }
        }

        var avoidKnownThreats = (route.Constraints & MilitaryRouteConstraints.AvoidKnownThreats) != 0;
        var avoidContact = (route.Constraints & MilitaryRouteConstraints.AvoidContact) != 0;
        if (avoidKnownThreats || avoidContact)
        {
            var enemies = observation.Units.Where(unit =>
                unit.Alliance == UnitAlliance.Enemy && unit.IsLiveVisible && unit.HasPosition &&
                !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();
            foreach (var unit in assigned)
            foreach (var enemy in enemies)
            {
                var weaponRange = gameData.WeaponsByUnitType.TryGetValue(enemy.UnitTypeId, out var weapons)
                    ? weapons.Where(weapon => unit.IsFlying
                            ? weapon.TargetType is Weapon.Types.TargetType.Air or Weapon.Types.TargetType.Any
                            : weapon.TargetType is Weapon.Types.TargetType.Ground or Weapon.Types.TargetType.Any)
                        .Select(weapon => weapon.Range).DefaultIfEmpty(0).Max()
                    : 0;
                var clearance = avoidContact
                    ? MathF.Max(UnknownThreatClearance, weaponRange + 5f)
                    : MathF.Max(UnknownThreatClearance * 0.5f, weaponRange + 2f);
                if (DistanceToPolylineSquared(
                        new MapPoint(enemy.PositionX, enemy.PositionY), route.Waypoints) < clearance * clearance)
                {
                    reason = $"Route is blocked by visible enemy contact {enemy.Tag} within the configured threat clearance.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsPathableSegment(
        MapPoint start,
        MapPoint end,
        SpatialTerrain terrain,
        SpatialTopology topology)
    {
        if (!topology.AreConnected(start, end))
            return false;
        var grid = terrain.Pathing;
        var step = MathF.Max(0.1f, MathF.Min(grid.WorldWidth / grid.Width, grid.WorldHeight / grid.Height) * 0.5f);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        var samples = Math.Max(1, (int)MathF.Ceiling(length / step));
        for (var index = 0; index <= samples; index++)
        {
            var fraction = (float)index / samples;
            var point = new MapPoint(start.X + dx * fraction, start.Y + dy * fraction);
            if (!terrain.TryIsPathable(point, out var pathable) || !pathable)
                return false;
        }
        return true;
    }

    private static float DistanceToPolylineSquared(MapPoint point, IReadOnlyList<MilitaryWaypointDefinition> waypoints)
    {
        var minimum = float.MaxValue;
        for (var index = 1; index < waypoints.Count; index++)
        {
            var start = new MapPoint(waypoints[index - 1].X, waypoints[index - 1].Y);
            var end = new MapPoint(waypoints[index].X, waypoints[index].Y);
            minimum = MathF.Min(minimum, DistanceToSegmentSquared(point, start, end));
        }
        return minimum;
    }

    private static float DistanceToSegmentSquared(MapPoint point, MapPoint start, MapPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.000001f)
            return point.DistanceSquaredTo(start);
        var projection = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0f, 1f);
        var closest = new MapPoint(start.X + projection * dx, start.Y + projection * dy);
        return point.DistanceSquaredTo(closest);
    }

    private static bool HasPositionOrderNear(UnitObservationFacts unit, int abilityId, float x, float y) =>
        unit.Orders.Any(order => order.AbilityId == (uint)abilityId &&
            order.TargetPositionX.HasValue && order.TargetPositionY.HasValue &&
            DistanceSquared(order.TargetPositionX.Value, order.TargetPositionY.Value, x, y) <= 2.25f);

    private static SC2APIProtocol.Action PositionCommand(ulong unitTag, int abilityId, float x, float y) => new()
    {
        ActionRaw = new ActionRaw
        {
            UnitCommand = new ActionRawUnitCommand
            {
                AbilityId = abilityId,
                UnitTags = { unitTag },
                TargetWorldSpacePos = new Point2D { X = x, Y = y },
                QueueCommand = false
            }
        }
    };

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static MilitaryOperationTranslation Blocked(string reason) =>
        new(Array.Empty<SC2APIProtocol.Action>(), true, reason);
}

public sealed record MilitaryOperationTranslation(
    IReadOnlyList<SC2APIProtocol.Action> Actions,
    bool IsBlocked,
    string Reason);
