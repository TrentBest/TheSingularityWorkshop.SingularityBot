using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class UnitObservationMemoryStoreTests
{
    [Fact]
    public void Upsert_UsesFixedUnmanagedSlotsAndUpdatesExistingUnit()
    {
        using var store = new UnitObservationMemoryStore(capacity: 4, pageSize: 4096);
        var first = Facts(42, 100, 45, 10, 20);
        store.Upsert(first);

        Assert.Equal(1, store.Count);
        Assert.True(store.TryRead(42, out var initial));
        Assert.Equal((ulong)42, initial.UnitTag);
        Assert.Equal(100u, initial.GameLoop);
        Assert.Equal(45f, initial.Health);

        store.Upsert(first with { GameLoop = 101, Health = 38, PositionX = 12 });
        Assert.Equal(1, store.Count);
        Assert.True(store.TryRead(42, out var updated));
        Assert.Equal(101u, updated.GameLoop);
        Assert.Equal(38f, updated.Health);
        Assert.Equal(12f, updated.PositionX);
        Assert.True(store.AllocatedBytes >= 4096);
    }

    [Fact]
    public void RemoveAndClear_InvalidateHandlesAndReleaseEveryLiveSlot()
    {
        using var store = new UnitObservationMemoryStore(capacity: 4, pageSize: 4096);
        store.Upsert(Facts(1, 1, 45, 0, 0));
        store.Upsert(Facts(2, 1, 45, 1, 1));

        Assert.True(store.Remove(1));
        Assert.False(store.TryRead(1, out _));
        Assert.Equal(1, store.Count);

        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.False(store.TryRead(2, out _));
    }

    private static UnitObservationFacts Facts(ulong tag, uint loop, float health, float x, float y) =>
        new(tag, 7, "SCV", 1, UnitAlliance.Self, UnitVisibility.Visible,
            health, 45, 0, 0, 1, x, y, true, false, false,
            false, false, false, loop);
}
