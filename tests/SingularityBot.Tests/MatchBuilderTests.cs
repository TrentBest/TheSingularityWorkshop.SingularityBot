using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class MatchBuilderTests
{
    private static ProductionPlanDefinition Opening() =>
        new ProductionPlanBuilder("terran-opening", "Terran Opening")
            .Buildings.Add("supply-depot", "SupplyDepot", "SCV")
            .Buildings.Add("barracks", "Barracks", "SCV")
            .Build();

    private static StrategyTransitionDefinition BansheeDefense() =>
        new(
            "zerg-roach-response",
            "Terran Banshee Defense",
            new[]
            {
                new StrategyEvidenceRequirement(
                    "enemy-structure-observed", "RoachWarren", 0.9f, 32)
            },
            new[] { "banshee-tech-path-available", "natural-defense-covered", "anti-air-risk-acceptable" },
            MinimumAdvantage: 0.15,
            EvaluationHorizonGameLoops: 224,
            CooldownGameLoops: 448);

    [Fact]
    public void MatchBuilder_ComposesOpeningAndConditionalMidAndLateGameStrategies()
    {
        var definition = new MatchBuilder("terran-adaptive", "Terran Adaptive")
            .WithOpening(Opening())
            .WithMidGame(BansheeDefense())
            .WithLateGame(new StrategyTransitionDefinition(
                "late-game-air-transition",
                "Terran Air Control",
                new[] { new StrategyEvidenceRequirement("enemy-tech", "FleetBeacon", 0.8f, 64) },
                new[] { "air-production-online" },
                0.2,
                448,
                896))
            .Build();

        Assert.Equal("terran-adaptive", definition.Id);
        Assert.Equal(2, definition.Opening.Steps.Count);
        Assert.Single(definition.MidGame);
        Assert.Single(definition.LateGame);
        Assert.Equal("zerg-roach-response", definition.MidGame[0].Id);
    }

    [Fact]
    public void TransitionEvaluator_RequiresEvidenceHardGuardsCooldownAndAdvantage()
    {
        var transition = BansheeDefense();
        var evidence = new[]
        {
            new StrategyEvidence("enemy-structure-observed", "RoachWarren", 0.95f, 100, true)
        };

        var eligible = StrategyTransitionEvaluator.Evaluate(transition,
            new StrategyTransitionContext(120, null, evidence,
                new HashSet<string>
                {
                    "banshee-tech-path-available",
                    "natural-defense-covered",
                    "anti-air-risk-acceptable"
                }, 0.2));

        Assert.True(eligible.IsEligible);
        Assert.Equal(StrategyTransitionStatus.Eligible, eligible.Status);

        var unsafeDefense = StrategyTransitionEvaluator.Evaluate(transition,
            new StrategyTransitionContext(120, null, evidence,
                new HashSet<string>
                {
                    "banshee-tech-path-available",
                    "anti-air-risk-acceptable"
                }, 0.9));
        Assert.Equal(StrategyTransitionStatus.HardGuardFailed, unsafeDefense.Status);

        var coolingDown = StrategyTransitionEvaluator.Evaluate(transition,
            new StrategyTransitionContext(120, 100, evidence,
                new HashSet<string>
                {
                    "banshee-tech-path-available",
                    "natural-defense-covered",
                    "anti-air-risk-acceptable"
                }, 0.9));
        Assert.Equal(StrategyTransitionStatus.CooldownActive, coolingDown.Status);
    }

    [Fact]
    public void TransitionEvaluator_RejectsStaleOrLowConfidenceEvidenceAndLowAdvantage()
    {
        var transition = BansheeDefense();
        var guards = new HashSet<string>
        {
            "banshee-tech-path-available",
            "natural-defense-covered",
            "anti-air-risk-acceptable"
        };

        var stale = StrategyTransitionEvaluator.Evaluate(transition,
            new StrategyTransitionContext(200, null,
                new[] { new StrategyEvidence("enemy-structure-observed", "RoachWarren", 1f, 100, true) },
                guards, 1));
        Assert.Equal(StrategyTransitionStatus.StaleEvidence, stale.Status);

        var uncertain = StrategyTransitionEvaluator.Evaluate(transition,
            new StrategyTransitionContext(120, null,
                new[] { new StrategyEvidence("enemy-structure-observed", "RoachWarren", 0.5f, 110, false) },
                guards, 1));
        Assert.Equal(StrategyTransitionStatus.InsufficientConfidence, uncertain.Status);

        var poorTrade = StrategyTransitionEvaluator.Evaluate(transition,
            new StrategyTransitionContext(120, null,
                new[] { new StrategyEvidence("enemy-structure-observed", "RoachWarren", 1f, 110, true) },
                guards, 0.1));
        Assert.Equal(StrategyTransitionStatus.InsufficientAdvantage, poorTrade.Status);
    }

    [Fact]
    public void MatchBuilder_RejectsDuplicateTransitionIds()
    {
        var transition = BansheeDefense();
        var builder = new MatchBuilder("match", "Match")
            .WithOpening(Opening())
            .WithMidGame(transition)
            .WithLateGame(transition);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
