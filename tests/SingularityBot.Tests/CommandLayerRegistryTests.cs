using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class CommandLayerRegistryTests
{
    private static CommandLayerDescriptor Army(string id = "terran.army.standard.v1") => new()
    {
        ImplementationId = id,
        Kind = CommandLayerKind.Army,
        SupportedRaces = new[] { "Terran" },
        RequiredCapabilities = new[] { "observation.units", "planner.reservations" },
        Emits = new[] { "military.operation.intent.v1" },
        DisplayName = "Standard Army",
        Description = "Coordinates match-level military operations."
    };

    [Fact]
    public void Registry_ResolvesOnlyRegisteredImplementationAndChecksRaceAndCapabilities()
    {
        var registry = new CommandLayerRegistry();
        registry.Register(Army());

        var resolved = registry.Resolve(
            new CommandLayerSelection { SlotId = "army", Kind = CommandLayerKind.Army, ImplementationId = "terran.army.standard.v1" },
            "Terran",
            new[] { "observation.units", "planner.reservations" });

        Assert.True(resolved.Success);
        Assert.Equal("terran.army.standard.v1", resolved.Descriptor.ImplementationId);

        Assert.False(registry.Resolve(
            new CommandLayerSelection { SlotId = "army", Kind = CommandLayerKind.Army, ImplementationId = "System.IO.FileInfo" },
            "Terran", Array.Empty<string>()).Success);
        Assert.False(registry.Resolve(
            new CommandLayerSelection { SlotId = "army", Kind = CommandLayerKind.Army, ImplementationId = "terran.army.standard.v1" },
            "Zerg", new[] { "observation.units", "planner.reservations" }).Success);
        Assert.False(registry.Resolve(
            new CommandLayerSelection { SlotId = "army", Kind = CommandLayerKind.Army, ImplementationId = "terran.army.standard.v1" },
            "Terran", new[] { "observation.units" }).Success);
    }

    [Fact]
    public void Registry_RejectsWrongLayerKindAndDuplicateRegistration()
    {
        var registry = new CommandLayerRegistry();
        registry.Register(Army());

        var wrongKind = registry.Resolve(
            new CommandLayerSelection { SlotId = "squad", Kind = CommandLayerKind.Squad, ImplementationId = "terran.army.standard.v1" },
            "Terran", new[] { "observation.units", "planner.reservations" });

        Assert.False(wrongKind.Success);
        Assert.Throws<InvalidOperationException>(() => registry.Register(Army()));
    }

    [Fact]
    public void Registry_PresentationChangeDoesNotChangeResolvedBehavior()
    {
        var registry = new CommandLayerRegistry();
        registry.Register(Army());
        var first = registry.Resolve(new CommandLayerSelection
        {
            SlotId = "army", Kind = CommandLayerKind.Army,
            ImplementationId = "terran.army.standard.v1", PresentationProfileId = "classic"
        }, "Terran", new[] { "observation.units", "planner.reservations" });
        var second = registry.Resolve(new CommandLayerSelection
        {
            SlotId = "army", Kind = CommandLayerKind.Army,
            ImplementationId = "terran.army.standard.v1", PresentationProfileId = "sci-fi"
        }, "Terran", new[] { "observation.units", "planner.reservations" });

        Assert.Same(first.Descriptor, second.Descriptor);
    }
}
