namespace SingularityBot.Behavior;

/// <summary>
/// Authoritative desired production quantities, keyed by SC2's match-resolved
/// UnitTypeId. This is strategic intent only; it never emits an SC2 action.
/// </summary>
public sealed class ProductionDemandLedger
{
    public const int MaximumUnitTypes = 512;
    public const int MaximumDesiredCount = 10000;
    public const int MaximumAppliedRequests = 4096;

    private readonly object _gate = new();
    private readonly Dictionary<uint, ProductionDemand> _demands = new();
    private readonly HashSet<string> _appliedRequests = new(StringComparer.Ordinal);

    public bool Set(Sc2UnitFacts unit, int desiredCount, int priority = 50)
    {
        if (!IsValid(unit, desiredCount, priority))
            return false;

        lock (_gate)
            return SetLocked(unit, desiredCount, priority);
    }

    /// <summary>Adds a signed delta to the desired total. Negative deltas lower the target.</summary>
    public bool Add(Sc2UnitFacts unit, int delta, int priority = 50)
    {
        if (!IsValidUnit(unit) || priority is < 0 or > 100)
            return false;

        lock (_gate)
        {
            var current = _demands.TryGetValue(unit.UnitId, out var existing)
                ? existing.DesiredCount
                : 0;
            var next = (long)current + delta;
            if (next is < 0 or > MaximumDesiredCount)
                return false;
            return SetLocked(unit, (int)next, priority);
        }
    }

    /// <summary>
    /// Applies an additive request at most once. Event/workflow correlation IDs
    /// should be used as requestId so replay cannot inflate the desired quantity.
    /// </summary>
    public bool AddOnce(string requestId, Sc2UnitFacts unit, int delta, int priority = 50)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 192 ||
            !IsValidUnit(unit) || priority is < 0 or > 100)
            return false;

        lock (_gate)
        {
            if (_appliedRequests.Contains(requestId) ||
                _appliedRequests.Count >= MaximumAppliedRequests)
                return false;

            var current = _demands.TryGetValue(unit.UnitId, out var existing)
                ? existing.DesiredCount
                : 0;
            var next = (long)current + delta;
            if (next is < 0 or > MaximumDesiredCount ||
                !SetLocked(unit, (int)next, priority))
                return false;

            _appliedRequests.Add(requestId);
            return true;
        }
    }

    public bool TryGet(uint unitTypeId, out ProductionDemand demand)
    {
        lock (_gate)
            return _demands.TryGetValue(unitTypeId, out demand);
    }

    public IReadOnlyDictionary<uint, ProductionDemand> Snapshot()
    {
        lock (_gate)
            return new System.Collections.ObjectModel.ReadOnlyDictionary<uint, ProductionDemand>(
                new Dictionary<uint, ProductionDemand>(_demands));
    }

    private bool SetLocked(Sc2UnitFacts unit, int desiredCount, int priority)
    {
        if (_demands.TryGetValue(unit.UnitId, out var existing))
        {
            if (!string.Equals(existing.UnitTypeName, unit.Name, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        else if (_demands.Count >= MaximumUnitTypes)
        {
            return false;
        }

        _demands[unit.UnitId] = new ProductionDemand(
            unit.UnitId, unit.Name, desiredCount, priority);
        return true;
    }

    private static bool IsValid(Sc2UnitFacts unit, int desiredCount, int priority) =>
        IsValidUnit(unit) && desiredCount is >= 0 and <= MaximumDesiredCount &&
        priority is >= 0 and <= 100;

    private static bool IsValidUnit(Sc2UnitFacts unit) =>
        unit is not null && unit.UnitId != 0 && !string.IsNullOrWhiteSpace(unit.Name);
}

/// <summary>A desired total for one authoritative SC2 unit type.</summary>
public sealed record ProductionDemand(
    uint UnitTypeId,
    string UnitTypeName,
    int DesiredCount,
    int Priority);
