using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class MilitaryPlanDefinitionTests
{
    [Fact]
    public void MilitaryPlan_ValidatesNamedRoutesAndSynchronizedBattlecruiserOperations()
    {
        var plan = new MilitaryPlanDefinition
        {
            Id = "battlecruiser-mineral-line-pincer",
            Name = "Battlecruiser Mineral Line Pincer",
            Locations =
            [
                new("OpponentMainMineralLine", "Opponent main mineral line", 120, 80, 8),
                new("OpponentExpansionMineralLine", "Opponent expansion mineral line", 96, 112, 8)
            ],
            Routes =
            [
                new("ConcealedMainApproach", "Concealed main approach",
                    [new("ApproachStart", 40, 40), new("MainFlank", 82, 55), new("MainMineralLine", 120, 80)],
                    MilitaryRouteConstraints.AvoidKnownThreats | MilitaryRouteConstraints.AvoidContact),
                new("TacticalJumpApproach", "Tactical jump approach",
                    [new("JumpOrigin", 70, 70), new("ExpansionLanding", 96, 112)])
            ],
            Operations =
            [
                new("FirstBattlecruiser", "Position over main mineral line",
                    MilitaryForceRole.Harass, "ConcealedMainApproach", "OpponentMainMineralLine",
                    SynchronizationGroup: "MineralLinePincer"),
                new("SecondBattlecruiser", "Jump to expansion mineral line",
                    MilitaryForceRole.Harass, "TacticalJumpApproach", "OpponentExpansionMineralLine",
                    DependsOn: new[] { "FirstBattlecruiser" },
                    RequiredAbility: "TacticalJump",
                    SynchronizationGroup: "MineralLinePincer")
            ]
        };

        Assert.Empty(MilitaryPlanValidator.Validate(plan));
        Assert.Contains("FirstBattlecruiser", plan.Operations[1].DependsOn);
        Assert.Equal(MilitaryRouteConstraints.AvoidKnownThreats | MilitaryRouteConstraints.AvoidContact,
            plan.Routes[0].Constraints);
    }

    [Fact]
    public void MilitaryPlanValidator_RejectsUnknownReferencesAndDependencyCycles()
    {
        var plan = new MilitaryPlanDefinition
        {
            Id = "invalid",
            Name = "Invalid",
            Locations = [new("main", "Main", 10, 10)],
            Routes = [new("route", "Route", [new("a", 0, 0), new("b", 10, 10)])],
            Operations =
            [
                new("first", "First", MilitaryForceRole.Harass, "missing-route", "main",
                    DependsOn: new[] { "second" }),
                new("second", "Second", MilitaryForceRole.Harass, "route", "missing-location",
                    DependsOn: new[] { "first" })
            ]
        };

        var errors = MilitaryPlanValidator.Validate(plan);

        Assert.Contains(errors, error => error.Contains("unknown route"));
        Assert.Contains(errors, error => error.Contains("unknown location"));
        Assert.Contains(errors, error => error.Contains("cycle"));
    }

    [Fact]
    public void MilitaryPlanParser_ReadsNamedRoutesAndStringEnums()
    {
        const string json = """
        {
          "version": 1,
          "id": "raid",
          "name": "Mineral line raid",
          "locations": [{ "id": "main", "name": "Main mineral line", "x": 80, "y": 90 }],
          "routes": [{
            "id": "approach",
            "name": "Evasive approach",
            "constraints": "avoidKnownThreats, avoidContact",
            "waypoints": [
              { "id": "start", "x": 10, "y": 10 },
              { "id": "finish", "x": 80, "y": 90 }
            ]
          }],
          "operations": [{
            "id": "first-bc",
            "name": "First Battlecruiser",
            "role": "harass",
            "routeId": "approach",
            "objectiveLocationId": "main",
            "requiredAbility": "TacticalJump",
            "completion": "atObjective"
          }]
        }
        """;

        var plan = MilitaryPlanDefinition.Parse(json);

        Assert.Empty(MilitaryPlanValidator.Validate(plan));
        Assert.Equal(MilitaryForceRole.Harass, Assert.Single(plan.Operations).Role);
    }
}
