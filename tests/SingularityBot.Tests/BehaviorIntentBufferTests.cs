using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BehaviorIntentBufferTests
{
    [Fact]
    public void HigherPriorityCapabilityWinsRegardlessOfEmissionOrder()
    {
        var buffer = new BehaviorIntentBuffer();

        buffer.Add(10, BehaviorCommand.RepairAssignedTarget);
        buffer.Add(10, BehaviorCommand.AssignWorkerToMinerals);

        var intent = Assert.Single(buffer.Drain());
        Assert.Equal(10UL, intent.Key);
        Assert.Equal(BehaviorCommand.RepairAssignedTarget, intent.Value);
    }

    [Fact]
    public void SafetyIntentOverridesRepairAndConstruction()
    {
        var buffer = new BehaviorIntentBuffer();

        buffer.Add(10, BehaviorCommand.BuildRequestedStructure);
        buffer.Add(10, BehaviorCommand.RepairAssignedTarget);
        buffer.Add(10, BehaviorCommand.RetreatToSafety);

        Assert.Equal(BehaviorCommand.RetreatToSafety, Assert.Single(buffer.Drain()).Value);
    }

    [Fact]
    public void HigherPriorityWinsAndDrainIsTagOrdered()
    {
        var buffer = new BehaviorIntentBuffer();

        buffer.Add(20, BehaviorCommand.AttackVisibleThreat);
        buffer.Add(10, BehaviorCommand.ReturnCarriedResources);
        buffer.Add(20, BehaviorCommand.DefendWorkerLine);

        var intents = buffer.Drain();
        Assert.Equal(new ulong[] { 10, 20 }, intents.Select(intent => intent.Key));
        Assert.Equal(BehaviorCommand.DefendWorkerLine, intents[1].Value);
        Assert.Empty(buffer.Drain());
    }
}
