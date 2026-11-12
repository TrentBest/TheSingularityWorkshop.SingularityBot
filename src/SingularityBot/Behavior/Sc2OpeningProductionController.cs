using SC2APIProtocol;
using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Keeps SCV production running from every available Command Center.
/// FSM_API owns the request lifecycle; observations and action results are facts
/// supplied by the SC2 adapter.
/// </summary>
public sealed class Sc2OpeningProductionController : IDisposable
{
    public const string FsmName = "SingularityBot.OpeningScvProduction";
    public const string ProcessingGroup = "SingularityBot.OpeningProduction";
    private const string Ready = "Ready";
    private const string AwaitingEvidence = "AwaitingEvidence";

    private readonly OpeningProductionContext _context = new();
    private readonly FSMHandle _handle;

    public Sc2OpeningProductionController()
    {
        EnsureDefinition();
        _handle = FSM_API.Create.CreateInstance(FsmName, _context, ProcessingGroup);
    }

    /// <summary>The authoritative FSM_API state for this production request lifecycle.</summary>
    public string CurrentState => _handle.CurrentState;
    public bool IsAwaitingObservedOrder => _handle.CurrentState == AwaitingEvidence;
    public bool HasObservedTrainingOrder => _context.HasObservedTrainingOrder;

    public SC2APIProtocol.Action TryCreateScvTrainingAction(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        uint reservedMinerals = 0,
        ulong? reservedCommandCenterTag = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);

        var commandCenters = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           unit.HasPosition &&
                           !observation.DeadUnitTags.Contains(unit.Tag) &&
                           (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
                            unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(unit => unit.Tag)
            .ToArray();

        _context.ApplyObservation(
            observation.GameLoop,
            gameData.ScvTrainAbilityId is > 0 &&
            commandCenters.Any(center => center.Orders.Any(order =>
                order.AbilityId == gameData.ScvTrainAbilityId.Value)));
        FSM_API.Interaction.Update(ProcessingGroup);

        if (_handle.CurrentState != Ready ||
            gameData.ScvTrainAbilityId is not > 0 ||
            gameData.ScvMineralCost == 0 ||
            commandCenters.Length == 0)
            return null;

        if (observation.Minerals < gameData.ScvMineralCost ||
            observation.Minerals - gameData.ScvMineralCost < reservedMinerals ||
            observation.FoodCap < observation.FoodUsed ||
            observation.FoodCap - observation.FoodUsed < gameData.ScvFoodRequired)
            return null;

        var idleCommandCenter = commandCenters.FirstOrDefault(center =>
            !center.HasOrders && center.Tag != reservedCommandCenterTag);
        if (idleCommandCenter is null)
            return null;

        var action = new SC2APIProtocol.Action
        {
            ActionRaw = new ActionRaw
            {
                UnitCommand = new ActionRawUnitCommand
                {
                    AbilityId = checked((int)gameData.ScvTrainAbilityId.Value),
                    UnitTags = { idleCommandCenter.Tag },
                    QueueCommand = false
                }
            }
        };

        _context.MarkSubmitted(observation.GameLoop);
        FSM_API.Interaction.Update(ProcessingGroup);
        return action;
    }

    public void OnActionResult(ActionResult? result)
    {
        if (result is null || result != ActionResult.Success)
        {
            _context.MarkRejected();
            FSM_API.Interaction.Update(ProcessingGroup);
            _context.ClearRejection();
        }
    }

    public void Dispose()
    {
        _context.IsValid = false;
        FSM_API.Interaction.DestroyInstance(_handle);
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
            .Transition(Ready, AwaitingEvidence, HasSubmissionRequest)
            .Transition(AwaitingEvidence, Ready, HasResolutionEvidence)
            .BuildDefinition();
    }

    // Named condition functions keep the transition contract inspectable and testable.
    private static bool HasSubmissionRequest(IStateContext context) =>
        context is OpeningProductionContext production && production.SubmissionRequested;

    private static bool HasResolutionEvidence(IStateContext context) =>
        context is OpeningProductionContext production &&
        (production.HasObservedTrainingOrder ||
         production.ActionRejected ||
         production.GameLoop > production.RequestGameLoop);

    private sealed class OpeningProductionContext : IStateContext
    {
        public string Name { get; set; } = "Opening SCV production";
        public bool IsValid { get; set; } = true;
        public uint GameLoop { get; private set; }
        public uint RequestGameLoop { get; private set; }
        public bool SubmissionRequested { get; private set; }
        public bool HasObservedTrainingOrder { get; private set; }
        public bool ActionRejected { get; private set; }

        public void ApplyObservation(uint gameLoop, bool hasObservedTrainingOrder)
        {
            GameLoop = gameLoop;
            HasObservedTrainingOrder = hasObservedTrainingOrder;
            SubmissionRequested = false;
            ActionRejected = false;
        }

        public void MarkSubmitted(uint gameLoop)
        {
            RequestGameLoop = gameLoop;
            SubmissionRequested = true;
        }

        public void MarkRejected() => ActionRejected = true;
        public void ClearRejection() => ActionRejected = false;
    }
}
