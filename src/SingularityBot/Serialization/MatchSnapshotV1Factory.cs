using SingularityBot.Observation;

namespace SingularityBot.Serialization;

/// <summary>Last live-visible contact state retained by the match observation owner.</summary>
public sealed record LastSeenContactV1(uint GameLoop, double X, double Y);

/// <summary>
/// Projects one normalized observation into a complete MatchSnapshot v1.
/// Remembered positions and timestamps must come from retained live-visible
/// history; fog snapshots never manufacture their own last-seen timestamp.
/// </summary>
public static class MatchSnapshotV1Factory
{
    public static MatchSnapshotV1 Create(
        string matchId,
        string mapId,
        string mapContentHash,
        ulong sequence,
        string lifecycle,
        NormalizedObservation observation,
        IReadOnlyList<MatchPlayerV1> players,
        IReadOnlyDictionary<ulong, LastSeenContactV1> lastSeenContacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lifecycle);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(lastSeenContacts);
        if (!SingularityBotDocumentJson.IsSha256(mapContentHash))
            throw new ArgumentException("A valid map content hash is required.", nameof(mapContentHash));

        var playerFacts = players.ToDictionary(player => player.PlayerId);
        if (!playerFacts.TryGetValue(observation.PlayerId, out var observingPlayer))
            throw new ArgumentException("Player metadata must include the observing player.", nameof(players));

        if ((observation.Units.Count > 0) &&
            observation.Units.Any(unit => unit.Tag == 0))
            throw new ArgumentException("Normalized units must have non-zero stable tags.", nameof(observation));

        if ((observation.CapturedFields & ObservationFields.Economy) != 0)
        {
            playerFacts[observation.PlayerId] = observingPlayer with
            {
                Minerals = observation.Minerals,
                Vespene = observation.Vespene,
                FoodUsed = observation.FoodUsed,
                FoodCap = observation.FoodCap
            };
        }

        var destroyedTags = observation.DeadUnitTags
            .Where(tag => tag != 0)
            .OrderBy(tag => tag)
            .ToArray();
        var destroyed = destroyedTags.ToHashSet();

        var units = observation.Units
            .Where(unit => !destroyed.Contains(unit.Tag))
            .OrderBy(unit => unit.Tag)
            .Select(unit => ProjectUnit(unit, observation.GameLoop, lastSeenContacts))
            .ToArray();

        return new MatchSnapshotV1
        {
            SchemaVersion = 1,
            MatchId = matchId,
            MapId = mapId,
            MapContentHash = mapContentHash,
            Sequence = sequence,
            GameLoop = observation.GameLoop,
            Lifecycle = lifecycle,
            Players = playerFacts.Values.OrderBy(player => player.PlayerId).ToArray(),
            Units = units,
            AuthoritativelyDestroyedUnitTags = destroyedTags
        };
    }

    private static MatchUnitV1 ProjectUnit(
        UnitObservationFacts facts,
        uint currentGameLoop,
        IReadOnlyDictionary<ulong, LastSeenContactV1> lastSeenContacts)
    {
        var contactState = "unknown";
        double? x = null;
        double? y = null;
        uint? lastSeenGameLoop = null;

        switch (facts.Visibility)
        {
            case UnitVisibility.Visible:
                if (!facts.HasPosition ||
                    (facts.CapturedFields & ObservationFields.Position) == 0)
                    throw new InvalidOperationException(
                        $"Visible unit {facts.Tag} has no captured position. Include ObservationFields.Position.");
                contactState = "visible";
                x = facts.PositionX;
                y = facts.PositionY;
                lastSeenGameLoop = currentGameLoop;
                break;

            case UnitVisibility.Snapshot:
                if (lastSeenContacts.TryGetValue(facts.Tag, out var memory))
                {
                    if (!double.IsFinite(memory.X) || !double.IsFinite(memory.Y) ||
                        memory.GameLoop > currentGameLoop)
                        throw new InvalidOperationException(
                            $"Last-seen history for unit {facts.Tag} is invalid for game loop {currentGameLoop}.");
                    contactState = "remembered";
                    x = memory.X;
                    y = memory.Y;
                    lastSeenGameLoop = memory.GameLoop;
                }
                break;

            case UnitVisibility.Hidden:
            case UnitVisibility.Unknown:
            default:
                break;
        }

        var fields = facts.CapturedFields;
        var orders = (fields & ObservationFields.OrderDetails) != 0
            ? facts.Orders.Select(order => new MatchOrderV1
            {
                AbilityId = order.AbilityId,
                TargetUnitTag = order.TargetUnitTag,
                TargetX = order.TargetPositionX,
                TargetY = order.TargetPositionY
            }).ToArray()
            : Array.Empty<MatchOrderV1>();

        return new MatchUnitV1
        {
            Tag = facts.Tag,
            UnitTypeId = facts.UnitTypeId,
            UnitKind = facts.UnitKind ?? string.Empty,
            OwnerId = facts.OwnerId,
            Alliance = MapAlliance(facts.Alliance),
            ContactState = contactState,
            LastSeenGameLoop = lastSeenGameLoop,
            X = x,
            Y = y,
            Health = (fields & ObservationFields.Health) != 0 ? facts.Health : null,
            HealthMax = facts.HealthMax,
            Shield = (fields & ObservationFields.Shields) != 0 ? facts.Shield : null,
            ShieldMax = (fields & ObservationFields.Shields) != 0 ? facts.ShieldMax : null,
            Energy = (fields & ObservationFields.Energy) != 0 ? facts.Energy : null,
            BuildProgress = (fields & ObservationFields.BuildProgress) != 0 ? facts.BuildProgress : null,
            Orders = orders
        };
    }

    private static string MapAlliance(UnitAlliance alliance) => alliance switch
    {
        UnitAlliance.Self => "self",
        UnitAlliance.Ally => "ally",
        UnitAlliance.Neutral => "neutral",
        UnitAlliance.Enemy => "enemy",
        _ => "unknown"
    };
}

