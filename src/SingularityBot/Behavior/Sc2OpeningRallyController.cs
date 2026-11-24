using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Leaves the Command Center's native mineral rally untouched while the opening
/// builder is delayed, then explicitly restores it to an observed starting
/// mineral target after the dedicated builder has been dispatched.
/// </summary>
public sealed class Sc2OpeningRallyController
{
    private enum PendingAction
    {
        None,
        Restore
    }

    private PendingAction _pendingAction;
    private bool _restoreAccepted;
    private bool _configurationFailureReported;

    public bool IsRestoreAccepted => _restoreAccepted;

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        SpatialMap spatialMap,
        Sc2GameDataCatalog gameData,
        IReadOnlyDictionary<ulong, WorkerResourceAssignment> assignments,
        bool builderDispatched)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);

        if (gameData.RallyWorkersAbilityId is not > 0)
        {
            if (!_configurationFailureReported)
            {
                Console.WriteLine("[Opening] Cannot restore the Command Center worker rally: SC2 game data did not expose an unambiguous Rally_Workers ability.");
                _configurationFailureReported = true;
            }
            return null;
        }

        // Never redirect newly produced SCVs toward the Depot before the
        // dedicated builder has actually been dispatched. The Command Center's
        // native mineral rally remains untouched during the opening delay.
        if (!builderDispatched || _restoreAccepted || _pendingAction != PendingAction.None)
            return null;

        var originalMineral = assignments.Values
            .OrderBy(assignment => assignment.WorkerTag)
            .FirstOrDefault();
        if (originalMineral is null)
            return null;

        var townHallVisible = observation.Units.Any(unit =>
            unit.Tag == originalMineral.TownHallTag &&
            unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible &&
            (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
             unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase)));
        var mineralStillVisible = observation.Units.Any(unit =>
            unit.Tag == originalMineral.MineralTag &&
            unit.Alliance == UnitAlliance.Neutral &&
            unit.IsLiveVisible &&
            unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase));
        if (!townHallVisible || !mineralStillVisible)
            return null;

        _pendingAction = PendingAction.Restore;
        Console.WriteLine($"[Opening] Restoring Command Center {originalMineral.TownHallTag} worker rally to observed mineral {originalMineral.MineralTag} after builder dispatch.");
        return CreateUnitTargetCommand(
            originalMineral.TownHallTag,
            checked((int)gameData.RallyWorkersAbilityId.Value),
            originalMineral.MineralTag);
    }

    public void OnActionResult(ActionResult? result)
    {
        if (_pendingAction == PendingAction.None)
            return;

        if (result == ActionResult.Success && _pendingAction == PendingAction.Restore)
            _restoreAccepted = true;

        _pendingAction = PendingAction.None;
    }

    private static SC2APIProtocol.Action CreateUnitTargetCommand(ulong unitTag, int abilityId, ulong targetTag) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    UnitTags = { unitTag },
                    TargetUnitTag = targetTag,
                    QueueCommand = false
                }
            }
        };
}
