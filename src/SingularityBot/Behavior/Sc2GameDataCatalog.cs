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
        uint? harvestReturnAbilityId)
    {
        UnitNames = unitNames;
        BuffNames = buffNames;
        HarvestGatherAbilityId = harvestGatherAbilityId;
        HarvestReturnAbilityId = harvestReturnAbilityId;
    }

    public IReadOnlyDictionary<uint, string> UnitNames { get; }
    public IReadOnlyDictionary<uint, string> BuffNames { get; }
    public uint? HarvestGatherAbilityId { get; }
    public uint? HarvestReturnAbilityId { get; }

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

        return new Sc2GameDataCatalog(
            unitNames,
            buffNames,
            ResolveAbility(data.Abilities, "Harvest_Gather"),
            ResolveAbility(data.Abilities, "Harvest_Return"));
    }

    private static uint? ResolveAbility(
        IEnumerable<AbilityData> abilities,
        string linkName)
    {
        var match = abilities
            .Where(ability => ability.AbilityId != 0 &&
                              string.Equals(ability.LinkName, linkName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ability => ability.AbilityId)
            .FirstOrDefault();

        return match?.AbilityId;
    }
}
