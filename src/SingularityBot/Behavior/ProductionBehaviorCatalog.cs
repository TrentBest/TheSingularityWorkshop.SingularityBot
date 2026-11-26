 
using TheSingularityWorkshop.FSM_API;
using FsmApi = SingularityBot.Behavior.BotFsmApi;

namespace SingularityBot.Behavior;

/// <summary>Reusable production-step graph shared by all user-authored plans.</summary>
public static class ProductionBehaviorCatalog
{
    private static bool _registered;
    public const string ProductionStepFsm = "SingularityBot.ProductionStep";

    public static void Register()
    {
        if (_registered) return;

        FsmApi.Create.CreateFiniteStateMachine(
                ProductionStepFsm,
                processRate: -1,
                processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
            .State("Waiting", null, null, null)
            .State("Issuing", ctx =>
            {
                if (ctx is ProductionStepContext step && step.CanIssue)
                    step.EmitIntent();
            }, null, null)
            .State("AwaitingEvidence", null, null, null)
            .State("Completed", null, null, null)
            .WithInitialState("Waiting")
            .Transition("Waiting", "Issuing",
                ctx => ctx is ProductionStepContext step && step.CanIssue)
            .Transition("Issuing", "AwaitingEvidence",
                ctx => ctx is ProductionStepContext)
            .Transition("AwaitingEvidence", "Completed",
                ctx => ctx is ProductionStepContext step && step.IsComplete)
            .Transition("AwaitingEvidence", "Waiting",
                ctx => ctx is ProductionStepContext step && step.CanIssue)
            .Transition("Completed", "Waiting",
                ctx => ctx is ProductionStepContext step && !step.IsComplete)
            .Transition(BotFsmApi.AnyStateIdentifier, "Completed",
                ctx => ctx is ProductionStepContext step && step.IsComplete)
            .BuildDefinition();

        _registered = true;
    }
}
