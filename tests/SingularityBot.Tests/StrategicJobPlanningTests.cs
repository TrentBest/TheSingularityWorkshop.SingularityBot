using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class StrategicJobPlanningTests
{
    [Fact]
    public void AdvisorsOwnAndReplaceTheirOwnPostings()
    {
        var board = new AdvisorJobBoard();
        var first = board.Upsert(AdvisorDomain.Intelligence, "scout:natural",
            StrategicJobKind.Scout, "natural", 1, 80, 100);
        var updated = board.Upsert(AdvisorDomain.Intelligence, "scout:natural",
            StrategicJobKind.Scout, "natural", 1, 95, 120);

        Assert.Equal(100u, updated.CreatedAtGameLoop);
        Assert.Equal(120u, updated.UpdatedAtGameLoop);
        Assert.Equal(95, Assert.Single(board.GetActive(120)).Priority);
        Assert.Throws<InvalidOperationException>(() =>
            board.Upsert(AdvisorDomain.Military, "scout:natural",
                StrategicJobKind.Scout, "natural", 1, 90, 121));
        Assert.Throws<InvalidOperationException>(() =>
            board.Withdraw(AdvisorDomain.Military, "scout:natural"));
        Assert.True(board.Withdraw(AdvisorDomain.Intelligence, first.Id));
        Assert.Empty(board.GetActive(120));
    }

    [Fact]
    public void ExpiredAdvisorPostingsAreNotOfferedToPlanner()
    {
        var board = new AdvisorJobBoard();
        board.Upsert(AdvisorDomain.Intelligence, "scout:third",
            StrategicJobKind.Scout, "third", 1, 80, 100, expiresAtGameLoop: 150);

        Assert.Single(new StrategicPlanner().GetPlan(board, 150));
        Assert.Empty(new StrategicPlanner().GetPlan(board, 151));
    }

    [Fact]
    public void CompositionGoalCompilesForceAndProductionCapacityIntoSharedJobMarket()
    {
        var board = new AdvisorJobBoard();
        var goal = new CompositionGoalDefinition
        {
            GoalId = "main-army",
            InitialBuildOutMinutes = 10,
            Units =
            [
                new("Marine", 50, 2),
                new("Marauder", 25, 1),
                new("Medivac", 10, 0.5)
            ]
        };
        var recipes = new Dictionary<string, ProductionRecipe>(StringComparer.OrdinalIgnoreCase)
        {
            ["Marine"] = new("Marine", "Barracks", 18, null, ["SupplyDepot"]),
            ["Marauder"] = new("Marauder", "Barracks", 21, "TechLab", ["SupplyDepot"]),
            ["Medivac"] = new("Medivac", "Starport", 30, null, ["Factory"])
        };

        var result = CompositionGoalCompiler.Compile(goal, recipes, board, 500);

        Assert.Empty(result.Errors);
        Assert.Equal(50, result.Postings.Single(posting => posting.Target == "Marine" && posting.Kind == StrategicJobKind.ProduceUnit).Quantity);
        Assert.Equal(25, result.Postings.Single(posting => posting.Target == "Marauder" && posting.Kind == StrategicJobKind.ProduceUnit).Quantity);
        Assert.Equal(10, result.Postings.Single(posting => posting.Target == "Medivac" && posting.Kind == StrategicJobKind.ProduceUnit).Quantity);
        Assert.Equal(4, result.Postings.Single(posting => posting.Target == "Barracks" &&
            posting.Kind == StrategicJobKind.BuildStructure).Quantity);
        Assert.Equal(1, result.Postings.Single(posting => posting.Target == "Starport").Quantity);
        Assert.Equal(2, result.Postings.Single(posting => posting.Target == "TechLab").Quantity);
        Assert.Contains(result.Postings, posting => posting.Target == "Factory");
        Assert.Contains(result.Postings, posting => posting.Target == "SupplyDepot");
        Assert.All(result.Postings, posting => Assert.Equal(StrategicJobSource.CompositionGoal, posting.Source));
        Assert.Equal(result.Postings.Count, new StrategicPlanner().GetPlan(board, 500).Count);
    }

    [Fact]
    public void InvalidCompositionDoesNotErasePreviouslyPublishedGoal()
    {
        var board = new AdvisorJobBoard();
        board.UpsertComposition("main-army", "existing", StrategicJobKind.ProduceUnit,
            "Marine", 10, 50, 100);
        var goal = new CompositionGoalDefinition
        {
            GoalId = "main-army",
            Units = [new("Marauder", 10)]
        };

        var result = CompositionGoalCompiler.Compile(goal,
            new Dictionary<string, ProductionRecipe>
            {
                ["Marauder"] = new("Marauder", "Barracks", 0)
            }, board, 200);

        Assert.NotEmpty(result.Errors);
        Assert.Equal("existing", Assert.Single(board.GetActive(200)).Id);
    }

    [Fact]
    public void CompositionGoalCanWithdrawItsEntireSetOfPostings()
    {
        var board = new AdvisorJobBoard();
        board.UpsertComposition("goal-a", "a:1", StrategicJobKind.ProduceUnit, "Marine", 10, 50, 1);
        board.UpsertComposition("goal-a", "a:2", StrategicJobKind.BuildStructure, "Barracks", 1, 50, 1);
        board.UpsertComposition("goal-b", "b:1", StrategicJobKind.Scout, "natural", 1, 40, 1);

        Assert.Equal(2, board.WithdrawComposition("goal-a"));
        Assert.Single(board.GetActive(1));
        Assert.Equal("goal-b", Assert.Single(board.GetActive(1)).Owner);
    }

    [Fact]
    public void PlannerAcceptsOnlyValidScoutJobsAndReservesUnitsByPriority()
    {
        var map = new SpatialMap(
            new MapBounds(0, 0, 90, 90),
            new MapPoint(10, 10),
            new MapPoint(80, 80));
        var board = new AdvisorJobBoard();
        board.Upsert(AdvisorDomain.Intelligence, "scout:first",
            StrategicJobKind.Scout, "20,20", 1, 80, 100,
            constraints: new Dictionary<string, string> { ["unitTag"] = "7" });
        board.Upsert(AdvisorDomain.Military, "scout:duplicate",
            StrategicJobKind.Scout, "30,30", 1, 70, 100,
            constraints: new Dictionary<string, string> { ["unitTag"] = "7" });
        board.Upsert(AdvisorDomain.Intelligence, "scout:bad-target",
            StrategicJobKind.Scout, "NaN,30", 1, 60, 100,
            constraints: new Dictionary<string, string> { ["unitTag"] = "8" });
        board.UpsertComposition("goal", "goal:marine",
            StrategicJobKind.ProduceUnit, "Marine", 10, 50, 100);

        var observation = new NormalizedObservation(
            100, 1,
            new[]
            {
                new UnitObservationFacts(
                    7, 1, "Marine", 1, UnitAlliance.Self,
                    UnitVisibility.Visible,
                    45, 45, 0, 0, 1, 15, 15, true, false, false,
                    false, false, false, 100),
                new UnitObservationFacts(
                    8, 1, "Marine", 1, UnitAlliance.Self,
                    UnitVisibility.Visible,
                    45, 45, 0, 0, 1, 16, 16, true, false, false,
                    false, false, false, 100)
            },
            new HashSet<ulong>());

        var decisions = new StrategicPlanner().Evaluate(board, 100, observation, map);

        Assert.Equal(4, decisions.Count);
        Assert.True(decisions[0].IsAccepted);
        Assert.Contains("uniquely reserved", decisions[0].Reason);
        Assert.False(decisions[1].IsAccepted);
        Assert.Contains("already reserved", decisions[1].Reason);
        Assert.False(decisions[2].IsAccepted);
        Assert.Contains("finite world-space point", decisions[2].Reason);
        Assert.False(decisions[3].IsAccepted);
        Assert.Contains("No validated executor", decisions[3].Reason);
    }

    [Fact]
    public void PlannerRejectsScoutJobsForMissingOrDeadUnits()
    {
        var map = new SpatialMap(
            new MapBounds(0, 0, 90, 90),
            new MapPoint(10, 10),
            new MapPoint(80, 80));
        var board = new AdvisorJobBoard();
        board.Upsert(AdvisorDomain.Intelligence, "scout:missing",
            StrategicJobKind.Scout, "20,20", 1, 80, 100,
            constraints: new Dictionary<string, string> { ["unitTag"] = "99" });
        var observation = new NormalizedObservation(
            100, 1, Array.Empty<UnitObservationFacts>(),
            new HashSet<ulong> { 99 });

        var decision = Assert.Single(new StrategicPlanner().Evaluate(board, 100, observation, map));
        Assert.False(decision.IsAccepted);
        Assert.Contains("not a live, visible friendly unit", decision.Reason);
    }
}
