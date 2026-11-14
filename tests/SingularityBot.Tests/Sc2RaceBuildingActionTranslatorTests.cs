using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2RaceBuildingActionTranslatorTests
{
    [Fact]
    public void Translation_UsesAuthoritativeBuildAbilityAndVerifiedPlacement()
    {
        var structure = new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0);
        var workerType = new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0);
        var ability = Ability(1344, isBuilding: true);
        var observation = Observation(200, 0, Unit(7, "Drone"));

        var result = Sc2RaceBuildingActionTranslator.Translate(
            structure, workerType, ability, observation,
            workerTag: 7, placementX: 22.5f, placementY: 31.5f,
            unavailableWorkerTags: new HashSet<ulong>());

        Assert.False(result.IsBlocked);
        Assert.Single(result.Actions);
        Assert.Equal(7ul, result.WorkerTag);
        Assert.Equal(200u, result.ReservedMinerals);
        Assert.Equal(1344, result.Actions[0].ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(7ul, Assert.Single(result.Actions[0].ActionRaw.UnitCommand.UnitTags));
        Assert.Equal(22.5f, result.Actions[0].ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(31.5f, result.Actions[0].ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
        Assert.False(result.Actions[0].ActionRaw.UnitCommand.QueueCommand);
    }

    [Fact]
    public void Translation_FailsClosedWhenBuildAbilityDoesNotMatchStructureGameData()
    {
        var result = Sc2RaceBuildingActionTranslator.Translate(
            new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0),
            new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0),
            Ability(999, isBuilding: true),
            Observation(500, 0, Unit(7, "Drone")),
            7, 22, 31, new HashSet<ulong>());

        Assert.True(result.IsBlocked);
        Assert.Empty(result.Actions);
        Assert.Contains("mismatched", result.BlockReason);
    }

    [Fact]
    public void Translation_FailsClosedForBusyReservedOrWrongRaceWorker()
    {
        var structure = new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0);
        var workerType = new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0);
        var ability = Ability(1344, isBuilding: true);

        var busy = Sc2RaceBuildingActionTranslator.Translate(
            structure, workerType, ability, Observation(500, 0, Unit(7, "Drone", hasOrders: true)),
            7, 22, 31, new HashSet<ulong>());
        var reserved = Sc2RaceBuildingActionTranslator.Translate(
            structure, workerType, ability, Observation(500, 0, Unit(7, "Drone")),
            7, 22, 31, new HashSet<ulong> { 7 });
        var wrongWorker = Sc2RaceBuildingActionTranslator.Translate(
            structure, workerType, ability, Observation(500, 0, Unit(7, "SCV")),
            7, 22, 31, new HashSet<ulong>());

        Assert.True(busy.IsBlocked);
        Assert.True(reserved.IsBlocked);
        Assert.True(wrongWorker.IsBlocked);
    }

    [Fact]
    public void Translation_AllowsOnlyExplicitlyWhitelistedGatherOrdersToBeInterrupted()
    {
        var worker = Unit(8, "Drone", hasOrders: true) with
        {
            Orders = new[] { new UnitOrderObservationFacts(777, null, null, null) }
        };
        var result = Sc2RaceBuildingActionTranslator.Translate(
            new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0),
            new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0),
            Ability(1344, isBuilding: true),
            Observation(500, 0, worker),
            8, 22, 31, new HashSet<ulong>(),
            new HashSet<uint> { 777 });

        Assert.False(result.IsBlocked);
        Assert.Equal(8ul, result.WorkerTag);
    }

    [Fact]
    public void Translation_RejectsUnrecognizedOrdersEvenWhenGatherOrdersAreAllowed()
    {
        var worker = Unit(8, "Drone", hasOrders: true) with
        {
            Orders = new[] { new UnitOrderObservationFacts(778, null, null, null) }
        };
        var result = Sc2RaceBuildingActionTranslator.Translate(
            new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0),
            new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0),
            Ability(1344, isBuilding: true),
            Observation(500, 0, worker),
            8, 22, 31, new HashSet<ulong>(),
            new HashSet<uint> { 777 });

        Assert.True(result.IsBlocked);
    }

    [Fact]
    public void Translation_FailsClosedWhenResourcesCannotCoverStructure()
    {
        var result = Sc2RaceBuildingActionTranslator.Translate(
            new Sc2UnitFacts(200, "SpawningPool", 1344, 200, 0, 0, 0),
            new Sc2UnitFacts(104, "Drone", 0, 50, 0, 1, 0),
            Ability(1344, isBuilding: true),
            Observation(199, 0, Unit(7, "Drone")),
            7, 22, 31, new HashSet<ulong>());

        Assert.True(result.IsBlocked);
        Assert.Contains("resources", result.BlockReason);
    }

    private static Sc2AbilityFacts Ability(uint id, bool isBuilding) =>
        new(id, "Build_SpawningPool", 0, "BuildSpawningPool", "Build Spawning Pool",
            0, true, default, isBuilding, false, 1.5f, 0);

    private static NormalizedObservation Observation(
        uint minerals, uint vespene, params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>())
        {
            Minerals = minerals,
            Vespene = vespene,
            FoodUsed = 10,
            FoodCap = 20
        };

    private static UnitObservationFacts Unit(ulong tag, string kind, bool hasOrders = false) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, 1, 10, 10, true,
            false, hasOrders, false, false, false, 100);
}
