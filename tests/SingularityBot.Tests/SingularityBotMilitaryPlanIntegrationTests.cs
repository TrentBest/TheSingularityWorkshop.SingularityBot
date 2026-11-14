using Xunit;

namespace SingularityBot.Tests;

public sealed class SingularityBotMilitaryPlanIntegrationTests
{
    [Fact]
    public void Bot_AcceptsMilitaryPlanAndExplicitForceAssignments()
    {
        var bot = new global::SingularityBot.SingularityBot();

        var errors = bot.ConfigureMilitaryPlanJson(ValidPlan("first-plan"));

        Assert.Empty(errors);
        var operation = Assert.Single(bot.MilitaryOperations);
        Assert.Equal("first-bc", operation.Definition.Id);
        var assignmentErrors = bot.AssignMilitaryOperationUnits("first-bc", new ulong[] { 101, 102 });
        Assert.Empty(assignmentErrors);
    }

    [Fact]
    public void Bot_RejectsInvalidReplacementWithoutDiscardingActiveMilitaryPlan()
    {
        var bot = new global::SingularityBot.SingularityBot();
        Assert.Empty(bot.ConfigureMilitaryPlanJson(ValidPlan("active-plan")));

        var errors = bot.ConfigureMilitaryPlanJson("""
        {
          "version": 1,
          "id": "invalid-plan",
          "name": "Invalid",
          "locations": [{ "id": "main", "name": "Main", "x": 5, "y": 5 }],
          "routes": [{ "id": "route", "name": "Route", "waypoints": [
            { "id": "start", "x": 1, "y": 1 }, { "id": "end", "x": 5, "y": 5 }
          ] }],
          "operations": [{ "id": "bad", "name": "Bad", "role": "harass",
            "routeId": "missing", "objectiveLocationId": "main" }]
        }
        """);

        Assert.NotEmpty(errors);
        Assert.Equal("first-bc", Assert.Single(bot.MilitaryOperations).Definition.Id);
    }

    [Fact]
    public void Bot_RejectsConflictingMilitaryUnitOwnershipWithoutPartialMutation()
    {
        var bot = new global::SingularityBot.SingularityBot();
        Assert.Empty(bot.ConfigureMilitaryPlanJson(ValidPlanWithTwoOperations()));
        Assert.Empty(bot.AssignMilitaryOperationUnits("first-bc", new ulong[] { 101, 102 }));

        var errors = bot.AssignMilitaryOperationUnits("second-bc", new ulong[] { 102, 103 });

        Assert.NotEmpty(errors);
        Assert.Contains("already assigned", errors[0]);
        Assert.True(bot.TryGetMilitaryUnitAssignment(101, out var firstOwner));
        Assert.Equal("first-bc", firstOwner);
        Assert.True(bot.TryGetMilitaryUnitAssignment(102, out var sharedOwner));
        Assert.Equal("first-bc", sharedOwner);
        Assert.False(bot.TryGetMilitaryUnitAssignment(103, out _));
    }

    [Fact]
    public void Bot_ReplacingAndReleasingOperationAssignmentsUpdatesBothIndexes()
    {
        var bot = new global::SingularityBot.SingularityBot();
        Assert.Empty(bot.ConfigureMilitaryPlanJson(ValidPlanWithTwoOperations()));
        Assert.Empty(bot.AssignMilitaryOperationUnits("first-bc", new ulong[] { 101, 102 }));

        Assert.Empty(bot.AssignMilitaryOperationUnits("first-bc", new ulong[] { 102, 103 }));

        Assert.False(bot.TryGetMilitaryUnitAssignment(101, out _));
        Assert.True(bot.TryGetMilitaryUnitAssignment(102, out var owner));
        Assert.Equal("first-bc", owner);
        Assert.True(bot.TryGetMilitaryUnitAssignment(103, out owner));
        Assert.Equal("first-bc", owner);
        Assert.True(bot.ReleaseMilitaryOperationUnits("first-bc"));
        Assert.False(bot.TryGetMilitaryUnitAssignment(102, out _));
        Assert.False(bot.TryGetMilitaryUnitAssignment(103, out _));
        Assert.False(bot.ReleaseMilitaryOperationUnits("first-bc"));
    }

    private static string ValidPlanWithTwoOperations() => """
    {
      "version": 1,
      "id": "two-operations",
      "name": "Two independent operations",
      "locations": [{ "id": "main", "name": "Opponent main", "x": 20, "y": 20 }],
      "routes": [{ "id": "route", "name": "Direct route", "waypoints": [
        { "id": "start", "x": 2, "y": 2 }, { "id": "end", "x": 20, "y": 20 }
      ] }],
      "operations": [
        { "id": "first-bc", "name": "First", "role": "harass",
          "routeId": "route", "objectiveLocationId": "main", "requiredUnitCount": 1 },
        { "id": "second-bc", "name": "Second", "role": "harass",
          "routeId": "route", "objectiveLocationId": "main", "requiredUnitCount": 1 }
      ]
    }
    """;

    private static string ValidPlan(string id) => $"""
    {
      "version": 1,
      "id": "{{id}}",
      "name": "Battlecruiser raid",
      "locations": [{ "id": "main", "name": "Opponent main mineral line", "x": 20, "y": 20, "radius": 4 }],
      "routes": [{ "id": "evasive", "name": "Evasive approach",
        "constraints": "avoidKnownThreats, avoidContact, preferPathableTerrain",
        "waypoints": [
          { "id": "start", "x": 2, "y": 2 },
          { "id": "main", "x": 20, "y": 20 }
        ] }],
      "operations": [{ "id": "first-bc", "name": "Position first Battlecruiser",
        "role": "harass", "routeId": "evasive", "objectiveLocationId": "main",
        "requiredUnitCount": 1, "completion": "atObjective" }]
    }
    """;
}
