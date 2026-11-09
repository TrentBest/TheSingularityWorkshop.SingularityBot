using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class GoalActionPlanningTests
{
    [Fact]
    public void PlanAssignsDistinctScoutSectorsAndLeavesOtherMarinesContaining()
    {
        UnitBehaviorCatalog.Register();
        var map = new SpatialMap(new MapBounds(0, 0, 90, 90), new MapPoint(10, 10), new MapPoint(80, 80));
        var runtime = new UnitBehaviorRuntime();
        var emitted = new List<BehaviorCommand>();
        for (ulong tag = 1; tag <= 4; tag++)
        {
            var unit = runtime.Attach(tag, UnitExpertiseProfiles.Marine, emitted.Add);
            unit.Apply(new UnitObservationFacts(tag, 1, "Marine", 1, UnitAlliance.Self,
                UnitVisibility.Visible, 45, 45, 0, 0, 1, 10, 10, true,
                false, false, false, false, false, 1));
        }
        Assert.Empty(GoalActionPlanner.Apply(new GoalActionPlanDefinition { ScoutCount = 2 }, runtime, map));
        var scouts = runtime.Contexts.Where(c => c.GoalRole == GoalActionRole.Scout).ToArray();
        Assert.Equal(2, scouts.Length);
        Assert.Equal(2, scouts.Select(c => c.AssignedSector).Distinct().Count());
        Assert.DoesNotContain(map.GetSector(map.Side.EnemyStart).Id, scouts.Select(c => c.AssignedSector.Value));
        Assert.Equal(GoalActionRole.Contain, runtime.Contexts.Single(c => c.UnitTag == 3).GoalRole);
        Assert.All(runtime.Contexts, c => Assert.True(c.StutterStepEnabled));
        runtime.UpdateGroup(UnitBehaviorCatalog.StrategyProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.ScoutSector, emitted);
        runtime.RemoveAll();
    }

    [Fact]
    public void StutterStepMovesOnCooldownAndAttacksWhenReady()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var emitted = new List<BehaviorCommand>();
        var unit = runtime.Attach(9001, UnitExpertiseProfiles.Marine, emitted.Add);
        unit.Apply(new UnitObservationFacts(9001, 1, "Marine", 1, UnitAlliance.Self,
            UnitVisibility.Visible, 45, 45, 0, 0, 1, 10, 10, true,
            false, false, false, false, false, 1));
        unit.StutterStepEnabled = true;
        unit.AttackTargetTag = 8001;
        unit.WeaponCooldownRemaining = 3;
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.StutterStepMove, emitted);
        emitted.Clear();
        unit.WeaponCooldownRemaining = 0;
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.AttackTarget, emitted);
        runtime.RemoveAll();
    }
}
