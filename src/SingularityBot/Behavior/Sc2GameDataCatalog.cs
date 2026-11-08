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
        uint? harvestGatherAbilityId,
        uint? harvestReturnAbilityId,
        uint? scvTrainAbilityId,
        uint scvMineralCost,
        float scvFoodRequired,
        uint? moveAbilityId,
        uint? supplyDepotBuildAbilityId,
        uint supplyDepotMineralCost,
        uint? sprayAbilityId)
    {
        UnitNames = unitNames;
        BuffNames = buffNames;
        HarvestGatherAbilityId = harvestGatherAbilityId;
        HarvestReturnAbilityId = harvestReturnAbilityId;
        ScvTrainAbilityId = scvTrainAbilityId;
        ScvMineralCost = scvMineralCost;
        ScvFoodRequired = scvFoodRequired;
        MoveAbilityId = moveAbilityId;
        SupplyDepotBuildAbilityId = supplyDepotBuildAbilityId;
        SupplyDepotMineralCost = supplyDepotMineralCost;
        SprayAbilityId = sprayAbilityId;
    }

    public IReadOnlyDictionary<uint, string> UnitNames { get; }
    public IReadOnlyDictionary<uint, string> BuffNames { get; }
    public uint? HarvestGatherAbilityId { get; }
    public uint? HarvestReturnAbilityId { get; }
    public uint? ScvTrainAbilityId { get; }
    public uint ScvMineralCost { get; }
    public float ScvFoodRequired { get; }
    public uint? MoveAbilityId { get; }
    public uint? SupplyDepotBuildAbilityId { get; }
    public uint SupplyDepotMineralCost { get; }
    public uint? SprayAbilityId { get; }

    public static Sc2GameDataCatalog Create(ResponseData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var unitNames = data.Units
            .Where(unit => unit.UnitId != 0 && !string.IsNullOrWhiteSpace(unit.Name))
            .GroupBy(unit => unit.UnitId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var buffNames = data.Buffs
            .Where(buff => buff.BuffId != 0 && !string.IsNullOrWhiteSpace(buff.Name))
            .GroupBy(buff => buff.BuffId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var scv = data.Units.FirstOrDefault(unit =>
            string.Equals(unit.Name, "SCV", StringComparison.OrdinalIgnoreCase));
        var supplyDepot = data.Units.FirstOrDefault(unit =>
            string.Equals(unit.Name, "SupplyDepot", StringComparison.OrdinalIgnoreCase));

        return new Sc2GameDataCatalog(
            unitNames,
            buffNames,
            ResolveAbility(data.Abilities, "HarvestGather", "Harvest_Gather"),
            ResolveAbility(data.Abilities, "HarvestReturn", "Harvest_Return"),
            scv?.AbilityId is > 0 ? scv.AbilityId : null,
            scv?.MineralCost ?? 0,
            scv?.FoodRequired ?? 0,
            ResolveAbility(data.Abilities, "Move") ?? ResolveAbility(data.Abilities, "Move_Move"),
            supplyDepot?.AbilityId is > 0 ? supplyDepot.AbilityId : null,
            supplyDepot?.MineralCost ?? 0,
            ResolveSprayAbility(data.Abilities));
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
