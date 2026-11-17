using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class UnitObservationHistoryTests
{
    [Fact]
    public void TracksOnlyEnabledFieldsAndComputesDeltasFromPreviousLiveSample()
    {
        var context = new UnitBehaviorContext(7, "Marine:7", "Marine", false, _ => { });
        context.History.EnabledFields = UnitObservationHistoryFields.Health |
                                        UnitObservationHistoryFields.Position;

        context.Apply(Facts(health: 40, x: 10, y: 20, loop: 1));
        Assert.False(context.History.HasPreviousHealth);
        Assert.False(context.History.HasPreviousPosition);

        context.Apply(Facts(health: 35, x: 13, y: 18, loop: 2));
        Assert.True(context.History.HasPreviousHealth);
        Assert.Equal(40, context.History.PreviousHealth);
        Assert.Equal(-5, context.History.HealthDelta);
        Assert.Equal(3, context.History.PositionDeltaX);
        Assert.Equal(-2, context.History.PositionDeltaY);
        Assert.False(context.History.HasPreviousWeaponCooldown);
    }

    [Fact]
    public void FogOfWarSnapshotDoesNotAdvanceLiveObservationHistory()
    {
        var context = new UnitBehaviorContext(7, "Marine:7", "Marine", false, _ => { });
        context.History.EnabledFields = UnitObservationHistoryFields.Health;
        context.Apply(Facts(40, 10, 20, 1));
        context.Apply(Facts(35, 11, 20, 2));
        var previous = context.History.PreviousHealth;
        var delta = context.History.HealthDelta;

        context.Apply(Facts(1, 99, 99, 3) with { Visibility = UnitVisibility.Snapshot });

        Assert.Equal(previous, context.History.PreviousHealth);
        Assert.Equal(delta, context.History.HealthDelta);
    }

    private static UnitObservationFacts Facts(float health, float x, float y, uint loop) =>
        new(7, 48, "Marine", 1, UnitAlliance.Self, UnitVisibility.Visible,
            health, 45, 0, 0, 1, x, y, true,
            false, false, false, false, false, loop);
}
