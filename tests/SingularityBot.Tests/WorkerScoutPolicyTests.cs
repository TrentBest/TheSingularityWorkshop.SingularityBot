using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class WorkerScoutPolicyTests
{
    [Fact]
    public void AdaptiveModeScoutsWhenEnemyStartIsUnknownAndMapHasMultipleStarts()
    {
        var decision = WorkerScoutPolicy.Evaluate(new WorkerScoutAssessment(
            WorkerScoutMode.WhenInformationIsMissing, 13, 12,
            OpponentStartKnown: false, ThreatInformationNeeded: false,
            MapHasMultipleStartLocations: true, ScoutAlreadyAssigned: false));

        Assert.Equal(WorkerScoutDecision.AssignScout, decision);
    }

    [Fact]
    public void AdaptiveModeDoesNotScoutWhenEnemyStartIsAlreadyKnown()
    {
        var decision = WorkerScoutPolicy.Evaluate(new WorkerScoutAssessment(
            WorkerScoutMode.WhenInformationIsMissing, 13, 12,
            OpponentStartKnown: true, ThreatInformationNeeded: true,
            MapHasMultipleStartLocations: true, ScoutAlreadyAssigned: false));

        Assert.Equal(WorkerScoutDecision.DoNotScout, decision);
    }

    [Fact]
    public void ScoutNeverConsumesWorkerReservedForMinimumMiningCoverage()
    {
        var decision = WorkerScoutPolicy.Evaluate(new WorkerScoutAssessment(
            WorkerScoutMode.AlwaysWhenAffordable, 12, 12,
            OpponentStartKnown: false, ThreatInformationNeeded: true,
            MapHasMultipleStartLocations: true, ScoutAlreadyAssigned: false));

        Assert.Equal(WorkerScoutDecision.DoNotScout, decision);
    }

    [Fact]
    public void PolicyNeverAssignsDuplicateScout()
    {
        var decision = WorkerScoutPolicy.Evaluate(new WorkerScoutAssessment(
            WorkerScoutMode.AlwaysWhenAffordable, 13, 12,
            OpponentStartKnown: false, ThreatInformationNeeded: true,
            MapHasMultipleStartLocations: true, ScoutAlreadyAssigned: true));

        Assert.Equal(WorkerScoutDecision.DoNotScout, decision);
    }

    [Fact]
    public void NeverModeIsExplicitlyNonScouting()
    {
        var decision = WorkerScoutPolicy.Evaluate(new WorkerScoutAssessment(
            WorkerScoutMode.Never, 20, 12,
            OpponentStartKnown: false, ThreatInformationNeeded: true,
            MapHasMultipleStartLocations: true, ScoutAlreadyAssigned: false));

        Assert.Equal(WorkerScoutDecision.DoNotScout, decision);
    }
}
