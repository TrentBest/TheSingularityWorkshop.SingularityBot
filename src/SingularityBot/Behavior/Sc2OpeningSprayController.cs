using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Uses SC2's per-unit available-ability query to prove whether an SCV can spray.
/// If available, issues spray, waits for the API result, then explicitly resumes
/// the same mineral route. No ability ID is guessed.
/// </summary>
public sealed class Sc2OpeningSprayController
{
    private enum Stage
    {
        NotStarted,
        AwaitingAbilityQuery,
        SprayReady,
        SpraySubmitted,
        GatherReady,
        GatherSubmitted,
        Complete,
        Unavailable
    }

    private Stage _stage;
    private ulong? _workerTag;
    private WorkerResourceAssignment _assignment;
    private bool _requiresPoint;
    private uint? _sprayAbilityId;
    private uint? _smartAbilityId;

    public ulong? WorkerTag => _workerTag;
    public bool IsComplete => _stage is Stage.Complete or Stage.Unavailable;

    public RequestQuery PrepareAbilityQuery(
        NormalizedObservation observation,
        IReadOnlyDictionary<ulong, WorkerResourceAssignment> assignments,
        Sc2GameDataCatalog gameData,
        bool initialScvProductionObserved,
        WorkerResourceAssignmentPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(planner);

        if (_stage != Stage.NotStarted || !initialScvProductionObserved)
            return null;

        if (gameData.SprayAbilityId is not > 0 || gameData.SmartAbilityId is not > 0)
        {
            _stage = Stage.Unavailable;
            Console.WriteLine("[Control proof] SC2 game data did not expose both an unambiguous Spray ability and a Smart ability; no Spray command will be fabricated.");
            return null;
        }

        foreach (var assignment in assignments.Values.OrderBy(item => item.WorkerTag))
        {
            var worker = observation.Units.FirstOrDefault(unit =>
                unit.Tag == assignment.WorkerTag &&
                unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible &&
                unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                !unit.HasCargo &&
                !observation.DeadUnitTags.Contains(unit.Tag));

            if (worker is null)
                continue;

            _workerTag = worker.Tag;
            _assignment = assignment;
            _sprayAbilityId = gameData.SprayAbilityId;
            _smartAbilityId = gameData.HarvestGatherAbilityId;
            planner.Reserve(worker.Tag);

            var query = new RequestQuery();
            query.Abilities.Add(new RequestQueryAvailableAbilities { UnitTag = worker.Tag });
            _stage = Stage.AwaitingAbilityQuery;
            return query;
        }

        return null;
    }

    public void OnQueryResult(ResponseQuery response, WorkerResourceAssignmentPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(planner);
        if (_stage != Stage.AwaitingAbilityQuery)
            return;

        var workerAbilities = response?.Abilities.FirstOrDefault(item => item.UnitTag == _workerTag);
        var spray = workerAbilities?.Abilities.FirstOrDefault(item =>
            item.AbilityId == checked((int)_sprayAbilityId!.Value));

        if (spray is null)
        {
            Restore(planner);
            _stage = Stage.Unavailable;
            Console.WriteLine($"[Control proof] SCV {_workerTag} does not report Spray as an available ability; restored its mineral assignment.");
            return;
        }

        _requiresPoint = spray.RequiresPoint;
        _stage = Stage.SprayReady;
        Console.WriteLine($"[Control proof] SC2 confirms Spray ability {_sprayAbilityId} is available to SCV {_workerTag}.");
    }

    public SC2APIProtocol.Action TryCreateAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(planner);

        if (_stage is not (Stage.SprayReady or Stage.GatherReady) || _workerTag is null)
            return null;

        if (observation.DeadUnitTags.Contains(_workerTag.Value))
        {
            planner.Unreserve(_workerTag.Value);
            _stage = Stage.Unavailable;
            return null;
        }

        var worker = observation.Units.FirstOrDefault(unit =>
            unit.Tag == _workerTag.Value &&
            unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible &&
            unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase));

        if (worker is null || worker.HasCargo)
            return null;

        if (_stage == Stage.SprayReady)
        {
            var command = new ActionRawUnitCommand
            {
                AbilityId = checked((int)_sprayAbilityId!.Value),
                UnitTags = { _workerTag.Value },
                QueueCommand = false
            };
            if (_requiresPoint)
                command.TargetWorldSpacePos = new Point2D { X = worker.PositionX, Y = worker.PositionY };

            _stage = Stage.SpraySubmitted;
            return new SC2APIProtocol.Action { ActionRaw = new ActionRaw { UnitCommand = command } };
        }

        if (_gatherAbilityId is not > 0)
        {
            Restore(planner);
            _stage = Stage.Unavailable;
            return null;
        }

        var mineralVisible = observation.Units.Any(unit =>
            unit.Tag == _assignment.MineralTag &&
            unit.Alliance == UnitAlliance.Neutral &&
            unit.IsLiveVisible &&
            unit.UnitKind.Contains("MineralField", StringComparison.OrdinalIgnoreCase));

        if (!mineralVisible)
        {
            Restore(planner);
            _stage = Stage.Unavailable;
            Console.WriteLine($"[Control proof] SCV {_workerTag} cannot resume its original mineral route because mineral {_assignment.MineralTag} is not visible.");
            return null;
        }

        _stage = Stage.GatherSubmitted;
        return new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)_gatherAbilityId.Value),
                    UnitTags = { _workerTag.Value },
                    TargetUnitTag = _assignment.MineralTag,
                    QueueCommand = false
                }
            }
        };
    }

    public void OnActionResult(ActionResult? result, WorkerResourceAssignmentPlanner planner)
    {
        ArgumentNullException.ThrowIfNull(planner);

        if (_stage == Stage.SpraySubmitted)
        {
            if (result == ActionResult.Success)
            {
                _stage = Stage.GatherReady;
                Console.WriteLine($"[Control proof] SC2 accepted Spray for SCV {_workerTag}; resuming its assigned mineral route with Smart next.");
            }
            else
            {
                Restore(planner);
                _stage = Stage.Unavailable;
                Console.WriteLine($"[Control proof] Spray was not accepted for SCV {_workerTag}; original mineral assignment restored.");
            }
            return;
        }

        if (_stage == Stage.GatherSubmitted)
        {
            if (result == ActionResult.Success)
            {
                Restore(planner);
                _stage = Stage.Complete;
                Console.WriteLine($"[Control proof] SC2 accepted the return-to-mining command for SCV {_workerTag} -> mineral {_assignment.MineralTag}.");
            }
            else
            {
                Restore(planner);
                _stage = Stage.Unavailable;
                Console.WriteLine($"[Control proof] Mining resume was rejected for SCV {_workerTag}; mineral assignment restored for normal reconciliation.");
            }
        }
    }

    private void Restore(WorkerResourceAssignmentPlanner planner)
    {
        if (_workerTag is not null && _assignment is not null)
            planner.Restore(_assignment);
        else if (_workerTag is not null)
            planner.Unreserve(_workerTag.Value);
    }
}
