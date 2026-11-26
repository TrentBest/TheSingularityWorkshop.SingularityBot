using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ProductionDemandLedgerTests
{
    private static readonly Sc2UnitFacts Marine = new(48, "Marine", 560, 50, 0, 1, 0);
    private static readonly Sc2UnitFacts Barracks = new(21, "Barracks", 321, 150, 0, 0, 0);

    [Fact]
    public void Set_StoresAbsoluteDesiredCountByAuthoritativeUnitTypeId()
    {
        var production = new ProductionDemandLedger();

        Assert.True(production.Set(Barracks, 2));
        Assert.True(production.Set(Marine, 10));

        Assert.Equal(2, production.Snapshot()[Barracks.UnitId].DesiredCount);
        Assert.Equal(10, production.Snapshot()[Marine.UnitId].DesiredCount);
    }

    [Fact]
    public void Add_IncreasesOrDecreasesDesiredTotalWithoutIssuingCommands()
    {
        var production = new ProductionDemandLedger();

        Assert.True(production.Set(Marine, 10));
        Assert.True(production.Add(Marine, 10));
        Assert.True(production.Add(Marine, -3));

        Assert.Equal(17, production.Snapshot()[Marine.UnitId].DesiredCount);
    }

    [Fact]
    public void AddOnce_IsIdempotentForReplayedEventCorrelation()
    {
        var production = new ProductionDemandLedger();

        Assert.True(production.AddOnce("enemy-force:loop-900", Marine, 10));
        Assert.False(production.AddOnce("enemy-force:loop-900", Marine, 10));

        Assert.Equal(10, production.Snapshot()[Marine.UnitId].DesiredCount);
    }

    [Fact]
    public void InvalidOrAmbiguousUnitIdentityAndOutOfRangeDemandFailClosed()
    {
        var production = new ProductionDemandLedger();

        Assert.False(production.Set(new Sc2UnitFacts(0, "Marine", 1, 50, 0, 1, 0), 10));
        Assert.False(production.Set(Marine, ProductionDemandLedger.MaximumDesiredCount + 1));
        Assert.False(production.Set(Marine with { Name = "Marauder" }, 10));
        Assert.False(production.Add(Marine, -1));
        Assert.Empty(production.Snapshot());
    }

    [Fact]
    public void LargeEnemyForceMethodAddsTenMarinesOnlyAboveThresholdAndOnlyOnce()
    {
        var production = new ProductionDemandLedger();
        var catalog = new BotDoctrineRuntimeCatalog();
        var gameData = Sc2GameDataCatalog.Create(new SC2APIProtocol.ResponseData
        {
            Units = { new SC2APIProtocol.UnitTypeData
            {
                UnitId = Marine.UnitId,
                Name = Marine.Name,
                AbilityId = Marine.AbilityId,
                MineralCost = Marine.MineralCost,
                FoodRequired = Marine.FoodRequired
            }}
        });
        BotProductionMethods.RegisterLargeEnemyForceMarineResponse(catalog, production, () => gameData);
        var method = Assert.IsType<BotDoctrineMethodRegistration>(GetMethod(catalog));
        var belowThreshold = new BotDoctrineEvent(
            "observation.enemy-force-concern", "contact-1", 100,
            new Dictionary<string, string> { ["enemy.count"] = "4" });
        var aboveThreshold = belowThreshold with
        {
            CorrelationId = "contact-2",
            Data = new Dictionary<string, string> { ["enemy.count"] = "5" }
        };

        method.Handler(belowThreshold, CancellationToken.None).GetAwaiter().GetResult();
        method.Handler(aboveThreshold, CancellationToken.None).GetAwaiter().GetResult();
        method.Handler(aboveThreshold, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(10, production.Snapshot()[Marine.UnitId].DesiredCount);
    }

    private static BotDoctrineMethodRegistration GetMethod(BotDoctrineRuntimeCatalog catalog)
    {
        Assert.True(catalog.TryGetMethod(BotProductionMethods.LargeEnemyForceMarineResponse, out var method));
        return method;
    }
}
