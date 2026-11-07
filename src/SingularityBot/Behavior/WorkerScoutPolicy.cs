namespace SingularityBot.Behavior;

/// <summary>Strategy-level choice for whether the opening economy may sacrifice a worker to scout.</summary>
public enum WorkerScoutMode
{
    Never = 0,
    WhenInformationIsMissing = 1,
    AlwaysWhenAffordable = 2
}

/// <summary>Evidence supplied by the match/economy layer; no SC2 protocol types.</summary>
public sealed record WorkerScoutAssessment(
    WorkerScoutMode Mode,
    int AvailableUnassignedWorkers,
    int MinimumMiningWorkersToPreserve,
    bool OpponentStartKnown,
    bool ThreatInformationNeeded,
    bool MapHasMultipleStartLocations,
    bool ScoutAlreadyAssigned);

public enum WorkerScoutDecision
{
    DoNotScout = 0,
    AssignScout = 1
}

/// <summary>
/// Conservative, deterministic scout gate. It decides whether to allocate a
/// worker; path selection and the actual scout action remain separate concerns.
/// </summary>
public static class WorkerScoutPolicy
{
    public static WorkerScoutDecision Evaluate(WorkerScoutAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        if (assessment.ScoutAlreadyAssigned ||
            assessment.AvailableUnassignedWorkers <= assessment.MinimumMiningWorkersToPreserve)
            return WorkerScoutDecision.DoNotScout;

        return assessment.Mode switch
        {
            WorkerScoutMode.Never => WorkerScoutDecision.DoNotScout,
            WorkerScoutMode.AlwaysWhenAffordable => WorkerScoutDecision.AssignScout,
            WorkerScoutMode.WhenInformationIsMissing =>
                !assessment.OpponentStartKnown &&
                (assessment.MapHasMultipleStartLocations || assessment.ThreatInformationNeeded)
                    ? WorkerScoutDecision.AssignScout
                    : WorkerScoutDecision.DoNotScout,
            _ => WorkerScoutDecision.DoNotScout
        };
    }
}
