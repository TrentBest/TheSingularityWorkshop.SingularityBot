using SC2APIProtocol;

namespace SingularityBot.Observation;

/// <summary>
/// Converts generated SC2 protocol messages into deterministic, protocol-independent facts.
/// Game data supplies unit names; numeric unit IDs are never guessed or hard-coded here.
/// </summary>
public static class Sc2ObservationNormalizer
{
    public static NormalizedObservation Normalize(
        ResponseObservation response,
        uint playerId,
        IReadOnlyDictionary<uint, string> unitNames,
        IReadOnlyDictionary<uint, string> buffNames)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(unitNames);
        ArgumentNullException.ThrowIfNull(buffNames);

        var observation = response.Observation
            ?? throw new ArgumentException("The response does not contain an observation.", nameof(response));
        var raw = observation.RawData
            ?? throw new ArgumentException("The observation has no raw unit data. Enable the SC2 raw interface.", nameof(response));

        var units = raw.Units
            .Where(unit => unit.Tag != 0)
            .OrderBy(unit => unit.Tag)
            .Select(unit =>
            {
                unitNames.TryGetValue(unit.UnitType, out var unitKind);
                var position = unit.Pos;
                return new UnitObservationFacts(
                    unit.Tag,
                    unit.UnitType,
                    unitKind ?? string.Empty,
                    unit.Owner,
                    MapAlliance(unit.Alliance),
                    MapVisibility(unit.DisplayType),
                    unit.Health,
                    unit.HealthMax,
                    unit.Shield,
                    unit.ShieldMax,
                    unit.BuildProgress,
                    position?.X ?? 0,
                    position?.Y ?? 0,
                    position is not null,
                    HasResourceCargo(unit, buffNames),
                    unit.Orders.Count > 0,
                    unit.IsFlying,
                    unit.IsBurrowed,
                    unit.IsHallucination,
                    observation.GameLoop)
                {
                    WeaponCooldownRemaining = unit.WeaponCooldown,
                    Orders = unit.Orders.Select(order => new UnitOrderObservationFacts(
                        order.AbilityId,
                        order.TargetCase == UnitOrder.TargetOneofCase.TargetUnitTag ? order.TargetUnitTag : null,
                        order.TargetCase == UnitOrder.TargetOneofCase.TargetWorldSpacePos ? order.TargetWorldSpacePos?.X : null,
                        order.TargetCase == UnitOrder.TargetOneofCase.TargetWorldSpacePos ? order.TargetWorldSpacePos?.Y : null))
                        .ToArray()
                };
            })
            .ToArray();

        var deadTags = raw.Event?.DeadUnits
            .Where(tag => tag != 0)
            .ToHashSet() ?? new HashSet<ulong>();

        return new NormalizedObservation(observation.GameLoop, playerId, units, deadTags)
        {
            Minerals = observation.PlayerCommon?.Minerals ?? 0,
            FoodUsed = observation.PlayerCommon?.FoodUsed ?? 0,
            FoodCap = observation.PlayerCommon?.FoodCap ?? 0
        };
    }

    private static bool HasResourceCargo(Unit unit, IReadOnlyDictionary<uint, string> buffNames) =>
        unit.BuffIds.Any(buffId =>
            buffNames.TryGetValue(buffId, out var name) &&
            (name.Contains("Mineral", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Vespene", StringComparison.OrdinalIgnoreCase)) &&
            (name.Contains("Carry", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Cargo", StringComparison.OrdinalIgnoreCase)));

    private static UnitAlliance MapAlliance(Alliance alliance) => alliance switch
    {
        Alliance.Self => UnitAlliance.Self,
        Alliance.Ally => UnitAlliance.Ally,
        Alliance.Neutral => UnitAlliance.Neutral,
        Alliance.Enemy => UnitAlliance.Enemy,
        _ => UnitAlliance.Unknown
    };

    private static UnitVisibility MapVisibility(DisplayType displayType) => displayType switch
    {
        DisplayType.Visible => UnitVisibility.Visible,
        DisplayType.Snapshot => UnitVisibility.Snapshot,
        DisplayType.Hidden => UnitVisibility.Hidden,
        _ => UnitVisibility.Unknown
    };
}
