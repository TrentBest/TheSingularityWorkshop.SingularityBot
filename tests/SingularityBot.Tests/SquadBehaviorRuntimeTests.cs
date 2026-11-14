using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class SquadBehaviorRuntimeTests
{
    [Fact]
    public void SquadLifecycleAdvancesThroughFormingReadyExecutingAndRegrouping()
    {
        var runtime = new SquadBehaviorRuntime();
        var squad = runtime.Create("squad.alpha", "assault", "objective.main");

        Assert.Equal("Forming", squad.LifecycleState);
        Assert.True(runtime.TryAddMember(squad.SquadId, 101));
        runtime.Update();
        Assert.Equal("Ready", squad.LifecycleState);

        Assert.True(runtime.SetExecuting(squad.SquadId, true));
        runtime.Update();
        Assert.Equal("Executing", squad.LifecycleState);

        Assert.True(runtime.SetRegrouping(squad.SquadId, true));
        runtime.Update();
        Assert.Equal("Regrouping", squad.LifecycleState);

        Assert.True(runtime.SetRegrouping(squad.SquadId, false));
        runtime.Update();
        Assert.Equal("Executing", squad.LifecycleState);

        runtime.Clear();
    }

    [Fact]
    public void UnitCanBelongToOnlyOneSquadInTheRuntime()
    {
        var runtime = new SquadBehaviorRuntime();
        runtime.Create("squad.one", "defense", "base.main");
        runtime.Create("squad.two", "assault", "enemy.third");

        Assert.True(runtime.TryAddMember("squad.one", 42));
        Assert.False(runtime.TryAddMember("squad.two", 42));
        Assert.True(runtime.TryGetMembership(42, out var owner));
        Assert.Equal("squad.one", owner);

        runtime.Clear();
    }

    [Fact]
    public void DeadUnitEvidenceRemovesMembershipButTemporaryAbsenceDoesNot()
    {
        var runtime = new SquadBehaviorRuntime();
        var squad = runtime.Create("squad.one", "scout", "map.north");
        runtime.TryAddMember(squad.SquadId, 77);

        // No implicit removal API is called for temporary fog-of-war absence.
        Assert.Contains((ulong)77, squad.UnitTags);
        runtime.ReconcileDeadUnits(new[] { 77UL });

        Assert.Empty(squad.UnitTags);
        Assert.False(runtime.TryGetMembership(77, out _));
        runtime.Clear();
    }

    [Fact]
    public void DisbandInvalidatesContextAndReleasesMembershipAndHandle()
    {
        var runtime = new SquadBehaviorRuntime();
        var squad = runtime.Create("squad.one", "escort", "base.main");
        runtime.TryAddMember(squad.SquadId, 88);

        Assert.True(runtime.Disband(squad.SquadId));

        Assert.False(squad.IsValid);
        Assert.True(squad.IsDisbanding);
        Assert.False(runtime.TryGetMembership(88, out _));
        Assert.False(runtime.TryGetSquad("squad.one", out _));
        runtime.Clear();
    }

    [Fact]
    public void RejectsInvalidSquadDefinitionsAndDuplicateIds()
    {
        var runtime = new SquadBehaviorRuntime();
        Assert.Throws<ArgumentException>(() => runtime.Create("", "assault", "objective"));
        Assert.Throws<ArgumentException>(() => runtime.Create("squad.one", "", "objective"));
        Assert.Throws<ArgumentException>(() => runtime.Create("squad.one", "assault", ""));
        runtime.Create("squad.one", "assault", "objective");
        Assert.Throws<InvalidOperationException>(() => runtime.Create("squad.one", "assault", "objective"));
        runtime.Clear();
    }
}
