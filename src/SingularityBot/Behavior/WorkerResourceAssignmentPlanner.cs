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

    public int Count => _assignments.Count;

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

        if (townHalls.Length == 0 || minerals.Count == 0)
            return Snapshot();

        var occupancy = _assignments.Values
            .GroupBy(assignment => assignment.MineralTag)
            .ToDictionary(group => group.Key, group => group.Count());

        // Adopt the game's initial mineral orders before assigning idle workers.
        // Starting SCVs commonly arrive with active gather orders; ignoring them
        // leaves the planner blind to the very routes it is supposed to manage.
        foreach (var worker in liveSelf.Values
                     .Where(unit => IsWorker(unit.UnitKind) && !_reservedWorkers.Contains(unit.Tag) && !_assignments.ContainsKey(unit.Tag))
                     .OrderBy(unit => unit.Tag))
        {
            var targetTag = worker.Orders.FirstOrDefault()?.TargetUnitTag;
            if (targetTag is not ulong mineralTag ||
                !minerals.TryGetValue(mineralTag, out var mineral) ||
                occupancy.GetValueOrDefault(mineralTag) >= mineralSaturation)
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
                                     (unit.Orders[0].TargetUnitTag is ulong targetTag &&
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
                    Distance = DistanceSquared(worker, mineral)
                })
                .OrderBy(item => occupancy.GetValueOrDefault(item.Mineral.Tag))
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
                candidate.TownHall.PositionY);

            _assignments.Add(worker.Tag, assignment);
            occupancy[candidate.Mineral.Tag] = occupancy.GetValueOrDefault(candidate.Mineral.Tag) + 1;
        }

        return Snapshot();
    }

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
    float TownHallY);
