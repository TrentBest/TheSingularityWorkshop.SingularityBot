using SingularityBot.Behavior;
using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BuildingBehaviorRuntimeTests
{
    [Fact]
    public void EveryBuildingGetsStableMatchLocalIdentityAndLifecycleFsm()
    {
        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        var runtime = new BuildingBehaviorRuntime();
        var first = Depot(1001, "SupplyDepot", 0.5f);
        var second = Depot(1002, "SupplyDepot", 1f);

        runtime.Reconcile(Observation(10, first, second), Array.Empty<BuildJobContext>(), _ => false);
        runtime.UpdateLifecycle();
        runtime.UpdateLifecycle();
        runtime.UpdateLifecycle();

        Assert.Equal("SupplyDepot 1", runtime.Buildings.Single(item => item.UnitTag == 1001).BuildingId);
        Assert.Equal("SupplyDepot 2", runtime.Buildings.Single(item => item.UnitTag == 1002).BuildingId);
        Assert.Equal("UnderConstruction", runtime.Buildings.Single(item => item.UnitTag == 1001).LifecycleState);
        Assert.Equal("Operational", runtime.Buildings.Single(item => item.UnitTag == 1002).LifecycleState);

        var events = runtime.DrainEvents();
        var completed = Assert.Single(events);
        Assert.Equal(BuildingEventKind.Operational, completed.Kind);
        Assert.Equal(1002UL, completed.BuildingTag);

        runtime.Clear();
    }

    [Fact]
    public void BuildingLifecycleReturnsItsBuilderOnlyWhenOperational()
    {
        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        var jobs = new BuildJobRuntime();
        var job = jobs.Create("SupplyDepot", 10, workerTag: 77);
        job.ObserveResult(2001, 0.5f, 11);
        var runtime = new BuildingBehaviorRuntime();

        runtime.Reconcile(Observation(11, Depot(2001, "SupplyDepot", 0.5f)),
            jobs.Jobs, _ => false);
        runtime.UpdateLifecycle();
        runtime.UpdateLifecycle();
        runtime.UpdateLifecycle();
        Assert.Empty(runtime.DrainEvents());

        runtime.Reconcile(Observation(12, Depot(2001, "SupplyDepot", 1f)),
            jobs.Jobs, _ => false);
        runtime.UpdateLifecycle();
        runtime.UpdateLifecycle();
        runtime.UpdateLifecycle();

        var completed = Assert.Single(runtime.DrainEvents());
        Assert.Equal(BuildingEventKind.Operational, completed.Kind);
        Assert.Equal(77UL, completed.BuilderWorkerTag);
        Assert.Equal("Operational", Assert.Single(runtime.Buildings).LifecycleState);

        runtime.Clear();
        jobs.Clear();
    }

    [Fact]
    public void BuildingActivityDistinguishesProductionFromResearch()
    {
        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        var runtime = new BuildingBehaviorRuntime();
        var barracks = Depot(3001, "Barracks", 1f) with
        {
            Orders = new[] { new UnitOrderObservationFacts(501, null, null, null) }
        };
        runtime.Reconcile(Observation(20, barracks), Array.Empty<BuildJobContext>(),
            id => id == 501);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Researching", Assert.Single(runtime.Buildings).ActivityState);

        var production = barracks with
        {
            GameLoop = 21,
            Orders = new[] { new UnitOrderObservationFacts(502, null, null, null) }
        };
        runtime.Reconcile(Observation(21, production), Array.Empty<BuildJobContext>(),
            _ => false);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Producing", Assert.Single(runtime.Buildings).ActivityState);

        runtime.Clear();
    }

    [Fact]
    public void TownHallOrbitalUpgradeIsAnFsmDecisionAndHonorsTheConstructionArea()
    {
        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        var runtime = new BuildingBehaviorRuntime();
        var commandCenter = Depot(5001, "CommandCenter", 1f);
        var barracks = Depot(5002, "Barracks", 1f);
        var depot = Depot(5003, "SupplyDepot", 1f);

        runtime.Reconcile(Observation(40, commandCenter, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("UpgradeReady", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);

        runtime.ConfigureDesignatedCommandCenterConstructionArea(10, 10, 3);
        runtime.Reconcile(Observation(41, commandCenter, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("WaitingForPrerequisites", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);
        Assert.Equal("StreamingCommandCenters", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallExpansionState);

        runtime.ClearDesignatedCommandCenterConstructionArea();
        var scvProducing = commandCenter with
        {
            GameLoop = 42,
            Orders = new[] { new UnitOrderObservationFacts(100, null, null, null) }
        };
        runtime.Reconcile(Observation(42, scvProducing, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("WaitingForPrerequisites", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);
        Assert.Equal("Waiting", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallExpansionState);

        runtime.Reconcile(Observation(43, commandCenter with { GameLoop = 43 }, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("UpgradeReady", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);

        // A second completed Barracks blocks the starting Command Center's
        // Orbital morph; the prerequisite is exactly one, not one-or-more.
        var secondBarracks = Depot(5004, "Barracks", 1f);
        runtime.Reconcile(Observation(431, commandCenter with { GameLoop = 431 }, barracks, secondBarracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("WaitingForPrerequisites", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);

        runtime.Reconcile(Observation(432, commandCenter with { GameLoop = 432 }, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("UpgradeReady", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);

        var upgradeOrder = commandCenter with
        {
            GameLoop = 44,
            Orders = new[] { new UnitOrderObservationFacts(5600, null, null, null) }
        };
        runtime.Reconcile(Observation(44, upgradeOrder, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Upgrading", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);

        var orbital = Depot(5001, "OrbitalCommand", 1f) with { GameLoop = 45 };
        runtime.Reconcile(Observation(45, orbital, barracks, depot),
            Array.Empty<BuildJobContext>(), _ => false, scvTrainAbilityId: 100, orbitalUpgradeAbilityId: 5600);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Orbital", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).TownHallUpgradeState);
        Assert.Equal("CommandCenter 1", Assert.Single(runtime.Buildings.Where(item => item.UnitTag == 5001)).BuildingId);

        runtime.Clear();
    }

    [Fact]
    public void OrbitalScanIsRequestedByItsFsmAndCompletesOnEnergyEvidence()
    {
        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        var runtime = new BuildingBehaviorRuntime();
        var orbital = Depot(6001, "OrbitalCommand", 1f) with { Energy = 100 };

        runtime.Reconcile(Observation(50, orbital), Array.Empty<BuildJobContext>(), _ => false);
        Assert.True(runtime.RequestOrbitalScan(6001, 15, 16));
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();

        var scan = Assert.Single(runtime.DrainEvents());
        Assert.Equal(BuildingEventKind.ScanRequested, scan.Kind);
        Assert.Equal(15f, scan.TargetX);
        Assert.Equal(16f, scan.TargetY);
        Assert.Contains(6001UL, runtime.ScanReservedOrbitalTags);

        Assert.True(runtime.MarkScanSubmitted(6001, 50));
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Empty(runtime.DrainEvents());

        var afterScan = orbital with { GameLoop = 51, Energy = 50 };
        runtime.Reconcile(Observation(51, afterScan), Array.Empty<BuildJobContext>(), _ => false);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();

        Assert.DoesNotContain(6001UL, runtime.ScanReservedOrbitalTags);
        Assert.Equal("Idle", Assert.Single(runtime.Buildings).OrbitalScanState);
        runtime.Clear();
    }

    [Fact]
    public void ExplicitDepotPassageOverridesEnemyThreatAndLowersTheDepot()
    {
        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        var runtime = new BuildingBehaviorRuntime();
        var raisedDepot = Depot(4001, "SupplyDepot", 1f);
        var enemy = new UnitObservationFacts(
            9001, 1, "Marine", 2, UnitAlliance.Enemy, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 14, 10, true, false, false,
            false, false, false, 30);
        runtime.Reconcile(Observation(30, raisedDepot, enemy), Array.Empty<BuildJobContext>(), _ => false);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Raised", Assert.Single(runtime.Buildings).AccessState);
        Assert.Empty(runtime.DrainEvents());

        Assert.True(runtime.RequestDepotPassage(4001));
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Lowered", Assert.Single(runtime.Buildings).AccessState);
        Assert.Equal("SupplyDepot 1", Assert.Single(runtime.Buildings).BuildingId);
        var lower = Assert.Single(runtime.DrainEvents());
        Assert.Equal(BuildingEventKind.LowerSupplyDepot, lower.Kind);

        // The next SC2 observation confirms that the lower command took effect.
        runtime.Reconcile(Observation(31, Depot(4001, "SupplyDepotLowered", 1f), enemy),
            Array.Empty<BuildJobContext>(), _ => false);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();

        runtime.RequestDepotPassage(4001, false);
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        runtime.UpdateActivityAndAccess();
        Assert.Equal("Raised", Assert.Single(runtime.Buildings).AccessState);
        var raise = Assert.Single(runtime.DrainEvents());
        Assert.Equal(BuildingEventKind.RaiseSupplyDepot, raise.Kind);

        runtime.Clear();
    }

    private static NormalizedObservation Observation(uint loop, params UnitObservationFacts[] units) =>
        new(loop, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Depot(ulong tag, string kind, float progress) =>
        new(tag, 19, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            400, 400, 0, 0, progress, 10, 10, true, false, false,
            false, false, false, 10);
}
