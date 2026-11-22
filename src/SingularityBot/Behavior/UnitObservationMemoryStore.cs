using TheSingularityWorkshop.DataWarehouse;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Compact unmanaged mirror of the latest normalized unit facts. High-volume
/// per-unit snapshots live in DataWarehouse shelves rather than one managed
/// object per observed field set.
/// </summary>
public struct UnitObservationMemoryRecord
{
    public ulong UnitTag;
    public uint UnitTypeId;
    public int OwnerId;
    public uint GameLoop;
    public uint Flags;
    public float Health;
    public float HealthMax;
    public float Shield;
    public float ShieldMax;
    public float BuildProgress;
    public float PositionX;
    public float PositionY;
    public float WeaponCooldownRemaining;
    public float Energy;
}

/// <summary>
/// Match-scoped DataWarehouse allocation for normalized unit state. The tag
/// index provides identity lookup; the actual hot record is fixed-size,
/// page-aligned unmanaged storage with generation-checked handles.
/// </summary>
public sealed class UnitObservationMemoryStore : IDisposable
{
    public const int DefaultCapacity = 32768;
    private const uint LiveVisible = 1u << 0;
    private const uint HasPosition = 1u << 1;
    private const uint HasCargo = 1u << 2;
    private const uint HasOrders = 1u << 3;
    private const uint IsFlying = 1u << 4;
    private const uint IsBurrowed = 1u << 5;
    private const uint IsHallucination = 1u << 6;
    private const uint IsPowered = 1u << 7;
    private const uint HasPowerEvidence = 1u << 8;

    private readonly DataWarehouse _warehouse;
    private readonly DataShelf<UnitObservationMemoryRecord> _shelf;
    private readonly Dictionary<ulong, DataHandle> _handles = new();

    public UnitObservationMemoryStore(int capacity = DefaultCapacity, int pageSize = 4096)
    {
        _warehouse = new DataWarehouse(pageSize);
        _shelf = _warehouse.CreateShelf<UnitObservationMemoryRecord>(capacity, pageSize);
    }

    public int Count => _handles.Count;
    public int Capacity => _shelf.Capacity;
    public long AllocatedBytes => _shelf.AllocatedBytes;

    public void Upsert(UnitObservationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Tag == 0)
            throw new ArgumentOutOfRangeException(nameof(facts), "Unit tags must be non-zero.");

        if (!_handles.TryGetValue(facts.Tag, out var handle))
        {
            if (!_shelf.TryAllocate(out handle))
                throw new InvalidOperationException($"Unit observation memory capacity ({Capacity}) was exhausted.");
            _handles.Add(facts.Tag, handle);
        }

        ref var record = ref _shelf.GetRef(handle);
        record.UnitTag = facts.Tag;
        record.UnitTypeId = facts.UnitTypeId;
        record.OwnerId = facts.OwnerId;
        record.GameLoop = facts.GameLoop;
        record.Flags =
            (facts.IsLiveVisible ? LiveVisible : 0) |
            (facts.HasPosition ? HasPosition : 0) |
            (facts.HasCargo ? HasCargo : 0) |
            (facts.HasOrders ? HasOrders : 0) |
            (facts.IsFlying ? IsFlying : 0) |
            (facts.IsBurrowed ? IsBurrowed : 0) |
            (facts.IsHallucination ? IsHallucination : 0) |
            (facts.IsPowered ? IsPowered : 0) |
            (facts.HasPowerEvidence ? HasPowerEvidence : 0);
        record.Health = facts.Health;
        record.HealthMax = facts.HealthMax;
        record.Shield = facts.Shield;
        record.ShieldMax = facts.ShieldMax;
        record.BuildProgress = facts.BuildProgress;
        record.PositionX = facts.PositionX;
        record.PositionY = facts.PositionY;
        record.WeaponCooldownRemaining = facts.WeaponCooldownRemaining;
        record.Energy = facts.Energy;
        _shelf.MarkClean(handle);
    }

    public bool TryRead(ulong unitTag, out UnitObservationMemoryRecord record)
    {
        if (_handles.TryGetValue(unitTag, out var handle) && _shelf.IsAlive(handle))
        {
            record = _shelf.Read(handle);
            return true;
        }
        record = default;
        return false;
    }

    public bool Remove(ulong unitTag)
    {
        if (!_handles.Remove(unitTag, out var handle))
            return false;
        return _shelf.TryFree(handle);
    }

    public void Clear()
    {
        foreach (var handle in _handles.Values)
            _shelf.TryFree(handle);
        _handles.Clear();
    }

    public void Dispose()
    {
        Clear();
        _warehouse.Dispose();
    }
}
