using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>
/// The strategic controller selected for one concrete matchup. The executable
/// implementation and its presentation profile are intentionally independent.
/// </summary>
public sealed record BotHeroSelection
{
    public string ImplementationId { get; init; } = string.Empty;
    public string PresentationProfileId { get; init; } = string.Empty;

    public IReadOnlyList<string> Validate(string path = "$.hero")
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ImplementationId) || ImplementationId.Length > 160)
            errors.Add($"{path}.implementationId: a stable registered implementation id of 1 to 160 characters is required.");
        if (string.IsNullOrWhiteSpace(PresentationProfileId) || PresentationProfileId.Length > 160)
            errors.Add($"{path}.presentationProfileId: a presentation profile id of 1 to 160 characters is required.");
        return errors.AsReadOnly();
    }
}

/// <summary>
/// A complete, independently authored composition for one own-race versus
/// opponent-race pairing. Hero is the strategic authority; command layers are
/// subordinate tactical/execution responsibilities.
/// </summary>
public sealed record BotMatchupComposition
{
    public Race OwnRace { get; init; }
    public Race OpponentRace { get; init; }
    public BotHeroSelection Hero { get; init; } = new();
    public CommandLayerComposition Layers { get; init; } = new();

    public IReadOnlyList<string> Validate(string path = "$")
    {
        var errors = new List<string>();
        if (!BotCompositionMatrix.IsConcreteRace(OwnRace))
            errors.Add($"{path}.ownRace: must be Terran, Protoss or Zerg; Random and Unknown are not concrete matchup races.");
        if (!BotCompositionMatrix.IsConcreteRace(OpponentRace))
            errors.Add($"{path}.opponentRace: must be Terran, Protoss or Zerg; Random and Unknown are not concrete matchup races.");
        if (Hero is null)
            errors.Add($"{path}.hero: is required.");
        else
            errors.AddRange(Hero.Validate($"{path}.hero"));
        if (Layers is null)
            errors.Add($"{path}.layers: is required.");
        else
            errors.AddRange(Layers.Validate().Select(error => $"{path}.layers{error.TrimStart('$')}"));
        return errors.AsReadOnly();
    }
}

/// <summary>
/// Versioned declarative matrix containing exactly one composition for each
/// of the nine concrete playable-race matchups. Random is a selection policy,
/// never a matrix key. This model does not resolve IDs to executable code.
/// </summary>
public sealed class BotCompositionMatrix
{
    private static readonly Race[] ConcreteRaces =
    {
        Race.Terran,
        Race.Protoss,
        Race.Zerg
    };

    public int Version { get; init; } = 1;
    public IReadOnlyList<BotMatchupComposition> Matchups { get; init; } =
        Array.Empty<BotMatchupComposition>();

    public static bool IsConcreteRace(Race race) => ConcreteRaces.Contains(race);

    /// <summary>Returns deterministic, path-qualified structural validation errors.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1)
            errors.Add("$.version: only version 1 is supported.");
        if (Matchups is null)
        {
            errors.Add("$.matchups: is required.");
            return errors.AsReadOnly();
        }
        if (Matchups.Count != 9)
            errors.Add("$.matchups: exactly nine concrete race matchups are required.");

        var keys = new HashSet<(Race Own, Race Opponent)>();
        for (var index = 0; index < Matchups.Count; index++)
        {
            var matchup = Matchups[index];
            var path = $"$.matchups[{index}]";
            if (matchup is null)
            {
                errors.Add($"{path}: cannot be null.");
                continue;
            }

            errors.AddRange(matchup.Validate(path));
            if (IsConcreteRace(matchup.OwnRace) && IsConcreteRace(matchup.OpponentRace) &&
                !keys.Add((matchup.OwnRace, matchup.OpponentRace)))
                errors.Add($"{path}: duplicate matchup '{matchup.OwnRace}-vs-{matchup.OpponentRace}'.");
        }

        foreach (var ownRace in ConcreteRaces)
        foreach (var opponentRace in ConcreteRaces)
        {
            if (!keys.Contains((ownRace, opponentRace)))
                errors.Add($"$.matchups: missing required matchup '{ownRace}-vs-{opponentRace}'.");
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Resolves only an exact concrete matchup. Unknown, Random, or unresolved
    /// opponent races never fall back to a default composition.
    /// </summary>
    public bool TryResolve(Race ownRace, Race opponentRace, out BotMatchupComposition matchup)
    {
        matchup = null;
        if (!IsConcreteRace(ownRace) || !IsConcreteRace(opponentRace) || Validate().Count != 0)
            return false;

        matchup = Matchups.SingleOrDefault(candidate =>
            candidate.OwnRace == ownRace && candidate.OpponentRace == opponentRace);
        return matchup is not null;
    }
}
