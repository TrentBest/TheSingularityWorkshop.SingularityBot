using SC2APIProtocol;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Gates the first SCV on a Command Center worker-rally command, then restores
/// the Command Center's rally to an actually observed starting mineral target
/// after the dedicated builder's Move command has been accepted.
/// </summary>
public sealed class Sc2OpeningRallyController
{
    private enum PendingAction
    {
        None,
        Temporary,
        Restore
    }

    private PendingAction _pendingAction;
    private ulong? _townHallTag;
    private MapPoint? _temporaryTarget;
    private bool _temporaryAccepted;
    private bool _restoreAccepted;

    public bool IsTemporaryRallyAccepted => _temporaryAccepted;
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
            return null;

        if (!_temporaryAccepted)
        {
            if (_pendingAction != PendingAction.None)
                return null;

            var townHall = observation.Units
                .Where(unit => unit.Alliance == UnitAlliance.Self &&
                               unit.IsLiveVisible && unit.HasPosition &&
                               !observation.DeadUnitTags.Contains(unit.Tag) &&
                               (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
                                unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(unit => unit.Tag)
                .FirstOrDefault();
            if (townHall is null || spatialMap is null)
                return null;

            var origin = new MapPoint(townHall.PositionX, townHall.PositionY);
            var forward = (spatialMap.Center - origin).Normalize();
            var target = origin + forward * 4.5f;
            if (!spatialMap.Bounds.Contains(target))
                return null;

            _townHallTag = townHall.Tag;
            _temporaryTarget = target;
            _pendingAction = PendingAction.Temporary;
            Console.WriteLine($"[Opening] Setting Command Center {_townHallTag} worker rally to Supply Depot staging point ({target.X:F1},{target.Y:F1}) before first SCV production.");

            return CreatePositionCommand(
                townHall.Tag,
                checked((int)gameData.RallyWorkersAbilityId.Value),
                target);
        }

        if (!builderDispatched || _restoreAccepted || _pendingAction != PendingAction.None ||
            _townHallTag is null)
            return null;

        // Use an observed starting worker assignment, not a guessed mineral patch.
        var originalMineral = assignments.Values
            .Where(assignment => assignment.TownHallTag == _townHallTag.Value)
            .OrderBy(assignment => assignment.WorkerTag)
            .FirstOrDefault();
        if (originalMineral is null)
            return null;

        var mineralStillVisible = observation.Units.Any(unit =>
            unit.Tag == originalMineral.MineralTag &&
            unit.Alliance == UnitAlliance.Neutral &&
            unit.IsLiveVisible &&
            unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase));
        if (!mineralStillVisible)
            return null;

        _pendingAction = PendingAction.Restore;
        Console.WriteLine($"[Opening] Restoring Command Center {_townHallTag} worker rally to observed mineral {originalMineral.MineralTag} after builder dispatch.");
        return CreateUnitTargetCommand(
            _townHallTag.Value,
            checked((int)gameData.RallyWorkersAbilityId.Value),
            originalMineral.MineralTag);
    }

    public void OnActionResult(ActionResult? result)
    {
        if (_pendingAction == PendingAction.None)
            return;

        if (result == ActionResult.Success)
        {
            if (_pendingAction == PendingAction.Temporary)
                _temporaryAccepted = true;
            else if (_pendingAction == PendingAction.Restore)
                _restoreAccepted = true;
        }

        _pendingAction = PendingAction.None;
    }

    private static SC2APIProtocol.Action CreatePositionCommand(ulong unitTag, int abilityId, MapPoint target) =>
        new()
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = abilityId,
                    UnitTags = { unitTag },
                    TargetWorldSpacePos = new Point2D { X = target.X, Y = target.Y },
                    QueueCommand = false
                }
            }
        };

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
