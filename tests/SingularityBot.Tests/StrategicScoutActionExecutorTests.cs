using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using Xunit;

namespace SingularityBot.Tests;

public sealed class StrategicScoutActionExecutorTests
{
    [Fact]
    public void Execute_EmitsMoveOnlyForAcceptedScoutWithValidatedPathing()
    {
        var map = Map();
        var terrain = OpenTerrain();
        var topology = SpatialTopology.Build(terrain);
        var gameData = MoveCatalog();
        var posting = ScoutPosting("scout:unit:10", 10, "8,8");

        var result = StrategicScoutActionExecutor.Execute(
            [new StrategicJobDecision(posting, true, "accepted")],
            Snapshot(Unit(10, 1, 1)),
            map, gameData, terrain, topology);

        var action = Assert.Single(result.Actions);
        Assert.Equal(16, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(new ulong[] { 10 }, action.ActionRaw.UnitCommand.UnitTags);
        Assert.Equal(8, action.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(8, action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
        Assert.True(Assert.Single(result.Decisions).IsAccepted);
    }

    [Fact]
    public void Execute_RejectsDisconnectedTargetAndEmitsNoAction()
    {
        var map = Map();
        var cells = Enumerable.Repeat((byte)1, 100).ToArray();
        for (var y = 0; y < 10; y++) cells[y * 10 + 5] = 0;
        var terrain = new SpatialTerrain(new SpatialGridSnapshot(10, 10, 10, 10, cells), null, null);
        var result = StrategicScoutActionExecutor.Execute(
            [new StrategicJobDecision(ScoutPosting("scout:unit:10", 10, "8,8"), true, "accepted")],
            Snapshot(Unit(10, 1, 1)), map, MoveCatalog(), terrain, SpatialTopology.Build(terrain));

        Assert.Empty(result.Actions);
        var decision = Assert.Single(result.Decisions);
        Assert.False(decision.IsAccepted);
        Assert.Contains("connected", decision.Reason);
    }

    [Fact]
    public void Execute_EmitsAtMostOneActionForAUnitInOneFrame()
    {
        var map = Map();
        var terrain = OpenTerrain();
        var posting = ScoutPosting("scout:unit:10", 10, "8,8");
        var duplicate = posting with { Id = "scout:unit:10:duplicate", Target = "7,7" };

        var result = StrategicScoutActionExecutor.Execute(
            [
                new StrategicJobDecision(posting, true, "accepted"),
                new StrategicJobDecision(duplicate, true, "accepted")
            ],
            Snapshot(Unit(10, 1, 1)), map, MoveCatalog(), terrain, SpatialTopology.Build(terrain));

        Assert.Single(result.Actions);
        Assert.Equal(2, result.Decisions.Count);
        Assert.True(result.Decisions[0].IsAccepted);
        Assert.False(result.Decisions[1].IsAccepted);
    }

    private static StrategicJobPosting ScoutPosting(string id, ulong tag, string target) =>
        new(id, StrategicJobSource.Advisor, nameof(AdvisorDomain.Intelligence),
            StrategicJobKind.Scout, target, 1, 60, 100, 100,
            Constraints: new Dictionary<string, string>
            {
                ["unitTag"] = tag.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

    private static SpatialMap Map() =>
        new(new MapBounds(0, 0, 10, 10), new MapPoint(1, 1), new MapPoint(9, 9));

    private static SpatialTerrain OpenTerrain() =>
        new(new SpatialGridSnapshot(10, 10, 10, 10, Enumerable.Repeat((byte)1, 100).ToArray()), null, null);

    private static Sc2GameDataCatalog MoveCatalog()
    {
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData
        {
            AbilityId = 16,
            LinkName = "move",
            LinkIndex = 0,
            ButtonName = "Move",
            Available = true,
            Target = AbilityData.Types.Target.Point
        });
        return Sc2GameDataCatalog.Create(data);
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Unit(ulong tag, float x, float y) =>
        new(tag, 48, "Marine", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, x, y, true, false, false,
            false, false, false, 100);
}
