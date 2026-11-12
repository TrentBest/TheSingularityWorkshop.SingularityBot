using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Translates secondary-Barracks production intent into SC2 commands.
/// BuildJobRuntime's FSM_API instance owns the request lifecycle; this adapter
/// retains only producer/job identity needed to correlate the command result.
/// </summary>
public sealed class Sc2BarracksProductionController
{
    private ulong? _pendingBarracksTag;
    private long? _pendingJobId;

    public ulong? PendingProducerTag => _pendingBarracksTag;

    public SC2APIProtocol.Action TryCreateMarineTrainingAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        BuildJobRuntime buildJobs,
        uint reservedMinerals = 0)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(buildJobs);
        if (gameData.MarineTrainAbilityId is not > 0 || gameData.MarineMineralCost == 0)
            return null;

        buildJobs.ReconcileMarineProduction(observation, gameData.MarineTrainAbilityId.Value);

        var barracks = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.BuildProgress >= 1f && unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        // The production job FSM is the authority for whether the previous
        // request is still pending, executing, or terminal.
        if (_pendingBarracksTag is ulong pendingTag)
        {
            var pendingProducer = barracks.Skip(1).FirstOrDefault(unit => unit.Tag == pendingTag);
            BuildJobContext pendingJob = null;
            var hasJob = _pendingJobId is long id && buildJobs.TryGet(id, out pendingJob);
            if (hasJob && (pendingJob.IsExecuting || pendingJob.HasExecutionEvidence))
            {
                ClearPending();
            }
            else if (pendingProducer is null)
            {
                FailPending(buildJobs, observation.GameLoop,
                    "Assigned Barracks disappeared before Marine training was observed.");
                ClearPending();
            }
            else if (hasJob && (pendingJob.IsFailed || pendingJob.HasFailureEvidence))
            {
                ClearPending();
            }
            else if (hasJob && observation.GameLoop > pendingJob.CreatedAtGameLoop + 8)
            {
                FailPending(buildJobs, observation.GameLoop,
                    "Marine training order was not observed before retry timeout.");
                ClearPending();
            }
            else
            {
                return null;
            }
        }

        // Sc2OpeningMarineController owns the first Barracks and its first
        // Marine. This controller scales the rest of the production line.
        if (barracks.Length < 2)
            return null;
        var secondaryBarracks = barracks.Skip(1).ToArray();

        // The opening force goal is 60 Marines. Stop adding units at the
        // target so the economy can fund expansions and later composition goals.
        var marineCount = observation.Units.Count(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag));
        if (marineCount >= 60)
            return null;

        if (observation.Minerals < gameData.MarineMineralCost ||
            observation.Minerals - gameData.MarineMineralCost < reservedMinerals ||
            observation.FoodCap < observation.FoodUsed ||
            observation.FoodCap - observation.FoodUsed < gameData.MarineFoodRequired)
            return null;

        var producer = secondaryBarracks.FirstOrDefault(unit => !unit.HasOrders);
        if (producer is null)
            return null;

        var job = buildJobs.CreateMarineProduction(
            observation.GameLoop,
            producer.Tag,
            observation.Units.Where(unit =>
                unit.Alliance == UnitAlliance.Self &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase))
                .Select(unit => unit.Tag));
        _pendingBarracksTag = producer.Tag;
        _pendingJobId = job.Id;

        return new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)gameData.MarineTrainAbilityId.Value),
                    UnitTags = { producer.Tag },
                    QueueCommand = false
                }
            }
        };
    }

    public void OnActionResult(ActionResult? result, BuildJobRuntime buildJobs, uint gameLoop = 0)
    {
        ArgumentNullException.ThrowIfNull(buildJobs);
        if (result is null || result != ActionResult.Success)
        {
            FailPending(buildJobs, gameLoop, "SC2 rejected the Marine training command.");
            ClearPending();
        }
    }

    private void FailPending(BuildJobRuntime buildJobs, uint gameLoop, string reason)
    {
        if (_pendingJobId is long id && buildJobs.TryGet(id, out var job))
            job.Fail(reason, gameLoop == 0 ? job.LastObservedGameLoop : gameLoop);
    }

    private void ClearPending()
    {
        _pendingBarracksTag = null;
        _pendingJobId = null;
    }
}
