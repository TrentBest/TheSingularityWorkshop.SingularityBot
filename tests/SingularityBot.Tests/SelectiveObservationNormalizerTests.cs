using SC2APIProtocol;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class SelectiveObservationNormalizerTests
{
    [Fact]
    public void Normalizer_CapturesMineralContentsOnlyWhenRequested()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 99,
            UnitType = 341,
            Owner = 0,
            Alliance = Alliance.Neutral,
            DisplayType = DisplayType.Visible,
            MineralContents = 1250
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation { RawData = raw }
        };

        var captured = Sc2ObservationNormalizer.Normalize(
            response, 1, new Dictionary<uint, string> { [341] = "MineralField750" },
            new Dictionary<uint, string>(),
            new ObservationRequirements(ObservationFields.MineralContents));
        Assert.Equal(1250u, Assert.Single(captured.Units).MineralContents);

        var omitted = Sc2ObservationNormalizer.Normalize(
            response, 1, new Dictionary<uint, string>(), new Dictionary<uint, string>(),
            ObservationRequirements.None);
        Assert.Equal(0u, Assert.Single(omitted.Units).MineralContents);
    }

    [Fact]
    public void Normalizer_CapturesOnlyRequestedScalarAndOrderFields()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 42,
            UnitType = 7,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            Health = 35,
            HealthMax = 45,
            Shield = 12,
            ShieldMax = 20,
            Energy = 75,
            Orders = { new UnitOrder { AbilityId = 3666, TargetUnitTag = 1234 } }
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation { RawData = raw }
        };

        var normalized = Sc2ObservationNormalizer.Normalize(
            response,
            1,
            new Dictionary<uint, string> { [7] = "SCV" },
            new Dictionary<uint, string>(),
            new ObservationRequirements(ObservationFields.Health | ObservationFields.OrderDetails));

        var unit = Assert.Single(normalized.Units);
        Assert.Equal(35f, unit.Health);
        Assert.Equal(45f, unit.HealthMax); // Mandatory identity/context fact, not an optional capture.
        Assert.Equal(0f, unit.Shield);
        Assert.Equal(0f, unit.ShieldMax);
        Assert.Equal(0f, unit.Energy);
        Assert.True(unit.HasOrders); // Order details imply presence.
        Assert.Equal(ObservationFields.Health | ObservationFields.OrderDetails, unit.CapturedFields);
        var order = Assert.Single(unit.Orders);
        Assert.Equal((uint)3666, order.AbilityId);
        Assert.Equal((ulong)1234, order.TargetUnitTag);
    }

    [Fact]
    public void Normalizer_CapturesOrderPresenceWithoutMaterializingOrderDetails()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 42,
            UnitType = 7,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            Orders = { new UnitOrder { AbilityId = 3666 } }
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation { RawData = raw }
        };

        var normalized = Sc2ObservationNormalizer.Normalize(
            response,
            1,
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>(),
            new ObservationRequirements(ObservationFields.HasOrders));

        var unit = Assert.Single(normalized.Units);
        Assert.True(unit.HasOrders);
        Assert.Empty(unit.Orders);
        Assert.Equal(ObservationFields.HasOrders, unit.CapturedFields);
    }

    [Fact]
    public void Normalizer_NoneDoesNotCaptureOptionalFields()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 42,
            UnitType = 7,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            Health = 35,
            Orders = { new UnitOrder { AbilityId = 3666 } }
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation { RawData = raw }
        };

        var normalized = Sc2ObservationNormalizer.Normalize(
            response,
            1,
            new Dictionary<uint, string>(),
            new Dictionary<uint, string>(),
            ObservationRequirements.None);

        var unit = Assert.Single(normalized.Units);
        Assert.Equal(0f, unit.Health);
        Assert.False(unit.HasOrders);
        Assert.Empty(unit.Orders);
        Assert.Equal(ObservationFields.None, unit.CapturedFields);
    }

    [Fact]
    public void Normalizer_CapturesAuthoritativePowerOnlyWhenRequested()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 77,
            UnitType = 62,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            IsPowered = true
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation { RawData = raw }
        };

        var captured = Sc2ObservationNormalizer.Normalize(
            response, 1, new Dictionary<uint, string>(), new Dictionary<uint, string>(),
            new ObservationRequirements(ObservationFields.Power));
        var unit = Assert.Single(captured.Units);
        Assert.True(unit.HasPowerEvidence);
        Assert.True(unit.IsPowered);

        var omitted = Sc2ObservationNormalizer.Normalize(
            response, 1, new Dictionary<uint, string>(), new Dictionary<uint, string>(),
            ObservationRequirements.None);
        unit = Assert.Single(omitted.Units);
        Assert.False(unit.HasPowerEvidence);
        Assert.False(unit.IsPowered);
    }
}
