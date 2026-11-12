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
        FSM_API.Interaction.Update(UnitBehaviorCatalog.DataSyncProcessingGroup);

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
        FSM_API.Interaction.Update(UnitBehaviorCatalog.DataSyncProcessingGroup);
        Assert.Empty(runtime.DrainEvents());

        runtime.Reconcile(Observation(12, Depot(2001, "SupplyDepot", 1f)),
            jobs.Jobs, _ => false);
        FSM_API.Interaction.Update(UnitBehaviorCatalog.DataSyncProcessingGroup);

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
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);
        Assert.Equal("Researching", Assert.Single(runtime.Buildings).ActivityState);

        var production = barracks with
        {
            GameLoop = 21,
            Orders = new[] { new UnitOrderObservationFacts(502, null, null, null) }
        };
        runtime.Reconcile(Observation(21, production), Array.Empty<BuildJobContext>(),
            _ => false);
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);
        Assert.Equal("Producing", Assert.Single(runtime.Buildings).ActivityState);

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
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);
        Assert.Equal("Raised", Assert.Single(runtime.Buildings).AccessState);
        Assert.Empty(runtime.DrainEvents());

        Assert.True(runtime.RequestDepotPassage(4001));
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);
        Assert.Equal("Lowered", Assert.Single(runtime.Buildings).AccessState);
        var lower = Assert.Single(runtime.DrainEvents());
        Assert.Equal(BuildingEventKind.LowerSupplyDepot, lower.Kind);

        runtime.RequestDepotPassage(4001, false);
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);
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
