using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ObservationReconciliationTests
{
    [Fact]
    public void Normalizer_OrdersUnitsAndPreservesVisibilityAndDeathEvidence()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 90,
            UnitType = 7,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            Health = 45,
            HealthMax = 45,
            BuffIds = { 11 }
        });
        raw.Units.Add(new Unit
        {
            Tag = 12,
            UnitType = 8,
            Owner = 2,
            Alliance = Alliance.Enemy,
            DisplayType = DisplayType.Snapshot
        });
        raw.Event = new SC2APIProtocol.Event();
        raw.Event.DeadUnits.Add(77);

        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation
            {
                GameLoop = 123,
                RawData = raw
            }
        };

        var normalized = Sc2ObservationNormalizer.Normalize(
            response,
            1,
            new Dictionary<uint, string> { [7] = "SCV", [8] = "Marine" },
            new Dictionary<uint, string> { [11] = "CarryingMinerals" });

        Assert.Equal((uint)123, normalized.GameLoop);
        Assert.Equal((uint)1, normalized.PlayerId);
        Assert.Equal(new ulong[] { 12, 90 }, normalized.Units.Select(unit => unit.Tag));
        Assert.Equal(UnitVisibility.Snapshot, normalized.Units[0].Visibility);
        Assert.False(normalized.Units[0].IsLiveVisible);
        Assert.Equal("SCV", normalized.Units[1].UnitKind);
        var replay = Sc2ObservationNormalizer.Normalize(response, 1,
            new Dictionary<uint, string> { [7] = "SCV", [8] = "Marine" },
            new Dictionary<uint, string> { [11] = "CarryingMinerals" });
        Assert.Equal(normalized.Units, replay.Units);
        Assert.True(normalized.Units[1].HasCargo);
        Assert.Contains((ulong)77, normalized.DeadUnitTags);
    }

    [Fact]
    public void Runtime_UpdatesStableContextAndDoesNotRetireOnAbsenceButDoesOnDeath()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var emitted = new List<BehaviorCommand>();
        var initial = runtime.Attach(44, UnitExpertiseProfiles.SCV, emitted.Add);
        Assert.True(runtime.TryGetHandle(44, out var originalHandle));

        var visible = new UnitObservationFacts(
            44, 7, "SCV", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 10, 20, true, true, true,
            false, false, false, 10);

        runtime.Reconcile(
            new NormalizedObservation(10, 1, new[] { visible }, new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(44, out var refreshed));
        Assert.Same(initial, refreshed);
        Assert.Equal((uint)10, refreshed.LastObservedGameLoop);
        Assert.True(refreshed.HasCargo);
        Assert.True(refreshed.IsValid);
        Assert.True(refreshed.IsCurrentlyVisible);

        runtime.Update();
        var commandsWhileVisible = emitted.Count;
        Assert.True(commandsWhileVisible > 0);

        var changed = visible with { Health = 30, HasCargo = false, GameLoop = 11 };
        var unsupported = visible with { Tag = 45, UnitKind = "UncataloguedUnit", GameLoop = 11 };
        runtime.Reconcile(
            new NormalizedObservation(11, 1, new[] { changed, unsupported }, new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(44, out var changedContext));
        Assert.Same(initial, changedContext);
        Assert.Equal(30, changedContext.Health);
        Assert.False(changedContext.HasCargo);
        Assert.False(runtime.TryGetContext(45, out _));

        runtime.Reconcile(
            new NormalizedObservation(12, 1, Array.Empty<UnitObservationFacts>(), new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(44, out var retained));
        Assert.Same(initial, retained);
        Assert.False(retained.IsValid);
        Assert.False(retained.IsCurrentlyVisible);
        Assert.True(runtime.TryGetHandle(44, out var suspendedHandle));
        Assert.Same(originalHandle, suspendedHandle);

        runtime.Update();
        Assert.Equal(commandsWhileVisible, emitted.Count);

        var visibleAgain = changed with { HasCargo = true, GameLoop = 13 };
        runtime.Reconcile(
            new NormalizedObservation(13, 1, new[] { visibleAgain }, new HashSet<ulong>()),
            static (_, _) => { });
        Assert.True(runtime.TryGetContext(44, out var reactivated));
        Assert.Same(initial, reactivated);
        Assert.True(reactivated.IsValid);
        Assert.True(reactivated.IsCurrentlyVisible);
        Assert.True(runtime.TryGetHandle(44, out var reactivatedHandle));
        Assert.Same(originalHandle, reactivatedHandle);
        runtime.Update();
        Assert.True(emitted.Count > commandsWhileVisible);

        runtime.Reconcile(
            new NormalizedObservation(14, 1, Array.Empty<UnitObservationFacts>(), new HashSet<ulong> { 44 }),
            static (_, _) => { });

        Assert.False(runtime.TryGetContext(44, out _));
        Assert.False(initial.IsValid);
        Assert.False(runtime.TryGetHandle(44, out _));
        Assert.Equal(0, runtime.Count);
    }
}
