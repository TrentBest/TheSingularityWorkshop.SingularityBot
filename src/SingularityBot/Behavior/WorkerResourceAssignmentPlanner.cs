using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Stable worker-to-resource reservations for one match. Existing observed gather orders are adopted; new assignments survive
/// observations and are replaced only when their worker or resource is retired.
/// The planner does not issue commands; worker FSMs remain the execution owner.
/// </summary>
public sealed class WorkerResourceAssignmentPlanner
{
    public const int DefaultMineralSaturation = 2;

    private readonly Dictionary<ulong, WorkerResourceAssignment> _assignments = new();
    private uint _nextGeneration;
    private readonly HashSet<ulong> _reservedWorkers = new();
    private readonly Dictionary<(ulong StartTag, ulong MineralTag), double> _pathingDistances = new();
    private readonly List<(ulong StartTag, ulong MineralTag)> _pendingPathingPairs = new();
    private bool _initialPathingQueryIssued;
    private bool _initialPathingPending;
    private uint _initialPathingQueryGameLoop;

    public int Count => _assignments.Count;

    /// <summary>
    /// Requests SC2's own pathing distance for every initial worker/mineral pair.
    /// The result is used for the one-time global opening assignment; later
    /// assignments fall back to current geometric distance until queried.
    /// </summary>
    public RequestQuery PrepareInitialPathingQuery(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (_initialPathingPending)
        {
            if (observation.GameLoop <= _initialPathingQueryGameLoop + 22)
                return null;

            // A missing query response must not stall the economy. Reconcile
            // with straight-line distance after one second of game time.
            _initialPathingPending = false;
            _pendingPathingPairs.Clear();
        }

        if (_initialPathingQueryIssued || _nextGeneration != 0 || _assignments.Count != 0)
            return null;

        var workers = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                           unit.HasPosition && IsWorker(unit.UnitKind) &&
                           !_reservedWorkers.Contains(unit.Tag) &&
                           !observation.DeadUnitTags.Contains(unit.Tag) &&
                           (!unit.HasOrders ||
                            (unit.Orders.FirstOrDefault()?.TargetUnitTag is ulong targetTag &&
                             observation.Units.Any(candidate => candidate.Tag == targetTag &&
                                 candidate.Alliance == UnitAlliance.Neutral &&
                                 IsMineral(candidate.UnitKind)))))
            .OrderBy(unit => unit.Tag)
            .ToArray();
        var minerals = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible &&
                           unit.HasPosition && IsMineral(unit.UnitKind) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();
        var townHalls = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                           unit.HasPosition && IsTownHall(unit.UnitKind) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        if (workers.Length == 0 || minerals.Length == 0 || townHalls.Length == 0)
            return null;

        var query = new RequestQuery();
        _pendingPathingPairs.Clear();

        foreach (var worker in workers)
        foreach (var mineral in minerals)
        {
            query.Pathing.Add(new RequestQueryPathing
            {
                StartPos = new Point2D { X = worker.PositionX, Y = worker.PositionY },
                EndPos = new Point2D { X = mineral.PositionX, Y = mineral.PositionY }
            });
            _pendingPathingPairs.Add((worker.Tag, mineral.Tag));
        }

        foreach (var townHall in townHalls)
        foreach (var mineral in minerals)
        {
            query.Pathing.Add(new RequestQueryPathing
            {
                StartPos = new Point2D { X = townHall.PositionX, Y = townHall.PositionY },
                EndPos = new Point2D { X = mineral.PositionX, Y = mineral.PositionY }
            });
            _pendingPathingPairs.Add((townHall.Tag, mineral.Tag));
        }

