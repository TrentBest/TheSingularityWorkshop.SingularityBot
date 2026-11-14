using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Dispatches accepted Scout jobs through the shared military-operation action translator.
/// This class owns job parsing and per-frame arbitration, not raw SC2 command construction.
/// </summary>
public static class StrategicScoutActionExecutor
{
    public static StrategicScoutExecutionResult Execute(
        IReadOnlyList<StrategicJobDecision> decisions, NormalizedObservation observation,
        SpatialMap map, Sc2GameDataCatalog gameData, SpatialTerrain terrain, SpatialTopology topology)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(gameData);
        var results = new List<StrategicJobDecision>();
        var actions = new List<SC2APIProtocol.Action>();
        var issuedUnits = new HashSet<ulong>();
        var translator = new Sc2MilitaryOperationActionTranslator();
        foreach (var decision in decisions)
        {
            if (!decision.IsAccepted || decision.Posting.Kind != StrategicJobKind.Scout)
            { results.Add(decision); continue; }
            var posting = decision.Posting;
            if (decision.Reservation?.UnitTag is not ulong tag ||
                decision.Reservation.TargetPoint is not MapPoint target)
            { results.Add(Reject(decision, "Accepted Scout decision is missing its typed unit and target reservation.")); continue; }
            if (issuedUnits.Contains(tag))
            { results.Add(Reject(decision, $"Scout unit {tag} already has an action in this frame.")); continue; }
            var translation = translator.TranslateScoutMove(tag, target, observation, map, gameData, terrain, topology);
            if (translation.IsBlocked)
            { results.Add(Reject(decision, translation.Reason)); continue; }
            // A valid existing order still occupies this unit's strategic slot for the frame.
            issuedUnits.Add(tag);
            if (translation.Actions.Count > 0)
            {
                actions.AddRange(translation.Actions);
                results.Add(new StrategicJobDecision(posting, true,
                    "Validated Scout Move action emitted through the shared SC2 action translator."));
                continue;
            }
            results.Add(new StrategicJobDecision(posting, true, string.IsNullOrWhiteSpace(translation.Reason)
                ? "Scout intent validated; no new command was necessary." : translation.Reason));
        }
        return new StrategicScoutExecutionResult(actions.AsReadOnly(), results.AsReadOnly());
    }
    private static StrategicJobDecision Reject(StrategicJobDecision decision, string reason) =>
        new(decision.Posting, false, reason);
}

public sealed record StrategicScoutExecutionResult(
    IReadOnlyList<SC2APIProtocol.Action> Actions, IReadOnlyList<StrategicJobDecision> Decisions);
