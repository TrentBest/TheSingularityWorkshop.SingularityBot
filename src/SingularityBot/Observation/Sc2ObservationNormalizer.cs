using SC2APIProtocol;

namespace SingularityBot.Observation;

/// <summary>
/// Converts generated SC2 protocol messages into deterministic, protocol-independent facts.
/// Game data supplies unit names; numeric unit IDs are never guessed or hard-coded here.
/// </summary>
public static class Sc2ObservationNormalizer
{
    /// <summary>Compatibility overload: captures every optional field.</summary>
    public static NormalizedObservation Normalize(
        ResponseObservation response,
        uint playerId,
        IReadOnlyDictionary<uint, string> unitNames,
        IReadOnlyDictionary<uint, string> buffNames) =>
        Normalize(response, playerId, unitNames, buffNames, ObservationRequirements.All);

    public static NormalizedObservation Normalize(
        ResponseObservation response,
        uint playerId,
        IReadOnlyDictionary<uint, string> unitNames,
        IReadOnlyDictionary<uint, string> buffNames,
        ObservationRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(unitNames);
        ArgumentNullException.ThrowIfNull(buffNames);

        var fields = requirements.Fields;
        bool Has(ObservationFields value) => (fields & value) == value;

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
                var captureHealth = Has(ObservationFields.Health);
                var captureShields = Has(ObservationFields.Shields);
                var capturePosition = Has(ObservationFields.Position);
                var captureCargo = Has(ObservationFields.Cargo);
                var captureOrderDetails = Has(ObservationFields.OrderDetails);
                var position = capturePosition ? unit.Pos : null;
                var orders = captureOrderDetails
                    ? unit.Orders.Select(order => new UnitOrderObservationFacts(
                        order.AbilityId,
                        order.TargetCase == UnitOrder.TargetOneofCase.TargetUnitTag ? order.TargetUnitTag : null,
                        order.TargetCase == UnitOrder.TargetOneofCase.TargetWorldSpacePos ? order.TargetWorldSpacePos?.X : null,
                        order.TargetCase == UnitOrder.TargetOneofCase.TargetWorldSpacePos ? order.TargetWorldSpacePos?.Y : null))
                        .ToArray()
                    : Array.Empty<UnitOrderObservationFacts>();

                return new UnitObservationFacts(
                    unit.Tag,
                    unit.UnitType,
                    unitKind ?? string.Empty,
                    unit.Owner,
                    MapAlliance(unit.Alliance),
                    MapVisibility(unit.DisplayType),
                    captureHealth ? unit.Health : 0,
                    unit.HealthMax,
                    captureShields ? unit.Shield : 0,
                    captureShields ? unit.ShieldMax : 0,
                    Has(ObservationFields.BuildProgress) ? unit.BuildProgress : 0,
                    position?.X ?? 0,
                    position?.Y ?? 0,
                    position is not null,
                    captureCargo && HasResourceCargo(unit, buffNames),
                    Has(ObservationFields.HasOrders) || captureOrderDetails ? unit.Orders.Count > 0 : false,
                    Has(ObservationFields.Mobility) && unit.IsFlying,
                    Has(ObservationFields.Mobility) && unit.IsBurrowed,
                    Has(ObservationFields.Mobility) && unit.IsHallucination,
                    observation.GameLoop)
                {
                    WeaponCooldownRemaining = Has(ObservationFields.WeaponCooldown) ? unit.WeaponCooldown : 0,
                    CollisionRadius = Has(ObservationFields.CollisionRadius) ? unit.Radius : 0,
                    Energy = Has(ObservationFields.Energy) ? unit.Energy : 0,
                    VespeneContents = Has(ObservationFields.VespeneContents)
                        ? checked((uint)Math.Max(0, unit.VespeneContents))
                        : 0,
                    Orders = orders,
                    CapturedFields = fields
                };
            })
            .ToArray();

        var deadTags = raw.Event?.DeadUnits
            .Where(tag => tag != 0)
            .ToHashSet() ?? new HashSet<ulong>();

        return new NormalizedObservation(observation.GameLoop, playerId, units, deadTags)
        {
            Minerals = Has(ObservationFields.Economy) ? observation.PlayerCommon?.Minerals ?? 0 : 0,
            Vespene = Has(ObservationFields.Economy) ? observation.PlayerCommon?.Vespene ?? 0 : 0,
            FoodUsed = Has(ObservationFields.Economy) ? observation.PlayerCommon?.FoodUsed ?? 0 : 0,
            FoodCap = Has(ObservationFields.Economy) ? observation.PlayerCommon?.FoodCap ?? 0 : 0
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
