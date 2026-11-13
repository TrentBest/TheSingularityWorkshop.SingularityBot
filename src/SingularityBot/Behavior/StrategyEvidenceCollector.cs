using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Retains explicitly observed opponent-tech evidence for strategy evaluation.
/// Only currently visible enemy structures create facts; fogged observations
/// retain their original timestamp, and confirmed deaths remove the fact.
/// This collector does not infer technology or estimate strategic advantage.
/// </summary>
public sealed class StrategyEvidenceCollector
{
    private const string RoachWarrenEvidenceId = "enemy.structure.roach-warren";
    private const string EnemySubject = "enemy";

    private readonly Dictionary<ulong, StrategyEvidence> _observedStructures = new();

    public IReadOnlyList<StrategyEvidence> Apply(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        foreach (var deadTag in observation.DeadUnitTags)
            _observedStructures.Remove(deadTag);

        foreach (var unit in observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Enemy &&
            unit.Visibility == UnitVisibility.Visible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.UnitKind.Equals("RoachWarren", StringComparison.OrdinalIgnoreCase)))
        {
            _observedStructures[unit.Tag] = new StrategyEvidence(
                RoachWarrenEvidenceId,
                EnemySubject,
                1f,
                observation.GameLoop,
                IsObservedFact: true);
        }

        return _observedStructures.Values
            .OrderBy(evidence => evidence.EvidenceId, StringComparer.Ordinal)
            .ThenBy(evidence => evidence.Subject, StringComparer.Ordinal)
            .ThenBy(evidence => evidence.ObservedAtGameLoop)
            .ToArray();
    }

    public void Reset() => _observedStructures.Clear();
}
