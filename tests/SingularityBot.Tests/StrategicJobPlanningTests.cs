using SingularityBot.Behavior;
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
        Assert.Equal(3, result.Postings.Single(posting => posting.Target == "Marine").Quantity);
        Assert.Equal(2, result.Postings.Single(posting => posting.Target == "Marauder").Quantity);
        Assert.Equal(1, result.Postings.Single(posting => posting.Target == "Medivac").Quantity);
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
}
