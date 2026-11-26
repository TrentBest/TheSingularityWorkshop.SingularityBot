using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class GoalActionPlanningTests
{
    [Fact]
    public void PlanAssignsScoutSectorsAndStutterStepsMarines()
    {
        UnitBehaviorCatalog.Register();
        var map = new SpatialMap(new MapBounds(0, 0, 90, 90), new MapPoint(10, 10), new MapPoint(80, 80));
        var runtime = new UnitBehaviorRuntime();
        var emitted = new List<BehaviorCommand>();
        for (ulong tag = 1; tag <= 4; tag++)
        {
            var unit = runtime.Attach(tag, UnitExpertiseProfiles.Marine, emitted.Add);
            unit.Apply(new UnitObservationFacts(tag, 1, "Marine", 1, UnitAlliance.Self,
                UnitVisibility.Visible, tag == 3 ? 10 : 45, 45, 0, 0, 1, 10, 10, true,
                false, false, false, false, false, 1));
        }

        Assert.Empty(GoalActionPlanner.Apply(new GoalActionPlanDefinition { ScoutCount = 2 }, runtime, map));
        var scouts = runtime.Contexts.Where(c => c.GoalRole == GoalActionRole.Scout).ToArray();
        Assert.Equal(2, scouts.Length);
        Assert.Equal(2, scouts.Select(c => c.AssignedSector).Distinct().Count());
        Assert.DoesNotContain(map.GetSector(map.Side.EnemyStart).Id, scouts.Select(c => c.AssignedSector.Value));
        Assert.Equal(GoalActionRole.Fallback, runtime.Contexts.Single(c => c.UnitTag == 3).GoalRole);
        Assert.All(runtime.Contexts, c => Assert.True(c.StutterStepEnabled));

        runtime.UpdateGroup(UnitBehaviorCatalog.StrategyProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.ScoutSector, emitted);

        var marine = runtime.Contexts.Single(c => c.UnitTag == 4);
        marine.AttackTargetTag = 8001;
        marine.WeaponCooldownRemaining = 3;
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.StutterStepMove, emitted);
        emitted.Clear();
        marine.WeaponCooldownRemaining = 0;
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.AttackTarget, emitted);
        runtime.RemoveAll();
    }
    [Fact]
    public void DetailedScoutIntentCarriesAssignedSectorAndWorldPosition()
    {
        UnitBehaviorCatalog.Register();
        var map = new SpatialMap(new MapBounds(0, 0, 90, 90), new MapPoint(10, 10), new MapPoint(80, 80));
        var runtime = new UnitBehaviorRuntime();
        var unit = runtime.Attach(101, UnitExpertiseProfiles.Marine, _ => { });
        unit.Apply(new UnitObservationFacts(101, 1, "Marine", 1, UnitAlliance.Self,
            UnitVisibility.Visible, 45, 45, 0, 0, 1, 10, 10, true,
            false, false, false, false, false, 1));
        Assert.Empty(GoalActionPlanner.Apply(new GoalActionPlanDefinition { ScoutCount = 1 }, runtime, map));
        runtime.UpdateGroup(UnitBehaviorCatalog.StrategyProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        var buffer = new BehaviorIntentBuffer();
        buffer.Add(unit.UnitTag, BehaviorCommand.ScoutSector);

        var intent = Assert.Single(buffer.DrainDetailed(runtime));
        Assert.Equal(BehaviorCommand.ScoutSector, intent.Command);
        Assert.Equal(unit.AssignedSector, intent.Sector);
        Assert.Equal(unit.SectorCenterX, intent.DestinationX);
        Assert.Equal(unit.SectorCenterY, intent.DestinationY);
        runtime.RemoveAll();
    }

}
