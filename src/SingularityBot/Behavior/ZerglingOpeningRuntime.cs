using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// First playable Zergling opening slice. It asks SC2 to validate Spawning Pool
/// placement, builds with a live Drone, waits for observed structure completion,
/// then trains Zerglings from Larva. One command is emitted per frame; uncertain
/// submissions remain latched rather than being blindly retried.
/// </summary>
public sealed class ZerglingOpeningRuntime
{
    private enum OpeningState { NeedPlacement, PlacementReady, BuildSubmitted, AwaitingPool, Producing, Complete }
    private readonly Sc2UnitFacts _drone;
    private readonly Sc2UnitFacts _pool;
    private readonly Sc2AbilityFacts _poolBuildAbility;
    private readonly Sc2RaceProductionProfile _profile;
    private readonly Sc2UnitFacts _zergling;
    private readonly Sc2UnitFacts _larva;
    private readonly IReadOnlySet<uint> _interruptibleGatherAbilityIds;
    private readonly int _desiredZerglings;
    private readonly HashSet<ulong> _reservedWorkers = new();
    private readonly HashSet<ulong> _pendingLarvaTags = new();
    private int _pendingEggBaseline;
    private readonly List<Point2D> _candidatePoints = new();
    private OpeningState _state = OpeningState.NeedPlacement;
    private ulong _builderTag;
    private Point2D _selectedPlacement;
    private bool _queryPending;
    private bool _buildResultPending;
    private bool _productionResultPending;
    private bool _placementQueryInFlight;
    private int _placementAttempts;
    private bool _failureLogged;

    public ZerglingOpeningRuntime(
        Sc2UnitFacts drone,
        Sc2UnitFacts spawningPool,
        Sc2AbilityFacts poolBuildAbility,
        Sc2RaceProductionProfile profile,
        Sc2UnitFacts zergling,
        Sc2UnitFacts larva,
        IReadOnlySet<uint> interruptibleGatherAbilityIds,
        int desiredZerglings = 2)
    {
        _drone = drone ?? throw new ArgumentNullException(nameof(drone));
        _pool = spawningPool ?? throw new ArgumentNullException(nameof(spawningPool));
        _poolBuildAbility = poolBuildAbility ?? throw new ArgumentNullException(nameof(poolBuildAbility));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _zergling = zergling ?? throw new ArgumentNullException(nameof(zergling));
        _larva = larva ?? throw new ArgumentNullException(nameof(larva));
        _interruptibleGatherAbilityIds = interruptibleGatherAbilityIds ?? throw new ArgumentNullException(nameof(interruptibleGatherAbilityIds));
        if (desiredZerglings < 2 || desiredZerglings % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(desiredZerglings), "Zergling targets must be positive pairs.");
        _desiredZerglings = desiredZerglings;
        if (_pool.AbilityId == 0 || _poolBuildAbility.AbilityId != _pool.AbilityId ||
            !_poolBuildAbility.IsBuilding || _zergling.AbilityId == 0 ||
            !_profile.TargetUnit.Equals(_zergling.Name, StringComparison.OrdinalIgnoreCase) ||
            !_profile.ProducerUnit.Equals(_larva.Name, StringComparison.OrdinalIgnoreCase) ||
            _profile.UnitsPerOrder != 2)
            throw new ArgumentException("Resolved Zerg production game data is inconsistent.");
    }

    public bool IsReady => _state is OpeningState.Producing or OpeningState.Complete;
    public string State => _state.ToString();
    public int PlacementAttempts => _placementAttempts;

