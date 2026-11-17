using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class ObservationRequirementsTests
{
    [Fact]
    public void AllIncludesEveryOptionalField()
    {
        var requirements = ObservationRequirements.All;

        Assert.Equal(ObservationFields.All, requirements.Fields);
        Assert.True(requirements.Includes(ObservationFields.All));
    }

    [Fact]
    public void UnionCombinesIndependentConsumerRequirements()
    {
        var requirements = ObservationRequirements.UnitFsm |
                           ObservationRequirements.Production |
                           ObservationRequirements.Spatial;

        Assert.True(requirements.Includes(ObservationFields.Cargo));
        Assert.True(requirements.Includes(ObservationFields.WeaponCooldown));
        Assert.True(requirements.Includes(ObservationFields.OrderDetails));
        Assert.True(requirements.Includes(ObservationFields.Economy));
        Assert.True(requirements.Includes(ObservationFields.Position));
        Assert.False(requirements.Includes(ObservationFields.CollisionRadius));
    }

    [Fact]
    public void CombineSupportsAnEmptyAndMultipleRequirementSet()
    {
        var empty = ObservationRequirements.Combine();

        Assert.Equal(ObservationFields.None, empty.Fields);

        var combined = ObservationRequirements.Combine(
            ObservationRequirements.Spatial,
            new ObservationRequirements(ObservationFields.Energy));

        Assert.True(combined.Includes(ObservationFields.Position | ObservationFields.Energy));
        Assert.False(combined.Includes(ObservationFields.OrderDetails));
    }

    [Fact]
    public void OrderDetailsCanBeRequestedSeparatelyFromPresence()
    {
        var presenceOnly = new ObservationRequirements(ObservationFields.HasOrders);
        var details = new ObservationRequirements(ObservationFields.OrderDetails);

        Assert.True(presenceOnly.Includes(ObservationFields.HasOrders));
        Assert.False(presenceOnly.Includes(ObservationFields.OrderDetails));
        Assert.True(details.Includes(ObservationFields.OrderDetails));
        Assert.False(details.Includes(ObservationFields.HasOrders));
    }
}
