using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2MarineFsmActionTranslatorTests
{
    [Fact]
    public void TranslatesScoutSectorFsmIntentIntoSectorMove()
    {
        var context = new UnitBehaviorContext(1, "Marine:1", "Marine", false, _ => { })
        {
            AssignedSector = new SingularityBot.Spatial.SectorId(0, 0),
            SectorCenterX = 18,
            SectorCenterY = 22
        };
        context.Apply(Fact());
        var data = new ResponseData();
        data.Abilities.Add(new AbilityData { AbilityId = 16, LinkName = "Move" });
        var actions = Sc2MarineFsmActionTranslator.Translate(
            new[] { new KeyValuePair<ulong, BehaviorCommand>(1, BehaviorCommand.ScoutSector) },
            new[] { context }, Snapshot(), Sc2GameDataCatalog.Create(data));

        var action = Assert.Single(actions);
        Assert.Equal(16, action.ActionRaw.UnitCommand.AbilityId);
        Assert.Equal(1UL, Assert.Single(action.ActionRaw.UnitCommand.UnitTags));
        Assert.Equal(18, action.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.Equal(22, action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
    }

    private static NormalizedObservation Snapshot() => new(100, 1, new[] { Fact() }, new HashSet<ulong>());
    private static UnitObservationFacts Fact() => new(1, 1, "Marine", 48, UnitAlliance.Self,
        UnitVisibility.Visible, 45, 45, 0, 0, 1, 10, 10, true, false, false,
        false, false, false, 100);
}