        _initialPathingQueryIssued = true;
        _initialPathingPending = true;
        _initialPathingQueryGameLoop = observation.GameLoop;
        return query;
    }

    /// <summary>Stores the pathing distances returned for the pending initial query.</summary>
    public void OnInitialPathingQueryResult(ResponseQuery response)
    {
        if (!_initialPathingPending || response is null || response.Pathing.Count == 0)
            return;

        if (response.Pathing.Count != _pendingPathingPairs.Count)
        {
            _initialPathingPending = false;
            _pendingPathingPairs.Clear();
            return;
        }

        for (var index = 0; index < _pendingPathingPairs.Count; index++)
        {
            var distance = response.Pathing[index].Distance;
            if (float.IsFinite(distance) && distance > 0)
                _pathingDistances[_pendingPathingPairs[index]] = distance;
        }

        _initialPathingPending = false;
        _pendingPathingPairs.Clear();
    }

    public IReadOnlyDictionary<ulong, WorkerResourceAssignment> Reconcile(
        NormalizedObservation observation,
        int mineralSaturation = DefaultMineralSaturation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (mineralSaturation < 1)
            throw new ArgumentOutOfRangeException(nameof(mineralSaturation));

        var liveSelf = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                           unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag))
            .ToDictionary(unit => unit.Tag);

        var minerals = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible &&
                           unit.HasPosition && IsMineral(unit.UnitKind) &&
                           !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToDictionary(unit => unit.Tag);

        var townHalls = liveSelf.Values
            .Where(unit => IsTownHall(unit.UnitKind))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        // Explicit death events invalidate a reservation. If a worker's current
        // order still targets that exact dead assigned mineral, it is safe to
        // replace that stale gather order with a new reservation. Other active
        // orders remain owned by their issuing behavior and are never overridden.
        var workersWithStaleMineralOrders = new HashSet<ulong>();
        foreach (var deadTag in observation.DeadUnitTags)
            _reservedWorkers.Remove(deadTag);
        foreach (var reservedTag in _reservedWorkers.ToArray())
        {
            if (liveSelf.TryGetValue(reservedTag, out var reservedUnit) &&
                !IsWorker(reservedUnit.UnitKind))
                _reservedWorkers.Remove(reservedTag);
        }
        foreach (var reservedWorker in _reservedWorkers)
            _assignments.Remove(reservedWorker);

        foreach (var pair in _assignments.ToArray())
        {
            var assignment = pair.Value;
            if (observation.DeadUnitTags.Contains(assignment.WorkerTag))
            {
                _assignments.Remove(pair.Key);
                _reservedWorkers.Remove(pair.Key);
                continue;
            }

            if (observation.DeadUnitTags.Contains(assignment.MineralTag))
            {
                if (liveSelf.TryGetValue(assignment.WorkerTag, out var worker) &&
                    worker.Orders.Count > 0 &&
                    worker.Orders[0].TargetUnitTag == assignment.MineralTag)
                    workersWithStaleMineralOrders.Add(assignment.WorkerTag);

                _assignments.Remove(pair.Key);
                continue;
            }

            if (liveSelf.TryGetValue(assignment.WorkerTag, out var currentWorker) &&
                !IsWorker(currentWorker.UnitKind))
                _assignments.Remove(pair.Key);
        }

        if (townHalls.Length == 0 || minerals.Count == 0 || _initialPathingPending)
            return Snapshot();

        var occupancy = _assignments.Values
            .GroupBy(assignment => assignment.MineralTag)
            .ToDictionary(group => group.Key, group => group.Count());

        // At match start, existing gather orders are only hints. Solve the
        // complete first distribution together so a low unit tag cannot pin a
        // distant worker to a patch that another SCV can reach much sooner.
        if (_nextGeneration == 0 && _assignments.Count == 0)
        {
            var initialWorkers = liveSelf.Values
                .Where(unit => IsWorker(unit.UnitKind) &&
                               !_reservedWorkers.Contains(unit.Tag) &&
                               (!unit.HasOrders ||
                                workersWithStaleMineralOrders.Contains(unit.Tag) ||
                                (unit.Orders.FirstOrDefault()?.TargetUnitTag is ulong targetTag &&
                                 minerals.ContainsKey(targetTag))))
                .OrderBy(unit => unit.Tag)
                .ToArray();

            AllocateInitialWorkers(
                initialWorkers, minerals.Values.OrderBy(unit => unit.Tag).ToArray(),
                townHalls, mineralSaturation, observation.GameLoop, occupancy);
        }

        // Adopt gather orders only for workers that become eligible after the
        // initial global allocation; their already-stable assignments are kept.
        // Starting SCVs commonly arrive with active gather orders; ignoring them
        // leaves the planner blind to the very routes it is supposed to manage.
        foreach (var worker in liveSelf.Values
                     .Where(unit => IsWorker(unit.UnitKind) && !_reservedWorkers.Contains(unit.Tag) && !_assignments.ContainsKey(unit.Tag))
                     .OrderBy(unit => unit.Tag))
        {
            var targetTag = worker.Orders.FirstOrDefault()?.TargetUnitTag;
            if (targetTag is not ulong mineralTag ||
                !minerals.TryGetValue(mineralTag, out var mineral) ||
                occupancy.GetValueOrDefault(mineralTag) >= 1)
                continue;

            var townHall = townHalls
                .OrderBy(hall => DistanceSquared(worker, hall))
                .ThenBy(hall => hall.Tag)
                .First();

            var adopted = new WorkerResourceAssignment(
                worker.Tag,
                mineral.Tag,
                townHall.Tag,
                NextGeneration(),
                observation.GameLoop,
                worker.PositionX,
                worker.PositionY,
                mineral.PositionX,
                mineral.PositionY,
                townHall.PositionX,
                townHall.PositionY);

            _assignments.Add(worker.Tag, adopted);
            occupancy[mineral.Tag] = occupancy.GetValueOrDefault(mineral.Tag) + 1;
        }

        foreach (var worker in liveSelf.Values
                     .Where(unit => IsWorker(unit.UnitKind) &&
                                    !_reservedWorkers.Contains(unit.Tag) &&
                                    (!unit.HasOrders ||
                                     workersWithStaleMineralOrders.Contains(unit.Tag) ||
                                     (unit.Orders.FirstOrDefault()?.TargetUnitTag is ulong targetTag &&
                                      minerals.ContainsKey(targetTag))))
                     .OrderBy(unit => unit.Tag))
        {
            if (_assignments.ContainsKey(worker.Tag))
                continue;

            var candidate = minerals.Values
                .Where(mineral => occupancy.GetValueOrDefault(mineral.Tag) < mineralSaturation)
                .Select(mineral => new
                {
                    Mineral = mineral,
                    TownHall = townHalls
                        .OrderBy(hall => DistanceSquared(worker, hall))
                        .ThenBy(hall => hall.Tag)
                        .First(),
                    Distance = DistanceSquared(worker, mineral),
                    IsSupplementalWorker = occupancy.GetValueOrDefault(mineral.Tag) > 0
                })
                .OrderBy(item => occupancy.GetValueOrDefault(item.Mineral.Tag))
                // With a small two-patch base, keep the second worker on the
                // closest route. Larger mineral lines spread second workers
                // toward the farthest patches after every patch has one worker.
                .ThenBy(item => worker.HasOrders && minerals.Count > 1
                    ? -DistanceSquared(item.Mineral, item.TownHall)
                    : item.Distance)
                .ThenBy(item => item.Distance)
                .ThenBy(item => item.Mineral.Tag)
                .FirstOrDefault();

            if (candidate is null)
                continue;

            var assignment = new WorkerResourceAssignment(
                worker.Tag,
                candidate.Mineral.Tag,
                candidate.TownHall.Tag,
                NextGeneration(),
                observation.GameLoop,
                worker.PositionX,
                worker.PositionY,
                candidate.Mineral.PositionX,
                candidate.Mineral.PositionY,
                candidate.TownHall.PositionX,
                candidate.TownHall.PositionY)
            {
                IsSupplementalWorker = candidate.IsSupplementalWorker
            };

            _assignments.Add(worker.Tag, assignment);
            occupancy[candidate.Mineral.Tag] = occupancy.GetValueOrDefault(candidate.Mineral.Tag) + 1;
        }

        return Snapshot();
    }

    private void AllocateInitialWorkers(
        UnitObservationFacts[] workers,
        UnitObservationFacts[] minerals,
        UnitObservationFacts[] townHalls,
        int mineralSaturation,
        uint gameLoop,
        Dictionary<ulong, int> occupancy,
        bool isSupplementalWorker)
    {
        if (workers.Length == 0 || minerals.Length == 0 || townHalls.Length == 0)
            return;

        foreach (var townHall in townHalls)
        {
            var baseWorkers = workers
                .Where(worker => NearestTownHall(worker, townHalls).Tag == townHall.Tag)
                .OrderBy(worker => worker.Tag)
                .ToArray();
            var baseMinerals = minerals
                .Where(mineral => NearestTownHall(mineral, townHalls).Tag == townHall.Tag)
                .OrderBy(mineral => mineral.Tag)
                .ToArray();

            if (baseWorkers.Length == 0 || baseMinerals.Length == 0)
                continue;

            // Every mineral receives one slot first. Additional slots are
            // created from the farthest patch inward, one per patch per round.
            var slots = new List<UnitObservationFacts>(baseMinerals);
            var farthestFirst = baseMinerals
                .OrderByDescending(mineral => TownHallRouteDistance(townHall, mineral))
                .ThenBy(mineral => mineral.Tag)
                .ToArray();
            for (var round = 1;
                 round < mineralSaturation && slots.Count < baseWorkers.Length;
                 round++)
            {
                foreach (var mineral in farthestFirst)
                {
                    if (slots.Count >= baseWorkers.Length)
                        break;
                    slots.Add(mineral);
                }
            }

            if (baseWorkers.Length <= slots.Count)
            {
                // Rows are workers and columns are mineral slots. Every worker
                // gets its minimum-total-distance slot; excess patches stay open.
                var costs = new double[baseWorkers.Length, slots.Count];
                for (var workerIndex = 0; workerIndex < baseWorkers.Length; workerIndex++)
                for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                    costs[workerIndex, slotIndex] =
                        RouteDistance(baseWorkers[workerIndex], slots[slotIndex]);

                var matching = SolveMinimumCostAssignment(costs);
                for (var workerIndex = 0; workerIndex < matching.Length; workerIndex++)
                {
                    var slotIndex = matching[workerIndex];
                    if (slotIndex >= 0)
                        AddAssignment(baseWorkers[workerIndex], slots[slotIndex],
                            townHall, gameLoop, occupancy,
                            slotIndex >= baseMinerals.Length);
                }
            }
            else
            {
                // Rows are mineral slots and columns are workers. Every slot
                // receives its globally best worker; surplus workers remain free.
                var costs = new double[slots.Count, baseWorkers.Length];
                for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                for (var workerIndex = 0; workerIndex < baseWorkers.Length; workerIndex++)
                    costs[slotIndex, workerIndex] =
                        RouteDistance(baseWorkers[workerIndex], slots[slotIndex]);

                var matching = SolveMinimumCostAssignment(costs);
                for (var slotIndex = 0; slotIndex < matching.Length; slotIndex++)
                {
                    var workerIndex = matching[slotIndex];
                    if (workerIndex >= 0)
                        AddAssignment(baseWorkers[workerIndex], slots[slotIndex],
                            townHall, gameLoop, occupancy,
                            slotIndex >= baseMinerals.Length);
                }
            }
        }
    }

    private void AddAssignment(
        UnitObservationFacts worker,
        UnitObservationFacts mineral,
        UnitObservationFacts townHall,
        uint gameLoop,
        Dictionary<ulong, int> occupancy)
    {
        var assignment = new WorkerResourceAssignment(
            worker.Tag,
            mineral.Tag,
            townHall.Tag,
            NextGeneration(),
            gameLoop,
            worker.PositionX,
            worker.PositionY,
            mineral.PositionX,
            mineral.PositionY,
            townHall.PositionX,
            townHall.PositionY)
        {
            IsSupplementalWorker = isSupplementalWorker
        };
        _assignments[worker.Tag] = assignment;
        occupancy[mineral.Tag] = occupancy.GetValueOrDefault(mineral.Tag) + 1;
    }

    private static UnitObservationFacts NearestTownHall(
        UnitObservationFacts unit,
        UnitObservationFacts[] townHalls) =>
        townHalls
            .OrderBy(hall => DistanceSquared(unit, hall))
            .ThenBy(hall => hall.Tag)
            .First();

    private static int[] SolveMinimumCostAssignment(double[,] costs)
    {
        var rowCount = costs.GetLength(0);
        var columnCount = costs.GetLength(1);
        if (rowCount > columnCount)
            throw new ArgumentException("The assignment matrix must have no more rows than columns.", nameof(costs));

        var rowPotential = new double[rowCount + 1];
        var columnPotential = new double[columnCount + 1];
        var matchedRow = new int[columnCount + 1];
        var previousColumn = new int[columnCount + 1];

        for (var row = 1; row <= rowCount; row++)
        {
            matchedRow[0] = row;
            var currentColumn = 0;
            var minimumReducedCost = Enumerable.Repeat(double.PositiveInfinity, columnCount + 1).ToArray();
            var used = new bool[columnCount + 1];

            do
            {
                used[currentColumn] = true;
                var currentRow = matchedRow[currentColumn];
                var delta = double.PositiveInfinity;
                var nextColumn = 0;

                for (var column = 1; column <= columnCount; column++)
                {
                    if (used[column])
                        continue;

                    var reducedCost = costs[currentRow - 1, column - 1] -
                                      rowPotential[currentRow] - columnPotential[column];
                    if (reducedCost < minimumReducedCost[column])
                    {
                        minimumReducedCost[column] = reducedCost;
                        previousColumn[column] = currentColumn;
                    }

                    if (minimumReducedCost[column] < delta)
                    {
                        delta = minimumReducedCost[column];
                        nextColumn = column;
                    }
                }

                for (var column = 0; column <= columnCount; column++)
                {
                    if (used[column])
                    {
                        rowPotential[matchedRow[column]] += delta;
                        columnPotential[column] -= delta;
                    }
                    else
                    {
                        minimumReducedCost[column] -= delta;
                    }
                }

                currentColumn = nextColumn;
            }
            while (matchedRow[currentColumn] != 0);

            do
            {
                var priorColumn = previousColumn[currentColumn];
                matchedRow[currentColumn] = matchedRow[priorColumn];
                currentColumn = priorColumn;
            }
            while (currentColumn != 0);
        }

        var result = new int[rowCount];
        Array.Fill(result, -1);
        for (var column = 1; column <= columnCount; column++)
        {
            if (matchedRow[column] > 0)
                result[matchedRow[column] - 1] = column - 1;
        }

        return result;
    }

    private double RouteDistance(UnitObservationFacts worker, UnitObservationFacts mineral) =>
        _pathingDistances.TryGetValue((worker.Tag, mineral.Tag), out var distance)
            ? distance
            : Distance(worker, mineral);

    private double TownHallRouteDistance(UnitObservationFacts townHall, UnitObservationFacts mineral) =>
        _pathingDistances.TryGetValue((townHall.Tag, mineral.Tag), out var distance)
            ? distance
            : Distance(townHall, mineral);

    private static double Distance(UnitObservationFacts left, UnitObservationFacts right) =>
        Math.Sqrt(DistanceSquared(left, right));

    public bool TryGet(ulong workerTag, out WorkerResourceAssignment assignment) =>
        _assignments.TryGetValue(workerTag, out assignment);

    public bool Release(ulong workerTag) => _assignments.Remove(workerTag);

    /// <summary>Remove a worker's mineral route and exclude it from future allocations.</summary>
    public void Reserve(ulong workerTag)
    {
        if (workerTag == 0) throw new ArgumentOutOfRangeException(nameof(workerTag));
        _assignments.Remove(workerTag);
        _reservedWorkers.Add(workerTag);
    }

    public bool IsReserved(ulong workerTag) => _reservedWorkers.Contains(workerTag);

    public bool Unreserve(ulong workerTag) => _reservedWorkers.Remove(workerTag);

    /// <summary>Restore a previously reserved worker to its exact mineral route.</summary>
    public void Restore(WorkerResourceAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _reservedWorkers.Remove(assignment.WorkerTag);
        _assignments[assignment.WorkerTag] = assignment;
    }

    public void Clear()
    {
        _assignments.Clear();
        _reservedWorkers.Clear();
        _pathingDistances.Clear();
        _pendingPathingPairs.Clear();
        _initialPathingQueryIssued = false;
        _initialPathingPending = false;
        _initialPathingQueryGameLoop = 0;
        _nextGeneration = 0;
    }

    private uint NextGeneration()
    {
        unchecked { _nextGeneration++; }
        if (_nextGeneration == 0)
            _nextGeneration = 1;
        return _nextGeneration;
    }

    private IReadOnlyDictionary<ulong, WorkerResourceAssignment> Snapshot() =>
        new Dictionary<ulong, WorkerResourceAssignment>(_assignments);

    private static bool IsWorker(string kind) =>
        kind.Equals("SCV", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Probe", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Drone", StringComparison.OrdinalIgnoreCase);

    private static bool IsMineral(string kind) =>
        kind.Contains("MineralField", StringComparison.OrdinalIgnoreCase);

    private static bool IsTownHall(string kind) =>
        kind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Nexus", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Hatchery", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Lair", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Hive", StringComparison.OrdinalIgnoreCase);

    private static double DistanceSquared(UnitObservationFacts left, UnitObservationFacts right)
    {
        var dx = left.PositionX - right.PositionX;
        var dy = left.PositionY - right.PositionY;
        return (double)dx * dx + (double)dy * dy;
    }
}

/// <summary>
/// Persistent reservation plus the last observed geometry used by a worker route.
/// Generation changes identify reassignment; positions are refreshed only when
/// the owning planner establishes a new assignment.
/// </summary>
public sealed record WorkerResourceAssignment(
    ulong WorkerTag,
    ulong MineralTag,
    ulong TownHallTag,
    uint Generation,
    uint AssignedAtGameLoop,
    float WorkerX,
    float WorkerY,
    float MineralX,
    float MineralY,
    float TownHallX,
    float TownHallY)
{
    /// <summary>True when this worker occupies a second-or-later mineral slot and can be drawn from the flexible construction crew.</summary>
    public bool IsSupplementalWorker { get; init; }
}
