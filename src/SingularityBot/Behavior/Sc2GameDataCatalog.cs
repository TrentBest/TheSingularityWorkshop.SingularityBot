using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// Immutable lookup tables derived from the authoritative SC2 game-data response.
/// No unit, buff, or ability identifiers are guessed or hard-coded.
/// </summary>
public sealed class Sc2GameDataCatalog
{
    private Sc2GameDataCatalog(
        IReadOnlyDictionary<uint, string> unitNames,
        IReadOnlyDictionary<uint, string> buffNames,
        IReadOnlyDictionary<uint, Sc2AbilityFacts> abilitiesById,
        IReadOnlyDictionary<uint, IReadOnlyList<Sc2WeaponFacts>> weaponsByUnitType,
        uint? smartAbilityId,
        uint? scvTrainAbilityId,
        uint scvMineralCost,
        float scvFoodRequired,
        uint? moveAbilityId,
        uint? stopAbilityId,
        uint? supplyDepotBuildAbilityId,
        uint supplyDepotMineralCost,
        uint? barracksBuildAbilityId,
        uint barracksMineralCost,
        uint? marineTrainAbilityId,
        uint marineMineralCost,
        float marineFoodRequired,
        uint? sprayAbilityId,
        uint? rallyWorkersAbilityId)
    {
        UnitNames = unitNames;
        BuffNames = buffNames;
        AbilitiesById = abilitiesById;
        WeaponsByUnitType = weaponsByUnitType;
        SmartAbilityId = smartAbilityId;
        ScvTrainAbilityId = scvTrainAbilityId;
        ScvMineralCost = scvMineralCost;
        ScvFoodRequired = scvFoodRequired;
        MoveAbilityId = moveAbilityId;
        StopAbilityId = stopAbilityId;
        SupplyDepotBuildAbilityId = supplyDepotBuildAbilityId;
        SupplyDepotMineralCost = supplyDepotMineralCost;
        BarracksBuildAbilityId = barracksBuildAbilityId;
        BarracksMineralCost = barracksMineralCost;
        MarineTrainAbilityId = marineTrainAbilityId;
        MarineMineralCost = marineMineralCost;
        MarineFoodRequired = marineFoodRequired;
        SprayAbilityId = sprayAbilityId;
        RallyWorkersAbilityId = rallyWorkersAbilityId;
    }

