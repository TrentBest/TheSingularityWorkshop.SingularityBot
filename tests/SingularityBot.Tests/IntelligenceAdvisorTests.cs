using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class IntelligenceAdvisorTests
{
    [Fact]
    public void PreservesFiftyEnergyForOrbitalScanByDefault()
    {
        var advisor = new IntelligenceAdvisor();

        Assert.True(advisor.Settings.PreserveOrbitalScanBeforeMules);
        Assert.Equal(50u, advisor.GetMuleScanEnergyReserve());
    }

    [Fact]
    public void CanReleaseScanReserveThroughIntelligencePolicy()
    {
        var advisor = new IntelligenceAdvisor();

        var errors = advisor.ConfigureJson(
            "{\"version\":1,\"preserveOrbitalScanBeforeMules\":false}");

        Assert.Empty(errors);
        Assert.Equal(0u, advisor.GetMuleScanEnergyReserve());
        Assert.Equal(50u, advisor.GetMuleScanEnergyReserve(economyAlsoRequestsPreservation: true));
    }

    [Fact]
    public void CanConfigureAReserveLargerThanOneScan()
    {
        var advisor = new IntelligenceAdvisor();

        var errors = advisor.ConfigureJson(
            "{\"version\":1,\"orbitalScanEnergyReserve\":100}");

        Assert.Empty(errors);
        Assert.Equal(100u, advisor.GetMuleScanEnergyReserve());
        Assert.Equal(100u, advisor.GetMuleScanEnergyReserve(economyAlsoRequestsPreservation: true));
    }

    [Fact]
    public void RejectsInvalidEnergyReserveAtomically()
    {
        var advisor = new IntelligenceAdvisor();

        var errors = advisor.ConfigureJson(
            "{\"version\":1,\"orbitalScanEnergyReserve\":201}");

        Assert.NotEmpty(errors);
        Assert.Equal(50u, advisor.GetMuleScanEnergyReserve());
    }
}