    public IReadOnlyList<SC2APIProtocol.Action> OnFrame(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var poolObserved = observation.Units.Any(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.UnitKind.Equals(_pool.Name, StringComparison.OrdinalIgnoreCase) &&
            unit.BuildProgress >= 1f);
        if (poolObserved && _state is not OpeningState.Producing and not OpeningState.Complete)
        {
            _state = OpeningState.Producing;
            _buildResultPending = false; // completed structure observation supersedes a missing callback
            _builderTag = 0;
            _reservedWorkers.Clear();
            Console.WriteLine("[ZergOpening] Completed Spawning Pool observed; enabling Larva production.");
        }

        if (_state is OpeningState.NeedPlacement or OpeningState.PlacementReady)
        {
            if (_state == OpeningState.NeedPlacement)
                PreparePlacementCandidates(observation);
            if (_state == OpeningState.PlacementReady)
            {
                if (observation.Minerals < _pool.MineralCost || observation.Vespene < _pool.VespeneCost)
                    return Array.Empty<SC2APIProtocol.Action>();
                var worker = observation.Units.FirstOrDefault(unit =>
                    unit.Tag == _builderTag && unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                    !observation.DeadUnitTags.Contains(unit.Tag) &&
                    unit.UnitKind.Equals(_drone.Name, StringComparison.OrdinalIgnoreCase) &&
                    !unit.HasCargo && HasOnlyInterruptibleGatherOrders(unit) && !_reservedWorkers.Contains(unit.Tag));
                if (worker is null)
                    return Array.Empty<SC2APIProtocol.Action>();
                var translated = Sc2RaceBuildingActionTranslator.Translate(
                    _pool, _drone, _poolBuildAbility, observation, worker.Tag,
                    _selectedPlacement.X, _selectedPlacement.Y, _reservedWorkers, _interruptibleGatherAbilityIds);
                if (translated.IsBlocked)
                {
                    LogOnce("Spawning Pool build blocked: " + translated.BlockReason);
                    return Array.Empty<SC2APIProtocol.Action>();
                }
                _builderTag = translated.WorkerTag;
                _reservedWorkers.Add(_builderTag);
                _buildResultPending = true;
                _state = OpeningState.BuildSubmitted;
                Console.WriteLine($"[ZergOpening] Submitting Spawning Pool with Drone {_builderTag} at ({_selectedPlacement.X:F1},{_selectedPlacement.Y:F1}).");
                return translated.Actions;
            }
        }

        if (_state == OpeningState.BuildSubmitted || _state == OpeningState.AwaitingPool)
            return Array.Empty<SC2APIProtocol.Action>();

        if (_state == OpeningState.Producing)
        {
            var progress = Sc2RaceProductionProgressReconciler.Reconcile(_profile, _zergling, observation);
            if (_pendingLarvaTags.Count > 0 && progress.InProgressOrders > _pendingEggBaseline)
            {
                _pendingLarvaTags.Clear();
                _productionResultPending = false;
            }
            foreach (var deadTag in observation.DeadUnitTags)
                _pendingLarvaTags.Remove(deadTag);
            if (_pendingLarvaTags.Count == 0 && observation.DeadUnitTags.Count > 0)
                _productionResultPending = false;
            // Do not issue another Larva order until the prior order is resolved
            // by its action result and/or observed Egg/death evidence.
            if (_pendingLarvaTags.Count > 0 || _productionResultPending)
                return Array.Empty<SC2APIProtocol.Action>();
            var remaining = _desiredZerglings - progress.AccountedUnits;
            if (remaining <= 0)
            {
                _state = OpeningState.Complete;
                Console.WriteLine($"[ZergOpening] Target reached: completed={progress.CompletedUnits}, inProgress={progress.InProgressUnits}.");
                return Array.Empty<SC2APIProtocol.Action>();
            }
            var translated = Sc2RaceProductionActionTranslator.Translate(
                _profile, _zergling, _larva, observation, remaining, _pendingLarvaTags);
            if (translated.IsBlocked)
                return Array.Empty<SC2APIProtocol.Action>();
            _productionResultPending = true;
            var producerTag = translated.ProducerTags[0];
            _pendingLarvaTags.Add(producerTag);
            _pendingEggBaseline = progress.InProgressOrders;
            Console.WriteLine($"[ZergOpening] Submitting one Larva order from producer {producerTag}; expected output={_profile.UnitsPerOrder}.");
            return new[] { translated.Actions[0] };
        }

        return Array.Empty<SC2APIProtocol.Action>();
    }

