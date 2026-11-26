using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class UnitBehaviorContextCombatTests
{
    [Fact]
    public void AppliesObservedWeaponCooldownToCombatContext()
    {
        var context = new UnitBehaviorContext(7, "Marine:7", "Marine", false, _ => { });
        var facts = new UnitObservationFacts(
            7, 48, "Marine", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 12, 12, true,
            false, false, false, false, false, 100)
        {
            WeaponCooldownRemaining = 3.5f
        };

        context.Apply(facts);

        Assert.Equal(3.5f, context.WeaponCooldownRemaining);
    }

    [Fact]
    public void DoesNotReplaceLastKnownCooldownWhenUnitIsNotLiveVisible()
    {
        var context = new UnitBehaviorContext(7, "Marine:7", "Marine", false, _ => { });
        var visible = new UnitObservationFacts(
            7, 48, "Marine", 1, UnitAlliance.Self, UnitVisibility.Visible,
            45, 45, 0, 0, 1, 12, 12, true,
            false, false, false, false, false, 100)
        {
            WeaponCooldownRemaining = 2.0f
        };
        context.Apply(visible);

        context.Apply(visible with
        {
            Visibility = UnitVisibility.Snapshot,
            GameLoop = 110,
            WeaponCooldownRemaining = 0
        });

        Assert.Equal(2.0f, context.WeaponCooldownRemaining);
    }
}
