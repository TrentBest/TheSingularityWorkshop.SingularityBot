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
    public void AuditedGameplayRequirementsCoverAllKnownRuntimeConsumers()
    {
        var requirements = ObservationRequirements.Combine(
            ObservationRequirements.UnitFsm,
            ObservationRequirements.WorkerAssignment,
            ObservationRequirements.Production,
            ObservationRequirements.Building,
            ObservationRequirements.Refinery,
            ObservationRequirements.Combat,
            ObservationRequirements.Scouting);

        Assert.Equal(ObservationFields.Gameplay, requirements.Fields);
        Assert.True(requirements.Includes(
            ObservationFields.Health | ObservationFields.Shields |
            ObservationFields.BuildProgress | ObservationFields.Position |
            ObservationFields.Cargo | ObservationFields.HasOrders |
            ObservationFields.OrderDetails | ObservationFields.Mobility |
            ObservationFields.WeaponCooldown | ObservationFields.Energy |
            ObservationFields.VespeneContents | ObservationFields.Economy));
        Assert.False(requirements.Includes(ObservationFields.CollisionRadius));
    }

    [Fact]
    public void OpeningRequirementsCaptureProductionAndWorkerFactsWithoutCombatOnlyFields()
    {
        var requirements = ObservationRequirements.Opening;

        Assert.True(requirements.Includes(
            ObservationFields.Position | ObservationFields.Cargo |
            ObservationFields.HasOrders | ObservationFields.OrderDetails |
            ObservationFields.BuildProgress | ObservationFields.Economy));
        Assert.False(requirements.Includes(
            ObservationFields.Health | ObservationFields.Shields |
            ObservationFields.Mobility | ObservationFields.WeaponCooldown |
            ObservationFields.Energy | ObservationFields.VespeneContents));
    }

    [Fact]
    public void PassiveBaselineRemainsFullCapture()
    {
        Assert.Equal(ObservationFields.All, ObservationRequirements.PassiveBaseline.Fields);
        Assert.True(ObservationRequirements.Gameplay.Includes(ObservationFields.VespeneContents));
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
