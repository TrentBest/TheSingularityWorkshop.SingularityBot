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

    [Fact]
    public void Registry_SnapshotsDescriptorMetadataAtRegistration()
    {
        var races = new List<string> { "Terran" };
        var capabilities = new List<string> { "planner.reservations" };
        var descriptor = Army() with
        {
            SupportedRaces = races,
            RequiredCapabilities = capabilities
        };
        var registry = new CommandLayerRegistry();

        registry.Register(descriptor);
        races[0] = "Zerg";
        capabilities.Clear();

        var resolved = registry.Resolve(
            new CommandLayerSelection
            {
                SlotId = "army",
                Kind = CommandLayerKind.Army,
                ImplementationId = "terran.army.standard.v1"
            },
            "Terran",
            new[] { "observation.units", "planner.reservations" });

        Assert.True(resolved.Success);
        Assert.Equal("Terran", Assert.Single(resolved.Descriptor.SupportedRaces));
        Assert.Contains("planner.reservations", resolved.Descriptor.RequiredCapabilities);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)resolved.Descriptor.SupportedRaces)[0] = "Zerg");
    }

    [Fact]
    public void Registry_RejectsMalformedDescriptorMetadata()
    {
        var registry = new CommandLayerRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(Army() with
        {
            SupportedRaces = null
        }));
        Assert.Throws<ArgumentException>(() => registry.Register(Army() with
        {
            SupportedRaces = Array.Empty<string>()
        }));
        Assert.Throws<ArgumentException>(() => registry.Register(Army() with
        {
            Emits = new[] { "intent.v1", "intent.v1" }
        }));
        Assert.Throws<ArgumentException>(() => registry.Register(Army() with
        {
            RequiredCapabilities = new[] { " " }
        }));
    }

    [Fact]
    public void Registry_ExposesVersionedTypedSettingsAndSnapshotsNestedMetadata()
    {
        var allowedRoles = new List<string> { "contain", "engage" };
        var settings = new[]
        {
            new CommandLayerSettingDescriptor
            {
                Id = "enabled",
                DisplayName = "Enabled",
                Description = "Whether this layer participates.",
                ValueKind = CommandLayerSettingValueKind.Boolean,
                DefaultJson = "true",
                Mutability = CommandLayerSettingMutability.PreMatch
            },
            new CommandLayerSettingDescriptor
            {
                Id = "default-role",
                DisplayName = "Default role",
                Description = "Initial squad posture.",
                ValueKind = CommandLayerSettingValueKind.Enumeration,
                DefaultJson = "\"contain\"",
                AllowedValues = allowedRoles,
                VisibleWhenSettingId = "enabled",
                VisibleWhenValueJson = "true"
            },
            new CommandLayerSettingDescriptor
            {
                Id = "fallback-health",
                DisplayName = "Fallback health",
                Description = "Health fraction at which units retreat.",
                ValueKind = CommandLayerSettingValueKind.Number,
                DefaultJson = "0.35",
                Minimum = 0,
                Maximum = 1,
                Precision = 2,
                Units = "fraction"
            }
        };
        var registry = new CommandLayerRegistry();
        registry.Register(Army() with { SettingsSchemaVersion = 3, Settings = settings });

        allowedRoles[0] = "mutated";
        var descriptor = Assert.Single(registry.Descriptors);
        Assert.Equal(3, descriptor.SettingsSchemaVersion);
        Assert.Equal("contain", descriptor.Settings[1].AllowedValues[0]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)descriptor.Settings[1].AllowedValues)[0] = "mutated");
        Assert.Empty(CommandLayerSettingSchema.Validate(descriptor.Settings, descriptor.SettingsSchemaVersion));
    }

    [Fact]
    public void Registry_RejectsInvalidSettingDefaultsRangesAndConditions()
    {
        var registry = new CommandLayerRegistry();
        var duplicate = new[]
        {
            new CommandLayerSettingDescriptor
            {
                Id = "enabled", DisplayName = "Enabled", ValueKind = CommandLayerSettingValueKind.Boolean,
                DefaultJson = "true"
            },
            new CommandLayerSettingDescriptor
            {
                Id = "enabled", DisplayName = "Also enabled", ValueKind = CommandLayerSettingValueKind.Boolean,
                DefaultJson = "false"
            }
        };
        Assert.Throws<ArgumentException>(() => registry.Register(Army() with { Settings = duplicate }));

        var invalidRange = new[]
        {
            new CommandLayerSettingDescriptor
            {
                Id = "health", DisplayName = "Health", ValueKind = CommandLayerSettingValueKind.Number,
                DefaultJson = "0.5", Minimum = 0.8, Maximum = 0.2
            }
        };
        Assert.Throws<ArgumentException>(() => registry.Register(Army() with { Settings = invalidRange }));

        var unknownCondition = new[]
        {
            new CommandLayerSettingDescriptor
            {
                Id = "role", DisplayName = "Role", ValueKind = CommandLayerSettingValueKind.Enumeration,
                DefaultJson = "\"contain\"", AllowedValues = new[] { "contain", "engage" },
                VisibleWhenSettingId = "missing", VisibleWhenValueJson = "true"
            }
        };
        Assert.Throws<ArgumentException>(() => registry.Register(Army() with { Settings = unknownCondition }));
    }

    [Fact]
    public void SettingSchema_ValidatesNestedObjectsAndBoundedStringLists()
    {
        var settings = new[]
        {
            new CommandLayerSettingDescriptor
            {
                Id = "formation",
                DisplayName = "Formation",
                ValueKind = CommandLayerSettingValueKind.Object,
                DefaultJson = "{\"spacing\":1}",
                Children = new[]
                {
                    new CommandLayerSettingDescriptor
                    {
                        Id = "spacing", DisplayName = "Spacing", ValueKind = CommandLayerSettingValueKind.Integer,
                        DefaultJson = "1", Minimum = 1, Maximum = 8
                    }
                }
            },
            new CommandLayerSettingDescriptor
            {
                Id = "preferred-targets",
                DisplayName = "Preferred targets",
                ValueKind = CommandLayerSettingValueKind.StringList,
                DefaultJson = "[\"Marine\"]",
                MaximumItems = 4,
                AllowedValues = new[] { "Marine", "Marauder", "SiegeTank" }
            }
        };

        Assert.Empty(CommandLayerSettingSchema.Validate(settings));
        Assert.Throws<ArgumentException>(() => CommandLayerSettingSchema.Snapshot(settings, schemaVersion: 0));
    }
}
