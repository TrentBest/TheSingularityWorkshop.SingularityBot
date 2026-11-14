using System.Globalization;
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
            if (posting.Constraints is null || !posting.Constraints.TryGetValue("unitTag", out var tagText) ||
                !ulong.TryParse(tagText, NumberStyles.None, CultureInfo.InvariantCulture, out var tag))
            { results.Add(Reject(decision, "Accepted Scout posting lost its valid unitTag constraint.")); continue; }
            if (!TryParseTarget(posting.Target, out var target))
            { results.Add(Reject(decision, "Scout target is not a finite invariant-culture coordinate pair.")); continue; }
            if (issuedUnits.Contains(tag))
            { results.Add(Reject(decision, $"Scout unit {tag} already has an action in this frame.")); continue; }
            var translation = translator.TranslateScoutMove(tag, target, observation, map, gameData, terrain, topology);
            if (translation.IsBlocked)
            { results.Add(Reject(decision, translation.Reason)); continue; }
            if (translation.Actions.Count > 0)
            {
                issuedUnits.Add(tag);
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
    private static bool TryParseTarget(string value, out MapPoint target)
    {
        target = default;
        var parts = value?.Split(',');
        if (parts is not { Length: 2 } ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.IsFinite(x) || !float.IsFinite(y)) return false;
        target = new MapPoint(x, y);
        return true;
    }
    private static StrategicJobDecision Reject(StrategicJobDecision decision, string reason) =>
        new(decision.Posting, false, reason);
}

public sealed record StrategicScoutExecutionResult(
    IReadOnlyList<SC2APIProtocol.Action> Actions, IReadOnlyList<StrategicJobDecision> Decisions);
