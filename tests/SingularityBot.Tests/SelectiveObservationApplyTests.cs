using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class SelectiveObservationApplyTests
{
    [Fact]
    public void OmittedFieldsDoNotErasePersistentLiveFacts()
    {
        var context = new UnitBehaviorContext(42, "unit-42", "Marine", false, _ => { });
        context.History.EnabledFields =
            UnitObservationHistoryFields.Health |
            UnitObservationHistoryFields.Position |
            UnitObservationHistoryFields.WeaponCooldown;

        context.Apply(CreateFacts(
            health: 45,
            healthMax: 50,
            positionX: 12,
            positionY: 8,
            cooldown: 3,
            captured: ObservationFields.All));

        context.Apply(CreateFacts(
            health: 0,
            healthMax: 50,
            positionX: 0,
            positionY: 0,
            cooldown: 0,
            captured: ObservationFields.None,
            gameLoop: 2));

        Assert.Equal(45, context.Health);
        Assert.Equal(50, context.HealthMax);
        Assert.Equal(12, context.PositionX);
        Assert.Equal(8, context.PositionY);
        Assert.Equal(3, context.WeaponCooldownRemaining);
        Assert.Equal(0, context.History.HealthDelta);
        Assert.Equal(0, context.History.PositionDeltaX);
        Assert.Equal(0, context.History.WeaponCooldownDelta);
    }

    [Fact]
    public void CapturedOrderPresenceDoesNotErasePreviouslyCapturedOrderDetails()
    {
        var context = new UnitBehaviorContext(42, "unit-42", "SCV", true, _ => { });
        var initial = CreateFacts(20, 20, 1, 2, 0, ObservationFields.All) with
        {
            HasOrders = true,
            Orders = [new UnitOrderObservationFacts(7, 99, null, null)]
        };
        context.Apply(initial);

        var next = CreateFacts(20, 20, 3, 4, 0, ObservationFields.HasOrders) with
        {
            HasOrders = false
        };
        context.Apply(next);

        Assert.False(context.HasAssignedWork);
        Assert.Equal((ulong)99, context.CurrentOrder?.TargetUnitTag);
    }

    private static UnitObservationFacts CreateFacts(
        float health,
        float healthMax,
        float positionX,
        float positionY,
        float cooldown,
        ObservationFields captured,
        uint gameLoop = 1) =>
        new(
            42, 1, "Marine", 1, UnitAlliance.Self, UnitVisibility.Visible,
            health, healthMax, 0, 0, 1,
            positionX, positionY, (captured & ObservationFields.Position) != 0,
            false, false, false, false, false, gameLoop)
        {
            WeaponCooldownRemaining = cooldown,
            CapturedFields = captured
        };
}
