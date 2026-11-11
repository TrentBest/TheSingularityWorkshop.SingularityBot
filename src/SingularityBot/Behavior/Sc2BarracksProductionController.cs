using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>Feeds every completed Barracks after the opening Barracks with Marines.</summary>
public sealed class Sc2BarracksProductionController
{
    private ulong? _pendingBarracksTag;
    private uint _pendingGameLoop;

    public SC2APIProtocol.Action TryCreateMarineTrainingAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        if (gameData.MarineTrainAbilityId is not > 0 || gameData.MarineMineralCost == 0)
            return null;

        var barracks = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.BuildProgress >= 1f && unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        // Sc2OpeningMarineController owns the first Barracks and its first
        // Marine. This controller scales the rest of the production line.
        if (barracks.Length < 2)
            return null;
        var secondaryBarracks = barracks.Skip(1).ToArray();

        if (_pendingBarracksTag is ulong pendingTag)
        {
            var pending = secondaryBarracks.FirstOrDefault(unit => unit.Tag == pendingTag);
            if (pending is null)
            {
                ClearPending();
            }
            else if (pending.Orders.Any(order => order.AbilityId == gameData.MarineTrainAbilityId.Value))
            {
                ClearPending();
            }
            else if (observation.GameLoop <= _pendingGameLoop)
            {
                return null;
            }
            else
            {
                ClearPending();
            }
        }

        if (observation.Minerals < gameData.MarineMineralCost ||
            observation.FoodCap < observation.FoodUsed ||
            observation.FoodCap - observation.FoodUsed < gameData.MarineFoodRequired)
            return null;

        var producer = secondaryBarracks.FirstOrDefault(unit => !unit.HasOrders);
        if (producer is null)
            return null;

        _pendingBarracksTag = producer.Tag;
        _pendingGameLoop = observation.GameLoop;
        return new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)gameData.MarineTrainAbilityId.Value),
                    UnitTags = { producer.Tag },
                    QueueCommand = false
                }
            }
        };
    }

    public void OnActionResult(ActionResult? result)
    {
        if (result is null || result != ActionResult.Success)
            ClearPending();
    }

    private void ClearPending()
    {
        _pendingBarracksTag = null;
        _pendingGameLoop = 0;
    }
}
