using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// First opening gate: request SCV production from an idle Command Center before
/// worker routing. The request is latched until the order is observed or rejected.
/// </summary>
public sealed class Sc2OpeningProductionController
{
    private bool _awaitingObservedOrder;
    private uint _requestGameLoop;

    public SC2APIProtocol.Action TryCreateScvTrainingAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        if (gameData.ScvTrainAbilityId is not > 0 ||
            gameData.ScvMineralCost == 0)
            return null;

        var commandCenters = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag) &&
                           (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
                            unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        if (commandCenters.Length == 0)
            return null;

        // An observed training order is stronger evidence than a locally remembered request.
        if (commandCenters.Any(center => center.Orders.Any(order =>
                order.AbilityId == gameData.ScvTrainAbilityId.Value)))
        {
            _awaitingObservedOrder = false;
            return null;
        }

        // A request is only held through the next observation. If the order is
        // absent then, the API did not produce the expected game-state evidence.
        if (_awaitingObservedOrder && observation.GameLoop <= _requestGameLoop)
            return null;
        _awaitingObservedOrder = false;

        if (observation.Minerals < gameData.ScvMineralCost ||
            observation.FoodCap < observation.FoodUsed ||
            observation.FoodCap - observation.FoodUsed < gameData.ScvFoodRequired)
            return null;

        var idleCommandCenter = commandCenters.FirstOrDefault(center => !center.HasOrders);
        if (idleCommandCenter is null)
            return null;

        _awaitingObservedOrder = true;
        _requestGameLoop = observation.GameLoop;
        return new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)gameData.ScvTrainAbilityId.Value),
                    UnitTags = { idleCommandCenter.Tag },
                    QueueCommand = false
                }
            }
        };
    }

    public void OnActionResult(ActionResult? result)
    {
        if (result is null || result != ActionResult.Success)
            _awaitingObservedOrder = false;
    }
}
