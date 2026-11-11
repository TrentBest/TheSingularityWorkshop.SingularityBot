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
            [MarineCombatPlannerTests.ArmedEnemyType] = new(6, 9)
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
    public void HoldsInsteadOfChasingAContactAcrossTheMap()
    {
        var result = Plan(Marine(1, 0, 0, 0), Enemy(2, 30, 0));
        Assert.Equal(MarineTacticalAction.Hold, Assert.Single(result).Action);
    }

    [Fact]
    public void StutterStepsAgainstOneEqualStrengthMarineInsteadOfAutomaticallyRetreating()
    {
        var result = Plan(Marine(1, 0, 0, 3), ArmedEnemy(2, 4, 0));
        Assert.Equal(MarineTacticalAction.RepositionDuringCooldown, Assert.Single(result).Action);
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



    [Fact]
    public void MarinesShareOneFocusTargetAndPrioritizeThreatsOverNeutralBlockers()
    {
        var observation = Snapshot(
            Marine(1, 0, 0, 0),
            Marine(4, 1, 0, 0),
            Enemy(2, 3, 0),
            ArmedEnemy(3, 5, 0),
            Fact(5, EnemyType, "DestructibleRock", UnitAlliance.Neutral, 1, 1));
        var decisions = MarineCombatPlanner.Plan(observation, Weapons);

        Assert.Equal(2, decisions.Count);
        Assert.All(decisions, decision => Assert.Equal(3UL, decision.TargetTag));
    }

    [Fact]
    public void TranslatorEmitsAttackAgainstCurrentVisibleTarget()
    {
        var catalog = CombatCatalog();
        var observation = Snapshot(Marine(1, 0, 0, 0), Enemy(2, 4, 0));
        var decisions = MarineCombatPlanner.Plan(observation, CombatWeaponProfile.FromCatalog(catalog));

        var action = Assert.Single(Sc2MarineCombatActionTranslator.Translate(
            decisions, observation, catalog, CombatWeaponProfile.FromCatalog(catalog)));
        var command = action.ActionRaw.UnitCommand;
        Assert.Equal(23, command.AbilityId);
        Assert.Equal(2UL, command.TargetUnitTag);
        Assert.Contains(1UL, command.UnitTags);
    }


    [Fact]
    public void TranslatorDoesNotResubmitAnExistingAttackOrder()
    {
        var catalog = CombatCatalog();
        var marine = Marine(1, 0, 0, 0) with
        {
            Orders = new[] { new UnitOrderObservationFacts(23, 2, null, null) }
        };
        var observation = Snapshot(marine, Enemy(2, 4, 0));
        var decisions = MarineCombatPlanner.Plan(observation, CombatWeaponProfile.FromCatalog(catalog));

        Assert.Empty(Sc2MarineCombatActionTranslator.Translate(
            decisions, observation, catalog, CombatWeaponProfile.FromCatalog(catalog)));
    }

    [Fact]
    public void TranslatorRejectsFriendlyFireEvenWhenGivenMalformedDecision()
    {
        var catalog = CombatCatalog();
        var marine = Marine(1, 0, 0, 0);
        var friendly = Marine(2, 4, 0, 0);
        var observation = Snapshot(marine, friendly);
        var decision = new MarineTacticalDecision(
            1, 2, MarineTacticalAction.Fire, 6, 0, "malformed target");

        Assert.Empty(Sc2MarineCombatActionTranslator.Translate(
            new[] { decision }, observation, catalog,
            CombatWeaponProfile.FromCatalog(catalog)));
    }

    [Fact]
    public void TranslatorRejectsFireDecisionOutsideWeaponRange()
    {
        var catalog = CombatCatalog();
        var observation = Snapshot(Marine(1, 0, 0, 0), Enemy(2, 8, 0));
        var decision = new MarineTacticalDecision(
            1, 2, MarineTacticalAction.Fire, 6, 0, "stale range");

        Assert.Empty(Sc2MarineCombatActionTranslator.Translate(
            new[] { decision }, observation, catalog,
            CombatWeaponProfile.FromCatalog(catalog)));
    }

    [Fact]
    public void TranslatorMovesMarineDuringWeaponCooldown()
    {
        var catalog = CombatCatalog();
        var observation = Snapshot(Marine(1, 0, 0, 3), Enemy(2, 4, 0));
        var decisions = MarineCombatPlanner.Plan(observation, CombatWeaponProfile.FromCatalog(catalog));

        var action = Assert.Single(Sc2MarineCombatActionTranslator.Translate(
            decisions, observation, catalog, CombatWeaponProfile.FromCatalog(catalog)));
        var command = action.ActionRaw.UnitCommand;
        Assert.Equal(16, command.AbilityId);
        Assert.NotNull(command.TargetWorldSpacePos);
    }

    [Fact]
    public void CooldownKitingDoesNotDriftFurtherAwayAfterLeavingEnemyRange()
    {
        var catalog = CombatCatalog();
        var observation = Snapshot(
            Marine(1, 0, 0, 3),
            ArmedEnemy(3, 8, 0));
        var decisions = MarineCombatPlanner.Plan(
            observation, CombatWeaponProfile.FromCatalog(catalog));

        var action = Assert.Single(Sc2MarineCombatActionTranslator.Translate(
            decisions, observation, catalog,
            CombatWeaponProfile.FromCatalog(catalog)));

        Assert.Equal(0f, action.ActionRaw.UnitCommand.TargetWorldSpacePos.X);
        Assert.NotEqual(0f, action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y);
    }

    [Fact]
    public void WithdrawsAwayFromThreatEvenWhenSupportersAreOnEnemyFacingSide()
    {
        var catalog = CombatCatalog();
        var observation = Snapshot(
            Marine(1, 0, 0, 0),
            Marine(2, 10, 0, 0),
            ArmedEnemy(3, 1, 0));
        var decision = new MarineTacticalDecision(
            1, 3, MarineTacticalAction.Withdraw, 0, 9, "retreat regression");

        var action = Assert.Single(Sc2MarineCombatActionTranslator.Translate(
            new[] { decision }, observation, catalog,
            CombatWeaponProfile.FromCatalog(catalog)));

        Assert.True(action.ActionRaw.UnitCommand.TargetWorldSpacePos.X < 0,
            "Retreat destination must move away from the enemy, not toward the supporter centroid.");
    }

    [Fact]
    public void CatalogRetainsAuthoritativeWeaponEnvelopeAndAttackAbility()
    {
        var catalog = CombatCatalog();
        Assert.Equal((uint)23, catalog.AttackAbilityId);
        var weapon = Assert.Single(catalog.WeaponsByUnitType[48]);
        Assert.Equal(SC2APIProtocol.Weapon.Types.TargetType.Any, weapon.TargetType);
        Assert.Equal(5f, weapon.Range);
        Assert.Equal(6f, weapon.Damage);
        Assert.Equal(1u, weapon.Attacks);
        Assert.Equal(1f, weapon.AttackPeriodSeconds);
    }

    private static Sc2GameDataCatalog CombatCatalog()
    {
        var data = new SC2APIProtocol.ResponseData();
        data.Units.Add(new SC2APIProtocol.UnitTypeData
        {
            UnitId = 48,
            Name = "Marine",
            Weapons = { new SC2APIProtocol.Weapon
            {
                Type = SC2APIProtocol.Weapon.Types.TargetType.Any,
                Range = 5,
                Damage = 6,
                Attacks = 1,
                Speed = 1
            } }
        });
        data.Abilities.Add(new SC2APIProtocol.AbilityData
        {
            AbilityId = 23,
            LinkName = "Attack",
            LinkIndex = 0,
            Target = SC2APIProtocol.AbilityData.Types.Target.Unit
        });
        data.Abilities.Add(new SC2APIProtocol.AbilityData
        {
            AbilityId = 16,
            LinkName = "move",
            LinkIndex = 0,
            ButtonName = "Move"
        });
        return Sc2GameDataCatalog.Create(data);
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
