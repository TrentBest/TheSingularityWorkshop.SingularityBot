using System.Globalization;

namespace SingularityBot.Behavior;

/// <summary>
/// Trusted, reusable BotMethods that translate doctrine events into production
/// intent. These methods mutate the demand ledger only; production controllers
/// remain responsible for reconciling and issuing game actions.
/// </summary>
public static class BotProductionMethods
{
    public const string LargeEnemyForceMarineResponse = "production.respond-to-large-enemy-force.v1";

    public static void RegisterLargeEnemyForceMarineResponse(
        BotDoctrineRuntimeCatalog catalog,
        ProductionDemandLedger production,
        Func<Sc2GameDataCatalog> gameDataProvider,
        int concernThreshold = 4,
        int additionalMarines = 10,
        int priority = 85)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(gameDataProvider);
        if (concernThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(concernThreshold));
        if (additionalMarines < 1 || additionalMarines > ProductionDemandLedger.MaximumDesiredCount)
            throw new ArgumentOutOfRangeException(nameof(additionalMarines));
        if (priority is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(priority));

        catalog.RegisterMethod(new BotDoctrineMethodRegistration(
            LargeEnemyForceMarineResponse,
            BotDoctrineMethodScope.Bot,
            (botEvent, cancellationToken) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return ValueTask.FromCanceled(cancellationToken);

                if (botEvent is null ||
                    !botEvent.Data.TryGetValue("enemy.count", out var countText) ||
                    !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var enemyCount) ||
                    enemyCount <= concernThreshold)
                    return ValueTask.CompletedTask;

                var gameData = gameDataProvider();
                if (gameData is null || !gameData.TryGetUniqueUnit("Marine", out var marine))
                    return ValueTask.CompletedTask;

                // A replayed event cannot add the same reinforcement request twice.
                production.AddOnce(
                    $"{LargeEnemyForceMarineResponse}:{botEvent.CorrelationId}",
                    marine,
                    additionalMarines,
                    priority);
                return ValueTask.CompletedTask;
            }));
    }
}
