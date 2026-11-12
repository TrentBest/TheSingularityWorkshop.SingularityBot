using SC2APIProtocol;
using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Feeds completed secondary Barracks with Marines. FSM_API owns the pending
/// production-request lifecycle; observations and action results are evidence.
/// </summary>
public sealed class Sc2BarracksProductionController : IDisposable
{
    public const string FsmName = "SingularityBot.SecondaryBarracksProduction";
    public const string ProcessingGroup = "SingularityBot.SecondaryBarracksProduction";
    private const string Ready = "Ready";
    private const string AwaitingEvidence = "AwaitingEvidence";

    private readonly ProductionRequestContext _context = new();
    private readonly FSMHandle _handle;

    public Sc2BarracksProductionController()
    {
        EnsureDefinition();
        _handle = FSM_API.Create.CreateInstance(FsmName, _context, ProcessingGroup);
    }

    public string CurrentState => _handle.CurrentState;
    public bool IsAwaitingEvidence => _handle.CurrentState == AwaitingEvidence;
    public ulong? PendingProducerTag => _context.PendingProducerTag;

    public SC2APIProtocol.Action TryCreateMarineTrainingAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        BuildJobRuntime buildJobs = null,
        uint reservedMinerals = 0)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        if (gameData.MarineTrainAbilityId is not > 0 || gameData.MarineMineralCost == 0)
            return null;

        buildJobs?.ReconcileMarineProduction(observation, gameData.MarineTrainAbilityId.Value);

        var barracks = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.BuildProgress >= 1f && unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !observation.DeadUnitTags.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .ToArray();
        var secondaryBarracks = barracks.Skip(1).ToArray();

        var pendingTag = _context.PendingProducerTag;
        var pendingProducer = pendingTag.HasValue
            ? secondaryBarracks.FirstOrDefault(unit => unit.Tag == pendingTag.Value)
            : null;
        var orderObserved = pendingProducer is not null &&
            pendingProducer.Orders.Any(order => order.AbilityId == gameData.MarineTrainAbilityId.Value);

        _context.ApplyObservation(observation.GameLoop, pendingProducer is not null, orderObserved);
        FSM_API.Interaction.Update(ProcessingGroup);
        ResolvePending(buildJobs, observation.GameLoop);

        if (_handle.CurrentState != Ready || secondaryBarracks.Length == 0)
            return null;

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

        var job = buildJobs?.CreateMarineProduction(
            observation.GameLoop,
            producer.Tag,
            observation.Units.Where(unit =>
                unit.Alliance == UnitAlliance.Self &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase))
                .Select(unit => unit.Tag));

        _context.MarkSubmitted(producer.Tag, observation.GameLoop, job?.Id);
        FSM_API.Interaction.Update(ProcessingGroup);

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

    public void OnActionResult(ActionResult? result, BuildJobRuntime buildJobs = null, uint gameLoop = 0)
    {
        if (result is null || result != ActionResult.Success)
        {
            _context.MarkRejected();
            FSM_API.Interaction.Update(ProcessingGroup);
            ResolvePending(buildJobs, gameLoop);
        }
    }

    public void Dispose()
    {
        _context.IsValid = false;
        FSM_API.Interaction.DestroyInstance(_handle);
    }

    private void ResolvePending(BuildJobRuntime buildJobs, uint gameLoop)
    {
        if (_handle.CurrentState != Ready || !_context.PendingProducerTag.HasValue)
            return;

        var reason = _context.ResolutionReason;
        if (reason is not null && reason != ProductionRequestContext.OrderObservedReason &&
            _context.PendingJobId is long id && buildJobs is not null &&
            buildJobs.TryGet(id, out var job))
        {
            var observedAt = gameLoop == 0 ? job.LastObservedGameLoop : gameLoop;
            job.Fail(reason, observedAt);
        }

        _context.ClearPending();
    }

    private static void EnsureDefinition()
    {
        if (FSM_API.Interaction.Exists(FsmName, ProcessingGroup))
            return;

        FSM_API.Create.CreateFiniteStateMachine(
                FsmName,
                processRate: -1,
                processingGroup: ProcessingGroup)
            .State(Ready, null, null, null)
            .State(AwaitingEvidence, null, null, null)
            .WithInitialState(Ready)
            .Transition(Ready, AwaitingEvidence, HasPendingRequest)
            .Transition(AwaitingEvidence, Ready, HasResolutionEvidence)
            .BuildDefinition();
    }

    // Named conditions expose the evidence that governs this request lifecycle.
    private static bool HasPendingRequest(IStateContext context) =>
        context is ProductionRequestContext request && request.PendingProducerTag.HasValue;

    private static bool HasResolutionEvidence(IStateContext context) =>
        context is ProductionRequestContext request && request.ResolutionReason is not null;

    private sealed class ProductionRequestContext : IStateContext
    {
        public const string OrderObservedReason = "Marine training order observed.";
        public string Name { get; set; } = "Secondary Barracks production request";
        public bool IsValid { get; set; } = true;
        public uint GameLoop { get; private set; }
        public uint RequestGameLoop { get; private set; }
        public ulong? PendingProducerTag { get; private set; }
        public long? PendingJobId { get; private set; }
        public bool PendingProducerPresent { get; private set; }
        public bool TrainingOrderObserved { get; private set; }
        public bool ActionRejected { get; private set; }
        public string ResolutionReason { get; private set; }

        public void ApplyObservation(uint gameLoop, bool producerPresent, bool orderObserved)
        {
            GameLoop = gameLoop;
            PendingProducerPresent = producerPresent;
            TrainingOrderObserved = orderObserved;
            ResolutionReason = DetermineResolutionReason(this);
        }

        public void MarkSubmitted(ulong producerTag, uint gameLoop, long? jobId)
        {
            PendingProducerTag = producerTag;
            RequestGameLoop = gameLoop;
            PendingJobId = jobId;
            PendingProducerPresent = true;
            TrainingOrderObserved = false;
            ActionRejected = false;
            ResolutionReason = null;
        }

        public void MarkRejected()
        {
            ActionRejected = true;
            ResolutionReason = "SC2 rejected the Marine training command.";
        }

        public void ClearPending()
        {
            PendingProducerTag = null;
            RequestGameLoop = 0;
            PendingJobId = null;
            PendingProducerPresent = false;
            TrainingOrderObserved = false;
            ActionRejected = false;
            ResolutionReason = null;
        }

        private static string DetermineResolutionReason(ProductionRequestContext request)
        {
            if (!request.PendingProducerTag.HasValue)
                return null;
            if (request.ActionRejected)
                return "SC2 rejected the Marine training command.";
            if (!request.PendingProducerPresent)
                return "Assigned Barracks disappeared before Marine training was observed.";
            if (request.TrainingOrderObserved)
                return OrderObservedReason;
            if (request.GameLoop > request.RequestGameLoop + 8)
                return "Marine training order was not observed before retry timeout.";
            return null;
        }
    }
}