    private void PreparePlacementCandidates(NormalizedObservation observation)
    {
        if (_queryPending || _placementQueryInFlight || _selectedPlacement is not null || _placementAttempts >= 3)
            return;
        var hatchery = observation.Units.FirstOrDefault(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            (unit.UnitKind is "Hatchery" or "Lair" or "Hive") && unit.HasPosition);
        var drone = observation.Units.FirstOrDefault(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.UnitKind.Equals(_drone.Name, StringComparison.OrdinalIgnoreCase) &&
            !unit.HasOrders && !unit.HasCargo);
        if (hatchery is null || drone is null)
            return;
        _builderTag = drone.Tag;
        _candidatePoints.Clear();
        // Candidate generation is intentionally broad; SC2 placement queries,
        // not geometry heuristics, are authoritative for legal sites.
        for (var ring = 0; ring < 3; ring++)
        {
            var radius = 5.5f + ring * 1.5f;
            for (var i = 0; i < 16; i++)
            {
                var angle = (float)(Math.PI * 2 * i / 16.0);
                _candidatePoints.Add(new Point2D
                {
                    X = hatchery.PositionX + MathF.Cos(angle) * radius,
                    Y = hatchery.PositionY + MathF.Sin(angle) * radius
                });
            }
        }
        _queryPending = true;
    }

    private bool HasOnlyInterruptibleGatherOrders(UnitObservationFacts unit) =>
        !unit.HasOrders || (unit.Orders.Count > 0 && unit.Orders.All(order =>
            _interruptibleGatherAbilityIds.Contains(order.AbilityId)));

    public RequestQuery TakeQueryRequest()
    {
        if (!_queryPending || _builderTag == 0 || _candidatePoints.Count == 0)
            return null;
        _queryPending = false;
        _placementQueryInFlight = true;
        _placementAttempts++;
        var query = new RequestQuery { IgnoreResourceRequirements = true };
        foreach (var point in _candidatePoints)
            query.Placements.Add(new RequestQueryBuildingPlacement
            {
                AbilityId = checked((int)_poolBuildAbility.AbilityId),
                TargetPos = point,
                PlacingUnitTag = _builderTag
            });
        return query;
    }

    public void OnQueryResult(ResponseQuery response)
    {
        _placementQueryInFlight = false;
        if (_state != OpeningState.NeedPlacement || _builderTag == 0)
            return;
        var count = Math.Min(response?.Placements.Count ?? 0, _candidatePoints.Count);
        for (var i = 0; i < count; i++)
        {
            if (response.Placements[i].Result != ActionResult.Success)
                continue;
            _selectedPlacement = _candidatePoints[i];
            _state = OpeningState.PlacementReady;
            Console.WriteLine($"[ZergOpening] SC2 approved Spawning Pool placement ({_selectedPlacement.X:F1},{_selectedPlacement.Y:F1}).");
            return;
        }
        _builderTag = 0;
        _state = OpeningState.NeedPlacement;
        if (_placementAttempts < 3)
            _queryPending = false; // a fresh observation selects a fresh Drone/site set
        else
            LogOnce("SC2 rejected all Spawning Pool placement candidates after three queries; remaining fail-closed.");
    }

    public void OnActionResult(ResponseAction response)
    {
        if (_buildResultPending)
        {
            if (response is null || response.Result.Count == 0)
                return; // retain the latch; an absent result is not a rejection
            _buildResultPending = false;
            var result = response.Result[0];
            if (result == ActionResult.Success)
            {
                _state = OpeningState.AwaitingPool;
                Console.WriteLine("[ZergOpening] Spawning Pool build accepted; waiting for authoritative structure observation.");
            }
            else
            {
                _reservedWorkers.Clear();
                _builderTag = 0;
                _selectedPlacement = null;
                _state = OpeningState.NeedPlacement;
                Console.WriteLine($"[ZergOpening] Spawning Pool action rejected/unknown ({result?.ToString() ?? "no result"}); placement will be reconsidered.");
            }
            return;
        }
        if (_productionResultPending)
        {
            if (response is null || response.Result.Count == 0)
                return; // keep the producer reservation and stop issuing further orders
            _productionResultPending = false;
            var result = response.Result[0];
            if (result != ActionResult.Success)
            {
                _pendingLarvaTags.Clear();
                Console.WriteLine($"[ZergOpening] Larva production action rejected: {result}; will retry only after a fresh observation.");
            }
        }
    }

    private void LogOnce(string message)
    {
        if (_failureLogged) return;
        _failureLogged = true;
        Console.WriteLine("[ZergOpening] FAIL-CLOSED: " + message);
    }
}
