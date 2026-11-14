using System.Globalization;
using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Executes only planner-accepted Scout jobs. This is the single strategic
/// Scout-to-SC2 command boundary; missing game data or pathing evidence fails closed.
/// </summary>
public static class StrategicScoutActionExecutor
{
    public static StrategicScoutExecutionResult Execute(
        IReadOnlyList<StrategicJobDecision> decisions,
        NormalizedObservation observation,
        SpatialMap map,
        Sc2GameDataCatalog gameData,
        SpatialTerrain terrain,
        SpatialTopology topology)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(gameData);

        var results = new List<StrategicJobDecision>();
        var actions = new List<SC2APIProtocol.Action>();
        var issuedUnits = new HashSet<ulong>();
        foreach (var decision in decisions)
        {
            if (!decision.IsAccepted || decision.Posting.Kind != StrategicJobKind.Scout)
            {
                results.Add(decision);
                continue;
            }

            var posting = decision.Posting;
            if (posting.Constraints is null ||
                !posting.Constraints.TryGetValue("unitTag", out var tagText) ||
                !ulong.TryParse(tagText, NumberStyles.None, CultureInfo.InvariantCulture, out var tag))
            {
                results.Add(Reject(decision, "Accepted Scout posting lost its valid unitTag constraint."));
                continue;
            }

            var unit = observation.Units.FirstOrDefault(candidate =>
                candidate.Tag == tag && candidate.Alliance == UnitAlliance.Self &&
                candidate.IsLiveVisible && candidate.HasPosition &&
                !observation.DeadUnitTags.Contains(candidate.Tag));
            if (unit is null)
            {
                results.Add(Reject(decision, $"Scout unit {tag} is no longer live and visible."));
                continue;
            }

            if (!TryParseTarget(posting.Target, out var target) || !map.Bounds.Contains(target))
            {
                results.Add(Reject(decision, "Scout target is invalid or outside map bounds."));
                continue;
            }

            if (gameData.MoveAbilityId is not > 0 ||
                !gameData.TryGetAbility(gameData.MoveAbilityId.Value, out var moveAbility) ||
                !moveAbility.Available ||
                moveAbility.Target != AbilityData.Types.Target.Point)
            {
                results.Add(Reject(decision, "Authoritative generic Move ability is unavailable or not point-targeted."));
                continue;
            }

            if (terrain?.Pathing is null || topology is null)
            {
                results.Add(Reject(decision, "Scout movement requires authoritative pathing and connected-region data."));
                continue;
            }

            if (!terrain.TryIsPathable(target, out var pathable) || !pathable ||
                !topology.AreConnected(new MapPoint(unit.PositionX, unit.PositionY), target))
            {
                results.Add(Reject(decision, "Scout target is not confirmed pathable and connected to the unit."));
                continue;
            }

            if (HasMoveOrderNear(unit, gameData, gameData.MoveAbilityId.Value, target))
            {
                results.Add(new StrategicJobDecision(posting, true, "Unit already has the matching Move order."));
                continue;
            }

            if (!issuedUnits.Add(tag))
            {
                results.Add(Reject(decision, $"Scout unit {tag} already has an action in this frame."));
                continue;
            }

            actions.Add(new SC2APIProtocol.Action
            {
                ActionRaw = new ActionRaw
                {
                    UnitCommand = new ActionRawUnitCommand
                    {
                        AbilityId = checked((int)gameData.MoveAbilityId.Value),
                        UnitTags = { tag },
                        TargetWorldSpacePos = new Point2D { X = target.X, Y = target.Y },
                        QueueCommand = false
                    }
                }
            });
            results.Add(new StrategicJobDecision(posting, true,
                "Validated Move action emitted for the uniquely reserved live Scout."));
        }

        return new StrategicScoutExecutionResult(actions.AsReadOnly(), results.AsReadOnly());
    }

    private static bool TryParseTarget(string value, out MapPoint target)
    {
        target = default;
        var parts = value?.Split(',');
        if (parts is not { Length: 2 } ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.IsFinite(x) || !float.IsFinite(y))
            return false;
        target = new MapPoint(x, y);
        return true;
    }

    private static bool HasMoveOrderNear(
        UnitObservationFacts unit,
        Sc2GameDataCatalog gameData,
        uint abilityId,
        MapPoint target)
    {
        var remapped = gameData.TryGetAbility(abilityId, out var ability)
            ? ability.RemapsToAbilityId
            : 0;
        return unit.Orders.Any(order =>
            (order.AbilityId == abilityId || remapped > 0 && order.AbilityId == remapped) &&
            order.TargetPositionX.HasValue && order.TargetPositionY.HasValue &&
            new MapPoint(order.TargetPositionX.Value, order.TargetPositionY.Value)
                .DistanceSquaredTo(target) <= 2.25f);
    }

    private static StrategicJobDecision Reject(StrategicJobDecision decision, string reason) =>
        new(decision.Posting, false, reason);
}

public sealed record StrategicScoutExecutionResult(
    IReadOnlyList<SC2APIProtocol.Action> Actions,
    IReadOnlyList<StrategicJobDecision> Decisions);
