using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class EconomyAdvisorTests
{
    [Fact]
    public void DefaultsReserveFirstDepotBeforeAdditionalWorkerProduction()
    {
        var advisor = new EconomyAdvisor();
        var reserve = advisor.GetReservedMinerals(
            new EconomyAdvisorFrame(false, false, false, false, false, true),
            new EconomyAdvisorCosts(400, 100, 50, 150, 50));

        Assert.Equal(uint.MaxValue, reserve);
    }

    [Fact]
    public void InfrastructureReservationsFollowObservedOpeningPriority()
    {
        var advisor = new EconomyAdvisor();
        var costs = new EconomyAdvisorCosts(400, 100, 50, 150, 50);

        Assert.Equal(400u, advisor.GetReservedMinerals(
            new EconomyAdvisorFrame(true, true, true, true, true, false), costs));
        Assert.Equal(150u, advisor.GetReservedMinerals(
            new EconomyAdvisorFrame(false, false, false, true, true, false), costs));
        Assert.Equal(50u, advisor.GetReservedMinerals(
            new EconomyAdvisorFrame(false, false, false, false, true, false), costs));
    }

    [Fact]
    public void SupplyDepotWorkerPullThresholdDefaultsToEightyAndCanBeConfigured()
    {
        var advisor = new EconomyAdvisor();

        Assert.Equal(80u, advisor.Settings.SupplyDepotWorkerPullMinerals);

        var valid = advisor.ConfigureJson(
            "{\"version\":1,\"supplyDepotWorkerPullMinerals\":95}");

        Assert.Empty(valid);
        Assert.Equal(95u, advisor.Settings.SupplyDepotWorkerPullMinerals);
    }

    [Fact]
    public void RejectsAnUnreasonableSupplyDepotWorkerPullThreshold()
    {
        var advisor = new EconomyAdvisor();

        var errors = advisor.ConfigureJson(
            "{\"version\":1,\"supplyDepotWorkerPullMinerals\":1001}");

        Assert.NotEmpty(errors);
        Assert.Equal(80u, advisor.Settings.SupplyDepotWorkerPullMinerals);
    }

    [Fact]
    public void MulesAreEnabledByDefaultButCanBeDisabledAndConfiguredForImmediateSpending()
    {
        var advisor = new EconomyAdvisor();
        Assert.True(advisor.Settings.EnableMules);
        Assert.Equal("PreserveScan", advisor.Settings.MuleCallDownPolicy);

        var valid = advisor.ConfigureJson(
            "{\"version\":1,\"enableMules\":false,\"muleCallDownPolicy\":\"Immediate\"}");

        Assert.Empty(valid);
        Assert.False(advisor.Settings.EnableMules);
        Assert.Equal("Immediate", advisor.Settings.MuleCallDownPolicy);
    }

    [Fact]
    public void OrbitalMorphWaitsForTheFirstObservedMarineAndEnableMulesSetting()
    {
        var advisor = new EconomyAdvisor();

        Assert.False(advisor.ShouldEnableOrbitalMorph(firstMarineTrainingOrderObserved: false));
        Assert.True(advisor.ShouldEnableOrbitalMorph(firstMarineTrainingOrderObserved: true));

        var errors = advisor.ConfigureJson(
            "{\"version\":1,\"enableMules\":false}");
        Assert.Empty(errors);
        Assert.False(advisor.ShouldEnableOrbitalMorph(firstMarineTrainingOrderObserved: true));
    }

    [Fact]
    public void FirstMarineCanSpendItsOwnFiftyMineralReservation()
    {
        var advisor = new EconomyAdvisor();

        Assert.Equal(0u, advisor.GetMarineActionReservedMinerals(
            50, firstMarineReady: true, firstMarineTrainingOrderObserved: false, marineMineralCost: 50));
        Assert.Equal(150u, advisor.GetMarineActionReservedMinerals(
            150, firstMarineReady: true, firstMarineTrainingOrderObserved: false, marineMineralCost: 50));
        Assert.Equal(50u, advisor.GetMarineActionReservedMinerals(
            50, firstMarineReady: true, firstMarineTrainingOrderObserved: true, marineMineralCost: 50));
    }

    [Fact]
    public void RejectsUnknownMuleCallDownPolicyWithoutChangingCurrentSettings()
    {
        var advisor = new EconomyAdvisor();

        var errors = advisor.ConfigureJson(
            "{\"version\":1,\"muleCallDownPolicy\":\"SpendEverything\"}");

        Assert.NotEmpty(errors);
        Assert.Equal("PreserveScan", advisor.Settings.MuleCallDownPolicy);
    }

    [Fact]
    public void ConfigurationIsValidatedAndAppliedAtomically()
    {
        var advisor = new EconomyAdvisor();

        var invalid = advisor.ConfigureJson("{\"mineralSaturation\":0}");
        Assert.NotEmpty(invalid);
        Assert.Equal(2, advisor.Settings.MineralSaturation);

        var valid = advisor.ConfigureJson(
            "{\"version\":1,\"mineralSaturation\":3,\"prioritizeOpeningInfrastructure\":false,\"holdWorkerProductionForFirstDepot\":false}");

        Assert.Empty(valid);
        Assert.Equal(3, advisor.Settings.MineralSaturation);
        Assert.False(advisor.Settings.PrioritizeOpeningInfrastructure);
        Assert.False(advisor.Settings.HoldWorkerProductionForFirstDepot);
    }
}