    public IReadOnlyDictionary<uint, string> UnitNames { get; }
    public IReadOnlyDictionary<uint, string> BuffNames { get; }
    /// <summary>All non-zero abilities supplied by the match's authoritative ResponseData.</summary>
    public IReadOnlyDictionary<uint, Sc2AbilityFacts> AbilitiesById { get; }
    public IReadOnlyDictionary<uint, IReadOnlyList<Sc2WeaponFacts>> WeaponsByUnitType { get; }
    public bool TryGetAbility(uint abilityId, out Sc2AbilityFacts ability) =>
        AbilitiesById.TryGetValue(abilityId, out ability);
    public IReadOnlyList<Sc2AbilityFacts> FindAbilities(string name) =>
        AbilitiesById.Values
            .Where(ability =>
                string.Equals(ability.LinkName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ability.ButtonName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ability.FriendlyName, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ability => ability.AbilityId)
            .ToArray();
    public uint? SmartAbilityId { get; }
    public uint? ScvTrainAbilityId { get; }
    public uint ScvMineralCost { get; }
    public float ScvFoodRequired { get; }
    public uint? MoveAbilityId { get; }
    public uint? StopAbilityId { get; }
    public uint? SupplyDepotBuildAbilityId { get; }
    public uint SupplyDepotMineralCost { get; }
    public uint? BarracksBuildAbilityId { get; }
    public uint BarracksMineralCost { get; }
    public uint? MarineTrainAbilityId { get; }
    public uint MarineMineralCost { get; }
    public float MarineFoodRequired { get; }
    public uint? SprayAbilityId { get; }
    public uint? RallyWorkersAbilityId { get; }

    public static Sc2GameDataCatalog Create(ResponseData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var unitNames = data.Units
            .Where(unit => unit.UnitId != 0 && !string.IsNullOrWhiteSpace(unit.Name))
            .GroupBy(unit => unit.UnitId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var weaponsByUnitType = data.Units
            .Where(unit => unit.UnitId != 0 && unit.Weapons.Count > 0)
            .GroupBy(unit => unit.UnitId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Sc2WeaponFacts>)group.First().Weapons
                    .Where(weapon => weapon.Range > 0 && weapon.Speed > 0 && weapon.Damage > 0 && weapon.Attacks > 0)
                    .Select(weapon => new Sc2WeaponFacts(
                        weapon.Type,
                        weapon.Range,
                        weapon.Damage,
                        weapon.Attacks,
                        weapon.Speed))
                    .ToArray());

        var buffNames = data.Buffs
            .Where(buff => buff.BuffId != 0 && !string.IsNullOrWhiteSpace(buff.Name))
            .GroupBy(buff => buff.BuffId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var abilitiesById = data.Abilities
            .Where(ability => ability.AbilityId != 0)
            .GroupBy(ability => ability.AbilityId)
            .ToDictionary(group => group.Key, group => Sc2AbilityFacts.From(group.First()));

        var scv = data.Units.FirstOrDefault(unit =>
            string.Equals(unit.Name, "SCV", StringComparison.OrdinalIgnoreCase));
        var supplyDepot = data.Units.FirstOrDefault(unit =>
            string.Equals(unit.Name, "SupplyDepot", StringComparison.OrdinalIgnoreCase));
        var barracks = data.Units.FirstOrDefault(unit =>
            string.Equals(unit.Name, "Barracks", StringComparison.OrdinalIgnoreCase));
        var marine = data.Units.FirstOrDefault(unit =>
            string.Equals(unit.Name, "Marine", StringComparison.OrdinalIgnoreCase));

        return new Sc2GameDataCatalog(
            unitNames,
            buffNames,
            abilitiesById,
            weaponsByUnitType,
            ResolveSmartAbility(data.Abilities),
            scv?.AbilityId is > 0 ? scv.AbilityId : null,
            scv?.MineralCost ?? 0,
            scv?.FoodRequired ?? 0,
            ResolveMoveAbility(data.Abilities),
            ResolveStopAbility(data.Abilities),
            supplyDepot?.AbilityId is > 0 ? supplyDepot.AbilityId : null,
            supplyDepot?.MineralCost ?? 0,
            barracks?.AbilityId is > 0 ? barracks.AbilityId : null,
            barracks?.MineralCost ?? 0,
            marine?.AbilityId is > 0 ? marine.AbilityId : null,
            marine?.MineralCost ?? 0,
            marine?.FoodRequired ?? 0,
            ResolveSprayAbility(data.Abilities),
            ResolveAbility(data.Abilities, "Rally_Workers"));
    }


    private static uint? ResolveSmartAbility(IEnumerable<AbilityData> abilities)
    {
        var candidates = abilities
            .Where(ability =>
                string.Equals(ability.LinkName, "Smart", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ability.ButtonName, "Smart", StringComparison.OrdinalIgnoreCase))
            .Select(ability => ability.AbilityId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static uint? ResolveSprayAbility(IEnumerable<AbilityData> abilities)
    {
        var candidates = abilities
            .Where(ability =>
                string.Equals(ability.LinkName, "Spray", StringComparison.OrdinalIgnoreCase) ||
                ability.LinkName.EndsWith("_Spray", StringComparison.OrdinalIgnoreCase))
            .Select(ability => ability.AbilityId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static uint? ResolveMoveAbility(IEnumerable<AbilityData> abilities)
    {
        // SC2 exposes several lowercase "move" variants (patrol, hold-position,
        // acquire-move, and turn). LinkIndex=0 identifies the generic player command.
        var genericMove = abilities
            .Where(ability => ability.AbilityId != 0 &&
                string.Equals(ability.LinkName, "move", StringComparison.OrdinalIgnoreCase) &&
                ability.LinkIndex == 0)
            .Select(ability => ability.AbilityId)
            .Distinct()
            .ToArray();

        if (genericMove.Length == 1)
            return genericMove[0];

        return ResolveAbility(abilities, "GeneralMove", "Move_Move");
    }

    private static uint? ResolveStopAbility(IEnumerable<AbilityData> abilities)
    {
        // SC2 exposes several lowercase "stop" button variants (including
        // emotes and a unit-targeted variant). The generic Stop command is
        // the distinct, case-sensitive LinkName "Stop" that remaps none.
        var candidates = abilities
            .Where(ability => ability.AbilityId != 0 &&
                string.Equals(ability.LinkName, "Stop", StringComparison.Ordinal))
            .Select(ability => ability.AbilityId)
            .Distinct()
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static uint? ResolveAbility(
        IEnumerable<AbilityData> abilities,
        params string[] linkNames)
    {
        var names = new HashSet<string>(linkNames, StringComparer.OrdinalIgnoreCase);
        var candidates = abilities
            .Where(ability => ability.AbilityId != 0 && names.Contains(ability.LinkName))
            .Select(ability => ability.AbilityId)
            .Distinct()
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }
}


/// <summary>
/// Immutable subset of SC2 AbilityData needed to validate and construct raw
/// commands. Values originate only from ResponseData supplied to OnStart.
/// </summary>
public sealed record Sc2AbilityFacts(
    uint AbilityId,
    string LinkName,
    uint LinkIndex,
    string ButtonName,
    string FriendlyName,
    uint RemapsToAbilityId,
    bool Available,
    AbilityData.Types.Target Target,
    bool IsBuilding,
    bool IsInstantPlacement,
    float FootprintRadius,
    float CastRange)
{
    internal static Sc2AbilityFacts From(AbilityData ability) => new(
        ability.AbilityId,
        ability.LinkName ?? string.Empty,
        ability.LinkIndex,
        ability.ButtonName ?? string.Empty,
        ability.FriendlyName ?? string.Empty,
        ability.RemapsToAbilityId,
        ability.Available,
        ability.Target,
        ability.IsBuilding,
        ability.IsInstantPlacement,
        ability.FootprintRadius,
        ability.CastRange);
}


/// <summary>Weapon facts copied directly from SC2 ResponseData.</summary>
public sealed record Sc2WeaponFacts(
    Weapon.Types.TargetType TargetType,
    float Range,
    float Damage,
    uint Attacks,
    float AttackPeriodSeconds)
{
    public float DamagePerSecond => Damage * Attacks / AttackPeriodSeconds;
}
