using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class MarineCombatPlannerTests
{
    private static readonly IReadOnlyDictionary<uint, CombatWeaponProfile> Weapons =
        new Dictionary<uint, CombatWeaponProfile>
        {
            [48] = new(5, 9),
            [MarineCombatPlannerTests.EnemyType] = new(6, 0),
            [MarineCombatPlannerTests.ArmedEnemyType] = new(6, 12)
        };

    private const uint EnemyType = 100;
    private const uint ArmedEnemyType = 101;

    [Fact]
    public void FiresImmediatelyWhenReadyAndInsideAuthoritativeRange()
    {
        var result = Plan(Marine(1, 0, 0, 0), Enemy(2, 4, 0));
        var decision = Assert.Single(result);
        Assert.Equal(MarineTacticalAction.Fire, decision.Action);
        Assert.Equal(2UL, decision.TargetTag);
    }

    [Fact]
    public void RepositionsDuringCooldownInsteadOfWaitingToFire()
    {
        var result = Plan(Marine(1, 0, 0, 3), Enemy(2, 4, 0));
        Assert.Equal(MarineTacticalAction.RepositionDuringCooldown, Assert.Single(result).Action);
    }

    [Fact]
    public void ApproachesTargetsOutsideWeaponRange()
    {
        var result = Plan(Marine(1, 0, 0, 0), Enemy(2, 8, 0));
        Assert.Equal(MarineTacticalAction.CloseToWeaponRange, Assert.Single(result).Action);
    }

    [Fact]
    public void WithdrawsWhenLocalThreatOutweighsFriendlyDamage()
    {
        var result = Plan(Marine(1, 0, 0, 0), ArmedEnemy(2, 4, 0), ArmedEnemy(3, 5, 1));
        Assert.Equal(MarineTacticalAction.Withdraw, Assert.Single(result).Action);
    }

    [Fact]
    public void IgnoresMineralFieldsAndUnclassifiedNeutralUnits()
    {
        var result = Plan(Marine(1, 0, 0, 0),
            Fact(2, 341, "MineralField750", UnitAlliance.Neutral, 1, 0),
            Fact(3, 999, "UnknownNeutral", UnitAlliance.Neutral, 2, 0));
        Assert.Equal(MarineTacticalAction.Hold, Assert.Single(result).Action);
    }

    [Fact]
    public void AllowsExplicitlyClassifiedDestructibleNeutrals()
    {
        var result = Plan(Marine(1, 0, 0, 0),
            Fact(2, EnemyType, "DestructibleRock", UnitAlliance.Neutral, 4, 0),
            destructibles: new HashSet<ulong> { 2 });
        Assert.Equal(2UL, Assert.Single(result).TargetTag);
    }

    [Fact]
    public void FailsClosedWhenMarineWeaponDataIsMissing()
    {
        var result = MarineCombatPlanner.Plan(
            Snapshot(Marine(1, 0, 0, 0), Enemy(2, 4, 0)),
            new Dictionary<uint, CombatWeaponProfile>());
        Assert.Equal(MarineTacticalAction.Hold, Assert.Single(result).Action);
    }

    private static IReadOnlyList<MarineTacticalDecision> Plan(
        UnitObservationFacts marine,
        UnitObservationFacts enemy,
        UnitObservationFacts? secondEnemy = null,
        IReadOnlySet<ulong>? destructibles = null)
    {
        var units = secondEnemy is null ? new[] { marine, enemy } : new[] { marine, enemy, secondEnemy };
        return MarineCombatPlanner.Plan(Snapshot(units), Weapons, destructibles);
    }

    private static NormalizedObservation Snapshot(params UnitObservationFacts[] units) =>
        new(100, 1, units, new HashSet<ulong>());

    private static UnitObservationFacts Marine(ulong tag, float x, float y, float cooldown) =>
        Fact(tag, 48, "Marine", UnitAlliance.Self, x, y, cooldown);

    private static UnitObservationFacts Enemy(ulong tag, float x, float y) =>
        Fact(tag, EnemyType, "EnemyStructure", UnitAlliance.Enemy, x, y);

    private static UnitObservationFacts ArmedEnemy(ulong tag, float x, float y) =>
        Fact(tag, ArmedEnemyType, "EnemyCombatUnit", UnitAlliance.Enemy, x, y);

    private static UnitObservationFacts Fact(
        ulong tag, uint type, string kind, UnitAlliance alliance, float x, float y, float cooldown = 0) =>
        new(tag, type, kind, alliance == UnitAlliance.Self ? 1 : 2, alliance,
            UnitVisibility.Visible, 45, 45, 0, 0, 1, x, y, true,
            false, false, false, false, false, 100)
        { WeaponCooldownRemaining = cooldown };
}
