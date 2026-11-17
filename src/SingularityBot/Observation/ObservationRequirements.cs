namespace SingularityBot.Observation;

/// <summary>
/// Independently selectable observation fields. Entity identity, ownership,
/// alliance, visibility, health maximum, and game-loop identity remain mandatory.
/// </summary>
[Flags]
public enum ObservationFields
{
    None = 0,
    Health = 1 << 0,
    Shields = 1 << 1,
    BuildProgress = 1 << 2,
    Position = 1 << 3,
    Cargo = 1 << 4,
    HasOrders = 1 << 5,
    OrderDetails = 1 << 6,
    Mobility = 1 << 7,
    WeaponCooldown = 1 << 8,
    CollisionRadius = 1 << 9,
    Energy = 1 << 10,
    VespeneContents = 1 << 11,
    Economy = 1 << 12,

    All = Health | Shields | BuildProgress | Position | Cargo |
          HasOrders | OrderDetails | Mobility | WeaponCooldown |
          CollisionRadius | Energy | VespeneContents | Economy,

    UnitFsm = Health | Shields | BuildProgress | Position | Cargo |
             HasOrders | OrderDetails | Mobility | WeaponCooldown,
    WorkerAssignment = Position | Cargo | HasOrders | OrderDetails,
    Production = BuildProgress | Cargo | HasOrders | OrderDetails | Economy,
    Building = BuildProgress | OrderDetails | Mobility | Energy,
    Combat = Health | Shields | Position | Mobility | WeaponCooldown,
    Spatial = Position,
    Scouting = Position,
    PassiveBaseline = All,

    Gameplay = UnitFsm | WorkerAssignment | Production | Building | Combat | Scouting
}

/// <summary>
/// Immutable, composable declaration of the optional observation fields a consumer needs.
/// Use <see cref="All"/> for compatibility and conservative fallback paths.
/// </summary>
public readonly record struct ObservationRequirements(ObservationFields Fields)
{
    public static ObservationRequirements None => new(ObservationFields.None);
    public static ObservationRequirements All => new(ObservationFields.All);
    public static ObservationRequirements UnitFsm => new(ObservationFields.UnitFsm);
    public static ObservationRequirements WorkerAssignment => new(ObservationFields.WorkerAssignment);
    public static ObservationRequirements Production => new(ObservationFields.Production);
    public static ObservationRequirements Building => new(ObservationFields.Building);
    public static ObservationRequirements Combat => new(ObservationFields.Combat);
    public static ObservationRequirements Spatial => new(ObservationFields.Spatial);
    public static ObservationRequirements Scouting => new(ObservationFields.Scouting);
    public static ObservationRequirements PassiveBaseline => new(ObservationFields.PassiveBaseline);
    public static ObservationRequirements Gameplay => new(ObservationFields.Gameplay);

    public bool Includes(ObservationFields fields) => (Fields & fields) == fields;

    public ObservationRequirements Union(ObservationRequirements other) =>
        new(Fields | other.Fields);

    public static ObservationRequirements Combine(params ObservationRequirements[] requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        var fields = ObservationFields.None;
        foreach (var requirement in requirements)
            fields |= requirement.Fields;
        return new ObservationRequirements(fields);
    }

    public static ObservationRequirements operator |(
        ObservationRequirements left, ObservationRequirements right) => left.Union(right);
}
