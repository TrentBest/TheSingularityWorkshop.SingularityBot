using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ProtossZealotOpeningRuntimeTests
{
    private const uint GatherAbilityId = 777;

    [Fact]
    public void Runtime_AllowsCargoBearingProbeToStartConstructionAndInterruptGatherOrder()
    {
        var runtime = CreateRuntime();
        var opening = Observation(
            300, 0,
            Unit(1, "Nexus"),
            Unit(7, "Probe") with
            {
                HasCargo = true,
                HasOrders = true,
                Orders = new[] { new UnitOrderObservationFacts(GatherAbilityId, null, null, null) }
            });

        Assert.Empty(runtime.OnFrame(opening));
        var query = runtime.TakeQueryRequest();
        Assert.NotNull(query);
        Assert.Equal(48, query.Placements.Count);
        Assert.All(query.Placements, placement => Assert.Equal(7ul, placement.PlacingUnitTag));

        var approved = new ResponseQuery();
        approved.Placements.Add(new ResponseQueryBuildingPlacement { Result = ActionResult.Success });
        runtime.OnQueryResult(approved);

        var build = Assert.Single(runtime.OnFrame(opening));
        Assert.Equal(882, build.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(7ul, Assert.Single(build.ActionRaw.UnitCommand.UnitTags));
        Assert.Equal("PylonSubmitted", runtime.State);
    }

    [Fact]
    public void Runtime_RejectsProbeWithNonGatherOrderBeforePlacementQuery()
    {
        var runtime = CreateRuntime();
        var opening = Observation(
            300, 0,
            Unit(1, "Nexus"),
            Unit(7, "Probe") with
            {
                HasCargo = true,
                HasOrders = true,
                Orders = new[] { new UnitOrderObservationFacts(GatherAbilityId + 1, null, null, null) }
            });

        Assert.Empty(runtime.OnFrame(opening));
        Assert.Null(runtime.TakeQueryRequest());
    }

    private static ProtossZealotOpeningRuntime CreateRuntime()
    {
        var profile = Sc2RaceProductionProfileCatalog.CreateDefault().Profiles
            .Single(item => item.ModeId == ProtossZealotOpener.Id);
        return new ProtossZealotOpeningRuntime(
            new Sc2UnitFacts(84, "Probe", 0, 50, 0, 1, 0),
            new Sc2UnitFacts(60, "Pylon", 882, 100, 0, 0, 0),
            new Sc2UnitFacts(62, "Gateway", 883, 150, 0, 0, 0),
            new Sc2UnitFacts(73, "Zealot", 916, 100, 0, 1, 0),
            Ability(882), Ability(883), profile,
            new HashSet<uint> { GatherAbilityId });
    }

    private static Sc2AbilityFacts Ability(uint id) =>
        new(id, $"Build_{id}", 0, $"Build{id}", $"Build {id}",
            0, true, default, true, false, 1.5f, 0);

    private static NormalizedObservation Observation(uint minerals, uint vespene, params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>())
        {
            Minerals = minerals,
            Vespene = vespene,
            FoodUsed = 10,
            FoodCap = 20
        };

    private static UnitObservationFacts Unit(ulong tag, string kind) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, 1, 20, 20, true,
            false, false, false, false, false, 100);
}
