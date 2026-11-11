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
            BuffIds = { 11 },
            Orders = { new UnitOrder { AbilityId = 3666, TargetUnitTag = 1234 } }
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
        Assert.Equal(
            normalized.Units.Select(unit => unit with { Orders = Array.Empty<UnitOrderObservationFacts>() }),
            replay.Units.Select(unit => unit with { Orders = Array.Empty<UnitOrderObservationFacts>() }));
        Assert.True(normalized.Units[1].HasCargo);
        Assert.Single(normalized.Units[1].Orders);
        Assert.Equal((uint)3666, normalized.Units[1].Orders[0].AbilityId);
        Assert.Equal((ulong)1234, normalized.Units[1].Orders[0].TargetUnitTag);
        Assert.Contains((ulong)77, normalized.DeadUnitTags);
    }

    [Fact]
    public void Normalizer_PreservesUnitEnergyForOrbitalCommandDecisions()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 42,
            UnitType = 132,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            Energy = 75
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation { RawData = raw }
        };

        var normalized = Sc2ObservationNormalizer.Normalize(
            response, 1, new Dictionary<uint, string> { [132] = "OrbitalCommand" },
            new Dictionary<uint, string>());

        Assert.Equal(75f, Assert.Single(normalized.Units).Energy);
    }

    [Fact]
    public void Runtime_ReplacesExpertiseContextWhenUnitMorphsToDifferentProfile()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var scv = new UnitObservationFacts(
            88, 7, "SCV", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 10, 20, true, false, false,
            false, false, false, 20);
        var marine = scv with
        {
            UnitTypeId = 8,
            UnitKind = "Marine",
            Health = 45,
            HealthMax = 45,
            GameLoop = 21
        };

        runtime.Reconcile(
            new NormalizedObservation(20, 1, new[] { scv }, new HashSet<ulong>()),
            static (_, _) => { });
        Assert.True(runtime.TryGetContext(88, out var workerContext));
        Assert.True(workerContext.IsWorker);
        Assert.True(runtime.TryGetHandle(88, out var workerHandle));

        runtime.Reconcile(
            new NormalizedObservation(21, 1, new[] { marine }, new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(88, out var combatContext));
        Assert.NotSame(workerContext, combatContext);
        Assert.False(combatContext.IsWorker);
        Assert.Equal("Marine", combatContext.UnitKind);
        Assert.True(combatContext.IsValid);
        Assert.True(runtime.TryGetHandle(88, out var combatHandle));
        Assert.NotEqual(workerHandle, combatHandle);
        Assert.False(workerContext.IsValid);

        runtime.RemoveAll();
    }

    [Fact]
    public void Runtime_CreatesExistenceFsmForEnemyAndRetainsItThroughFogUntilExplicitDeath()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var enemy = new UnitObservationFacts(
            900, 8, "Marine", 2, UnitAlliance.Enemy, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 30, 40, true, false, false,
            false, false, false, 50);

        runtime.Reconcile(
            new NormalizedObservation(50, 1, new[] { enemy }, new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(900, out var context));
        Assert.True(context.IsValid);
        Assert.Equal(UnitAlliance.Enemy, context.Alliance);
        Assert.True(context.IsCurrentlyVisible);
        Assert.Contains(UnitBehaviorCatalog.EntityExistenceFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.CombatFsm, context.Capabilities);

        runtime.Update();
        runtime.Reconcile(
            new NormalizedObservation(51, 1, Array.Empty<UnitObservationFacts>(), new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(900, out var remembered));
        Assert.Same(context, remembered);
        Assert.True(remembered.IsValid);
        Assert.False(remembered.IsCurrentlyVisible);
        Assert.Equal(50u, remembered.LastSeenGameLoop);
        Assert.Equal(30f, remembered.PositionX);

        runtime.Update();
        runtime.Reconcile(
            new NormalizedObservation(52, 1, Array.Empty<UnitObservationFacts>(), new HashSet<ulong> { 900 }),
            static (_, _) => { });

        Assert.False(runtime.TryGetContext(900, out _));
        Assert.False(context.IsValid);
        Assert.Equal(0, runtime.Count);
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

        var firstFrame = new BehaviorIntentBuffer();
        runtime.Reconcile(
            new NormalizedObservation(10, 1, new[] { visible }, new HashSet<ulong>()),
            firstFrame.Add);

        Assert.True(runtime.TryGetContext(44, out var refreshed));
        Assert.Same(initial, refreshed);
        Assert.Equal((uint)10, refreshed.LastObservedGameLoop);
        Assert.True(refreshed.HasCargo);
        Assert.True(refreshed.IsValid);
        Assert.True(refreshed.IsCurrentlyVisible);

        runtime.Update();
        var firstFrameCommands = firstFrame.Drain();
        Assert.NotEmpty(firstFrameCommands);
        Assert.Contains(firstFrameCommands, command => command.Key == 44);

        var changed = visible with { Health = 30, HasCargo = false, GameLoop = 11 };
        var unsupported = visible with { Tag = 45, UnitKind = "UncataloguedUnit", GameLoop = 11 };
        var secondFrame = new BehaviorIntentBuffer();
        runtime.Reconcile(
            new NormalizedObservation(11, 1, new[] { changed, unsupported }, new HashSet<ulong>()),
            secondFrame.Add);

        Assert.True(runtime.TryGetContext(44, out var changedContext));
        Assert.Same(initial, changedContext);
        Assert.Equal(30, changedContext.Health);
        Assert.False(changedContext.HasCargo);
        Assert.True(runtime.TryGetContext(45, out var unknownContext));
        Assert.Equal("UncataloguedUnit", unknownContext.UnitKind);
        Assert.Contains(UnitBehaviorCatalog.EntityExistenceFsm, unknownContext.Capabilities);

        // The same FSM context survives the frame boundary. An emission from
        // that persistent context must route to this observation's buffer,
        // never to the drained buffer captured during its original attachment.
        changedContext.Emit(BehaviorCommand.AssignWorkerToMinerals);
        var secondFrameCommands = secondFrame.Drain();
        Assert.Contains(secondFrameCommands, command => command.Key == 44);
        Assert.Empty(emitted);
        runtime.Update();

        runtime.Reconcile(
            new NormalizedObservation(12, 1, Array.Empty<UnitObservationFacts>(), new HashSet<ulong>()),
            static (_, _) => { });

        Assert.True(runtime.TryGetContext(44, out var retained));
        Assert.Same(initial, retained);
        Assert.True(retained.IsValid);
        Assert.False(retained.IsCurrentlyVisible);
        Assert.True(runtime.TryGetHandle(44, out var suspendedHandle));
        Assert.Same(originalHandle, suspendedHandle);

        runtime.Update();

        var visibleAgain = changed with { HasCargo = false, HasOrders = false, GameLoop = 13 };
        var reactivatedFrame = new BehaviorIntentBuffer();
        runtime.Reconcile(
            new NormalizedObservation(13, 1, new[] { visibleAgain }, new HashSet<ulong>()),
            reactivatedFrame.Add);
        Assert.True(runtime.TryGetContext(44, out var reactivated));
        Assert.Same(initial, reactivated);
        Assert.True(reactivated.IsValid);
        Assert.True(reactivated.IsCurrentlyVisible);
        Assert.True(runtime.TryGetHandle(44, out var reactivatedHandle));
        Assert.Same(originalHandle, reactivatedHandle);
        runtime.Update();
        Assert.Empty(reactivatedFrame.Drain());
        Assert.Empty(emitted); // The context reactivated without replacing its handle or fallback.

        runtime.Reconcile(
            new NormalizedObservation(14, 1, Array.Empty<UnitObservationFacts>(), new HashSet<ulong> { 44 }),
            static (_, _) => { });

        Assert.False(runtime.TryGetContext(44, out _));
        Assert.False(initial.IsValid);
        Assert.False(runtime.TryGetHandle(44, out _));
        Assert.True(runtime.TryGetContext(45, out _)); // The separately observed unsupported unit remains alive.
        Assert.Equal(1, runtime.Count);
    }
    [Fact]
    public void Runtime_BindsWorkerMiningJobToPersistentMineralContextEvenWithoutHealthMax()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var worker = new UnitObservationFacts(
            10, 7, "SCV", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 2, 0, true, false, false,
            false, false, false, 100);
        var townHall = new UnitObservationFacts(
            1, 18, "CommandCenter", 1, UnitAlliance.Self, UnitVisibility.Visible,
            1500, 1500, 0, 0, 1, 0, 0, true, false, false,
            false, false, false, 100);
        var mineral = new UnitObservationFacts(
            20, 341, "MineralField750", 0, UnitAlliance.Neutral, UnitVisibility.Visible,
            0, 0, 0, 0, 1, 5, 0, true, false, false,
            false, false, false, 100);
        var observation = new NormalizedObservation(
            100, 1, new[] { worker, townHall, mineral }, new HashSet<ulong>());
        var planner = new WorkerResourceAssignmentPlanner();
        var assignments = planner.Reconcile(observation);

        runtime.Reconcile(observation, static (_, _) => { });
        runtime.ApplyWorkerAssignments(assignments, 100);

        Assert.True(runtime.TryGetContext(20, out var mineralContext));
        Assert.True(mineralContext.IsMineralResource);
        Assert.True(runtime.TryGetContext(10, out var workerContext));
        Assert.Same(mineralContext, workerContext.AssignedMineralContext);
        Assert.Equal(20UL, workerContext.AssignedMineralTag);

        runtime.RemoveAll();
    }

}
