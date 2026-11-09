using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class CapabilityFsmExecutionTests
{
    [Fact]
    public void Marine_ComposesIndependentMovementAndAttackHandles_AndExecutesThemThroughFsmApi()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var emitted = new List<BehaviorCommand>();
        var context = runtime.Attach(7001, UnitExpertiseProfiles.Marine, emitted.Add);

        Assert.Contains(UnitBehaviorCatalog.MoveTowardsFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.MoveAwayFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.FollowFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.PatrolFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.FlankLeftFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.FlankRightFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.AttackTargetFsm, context.Capabilities);
        Assert.Contains(UnitBehaviorCatalog.CombatFsm, context.Capabilities);

        context.Apply(new UnitObservationFacts(
            7001, 1, "Marine", 1, UnitAlliance.Self,
            UnitVisibility.Visible,
            45, 45, 0, 0, 1, 10, 10, true, false, false,
            false, false, false, 1));

        context.FollowTargetTag = 8001;
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.FollowTarget, emitted);

        emitted.Clear();
        context.FollowTargetTag = null;
        context.AttackTargetTag = 8002;
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.HighDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.AttackTarget, emitted);

        runtime.RemoveAll();
    }

    [Fact]
    public void FlankCapability_RequiresTargetAndExpressesStrategicSideIntent()
    {
        UnitBehaviorCatalog.Register();
        var runtime = new UnitBehaviorRuntime();
        var emitted = new List<BehaviorCommand>();
        var context = runtime.Attach(7002, UnitExpertiseProfiles.Marine, emitted.Add);
        context.Apply(new UnitObservationFacts(
            7002, 1, "Marine", 1, UnitAlliance.Self,
            UnitVisibility.Visible,
            45, 45, 0, 0, 1, 10, 10, true, false, false,
            false, false, false, 1));

        context.FlankLeftRequested = true;
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        Assert.DoesNotContain(BehaviorCommand.FlankLeftOfTarget, emitted);

        context.FlankTargetTag = 9001;
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        runtime.UpdateGroup(UnitBehaviorCatalog.LowDpsProcessingGroup);
        Assert.Contains(BehaviorCommand.FlankLeftOfTarget, emitted);

        runtime.RemoveAll();
    }
}
