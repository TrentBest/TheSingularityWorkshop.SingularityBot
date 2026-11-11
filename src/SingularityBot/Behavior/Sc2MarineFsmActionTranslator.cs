using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>Translates Marine capability-FSM intents into validated SC2 movement/combat actions.</summary>
public static class Sc2MarineFsmActionTranslator
{
    public static IReadOnlyList<SC2APIProtocol.Action> Translate(
        IReadOnlyList<KeyValuePair<ulong, BehaviorCommand>> intents,
        IReadOnlyCollection<UnitBehaviorContext> contexts,
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData)
    {
        ArgumentNullException.ThrowIfNull(intents);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        var contextByTag = contexts.ToDictionary(context => context.UnitTag);
        var visibleMarines = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
            unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToDictionary(unit => unit.Tag);
        var actions = new List<SC2APIProtocol.Action>();
        var commanded = new HashSet<ulong>();

        foreach (var intent in intents.OrderBy(item => item.Key))
        {
            if (!commanded.Add(intent.Key) ||
                !visibleMarines.TryGetValue(intent.Key, out var marine) ||
                !contextByTag.TryGetValue(intent.Key, out var context) ||
                !context.IsCurrentlyVisible)
                continue;

            if (intent.Value == BehaviorCommand.ScoutSector &&
                context.AssignedSector.HasValue && gameData.MoveAbilityId is > 0)
            {
                var dx = marine.PositionX - context.SectorCenterX;
                var dy = marine.PositionY - context.SectorCenterY;
                if (dx * dx + dy * dy > 2.25f)
                    actions.Add(PositionCommand(marine.Tag, checked((int)gameData.MoveAbilityId.Value),
                        context.SectorCenterX, context.SectorCenterY));
            }
            else if (intent.Value == BehaviorCommand.AttackTarget &&
                     context.AttackTargetTag is ulong targetTag &&
                     visibleMarines.ContainsKey(marine.Tag) && gameData.AttackAbilityId is > 0 &&
                     observation.Units.Any(unit => unit.Tag == targetTag &&
                         unit.Alliance == UnitAlliance.Enemy && unit.IsLiveVisible &&
                         !observation.DeadUnitTags.Contains(unit.Tag)))
            {
                actions.Add(UnitCommand(marine.Tag, checked((int)gameData.AttackAbilityId.Value), targetTag));
            }
        }

        return actions;
    }

    private static SC2APIProtocol.Action PositionCommand(ulong tag, int abilityId, float x, float y) => new()
    {
        ActionRaw = new ActionRaw
        {
            UnitCommand = new ActionRawUnitCommand
            {
                AbilityId = abilityId, UnitTags = { tag },
                TargetWorldSpacePos = new Point2D { X = x, Y = y }, QueueCommand = false
            }
        }
    };

    private static SC2APIProtocol.Action UnitCommand(ulong tag, int abilityId, ulong targetTag) => new()
    {
        ActionRaw = new ActionRaw
        {
            UnitCommand = new ActionRawUnitCommand
            {
                AbilityId = abilityId, UnitTags = { tag }, TargetUnitTag = targetTag, QueueCommand = false
            }
        }
    };
}
