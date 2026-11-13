using SC2APIProtocol;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2WeaponCooldownNormalizationTests
{
    [Fact]
    public void NormalizerPreservesRawWeaponCooldown()
    {
        var raw = new ObservationRaw();
        raw.Units.Add(new Unit
        {
            Tag = 7,
            UnitType = 48,
            Owner = 1,
            Alliance = Alliance.Self,
            DisplayType = DisplayType.Visible,
            Health = 45,
            HealthMax = 45,
            WeaponCooldown = 2.75f,
            Radius = 0.375f
        });
        var response = new ResponseObservation
        {
            Observation = new SC2APIProtocol.Observation
            {
                GameLoop = 100,
                RawData = raw
            }
        };

        var normalized = Sc2ObservationNormalizer.Normalize(
            response, 1,
            new Dictionary<uint, string> { [48] = "Marine" },
            new Dictionary<uint, string>());

        var unit = Assert.Single(normalized.Units);
        Assert.Equal(2.75f, unit.WeaponCooldownRemaining);
        Assert.Equal(0.375f, unit.CollisionRadius);
    }
}
