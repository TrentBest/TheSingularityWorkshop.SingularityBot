using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class CommandLayerCompositionTests
{
    [Fact]
    public void Composition_AllowsReplaceableDoctrineAndIndependentPresentation()
    {
        var composition = new CommandLayerComposition
        {
            Layers = new[]
            {
                new CommandLayerSelection
                {
                    SlotId = "army",
                    Kind = CommandLayerKind.Army,
                    ImplementationId = "terran.black-ops.army.v1",
                    PresentationProfileId = "doctrine.black-ops",
                    Emits = new[] { "military.operation.intent.v1" }
                },
                new CommandLayerSelection
                {
                    SlotId = "squad.default",
                    Kind = CommandLayerKind.Squad,
                    ImplementationId = "terran.black-ops.squad.v1",
                    PresentationProfileId = "doctrine.black-ops",
                    Accepts = new[] { "military.operation.intent.v1" },
                    Emits = new[] { "military.unit-assignment.v1" }
                },
                new CommandLayerSelection
                {
                    SlotId = "unit.default",
                    Kind = CommandLayerKind.Unit,
                    ImplementationId = "terran.vanilla.unit-micro.v1",
                    PresentationProfileId = "doctrine.black-ops",
                    Accepts = new[] { "military.unit-assignment.v1" }
                }
            }
        };

        Assert.Empty(composition.Validate());
        Assert.NotEqual(composition.Layers[0].ImplementationId,
            composition.Layers[0].PresentationProfileId);
    }

    [Fact]
    public void Composition_RejectsDuplicateSlotsAndDisconnectedDeclaredHandoff()
    {
        var composition = new CommandLayerComposition
        {
            Layers = new[]
            {
                new CommandLayerSelection
                {
                    SlotId = "army",
                    Kind = CommandLayerKind.Army,
                    ImplementationId = "army.v1",
                    PresentationProfileId = "default",
                    Emits = new[] { "military.operation.v1" }
                },
                new CommandLayerSelection
                {
                    SlotId = "army",
                    Kind = CommandLayerKind.Squad,
                    ImplementationId = "squad.v1",
                    PresentationProfileId = "default",
                    Accepts = new[] { "military.assignment.v1" }
                }
            }
        };

        var errors = composition.Validate();

        Assert.Contains(errors, error => error.Contains("duplicate slot id"));
        Assert.Contains(errors, error => error.Contains("emits no contract accepted"));
    }

    [Fact]
    public void Composition_AllowsSparseHierarchyAndDisabledLayers()
    {
        var composition = new CommandLayerComposition
        {
            Layers = new[]
            {
                new CommandLayerSelection
                {
                    SlotId = "army",
                    Kind = CommandLayerKind.Army,
                    ImplementationId = "army.simple.v1",
                    PresentationProfileId = "classic"
                },
                new CommandLayerSelection
                {
                    SlotId = "platoon.optional",
                    Kind = CommandLayerKind.Platoon,
                    ImplementationId = "platoon.experimental.v1",
                    PresentationProfileId = "classic",
                    Enabled = false
                },
                new CommandLayerSelection
                {
                    SlotId = "squad",
                    Kind = CommandLayerKind.Squad,
                    ImplementationId = "squad.simple.v1",
                    PresentationProfileId = "classic"
                },
                new CommandLayerSelection
                {
                    SlotId = "unit",
                    Kind = CommandLayerKind.Unit,
                    ImplementationId = "unit.micro.v1",
                    PresentationProfileId = "classic"
                }
            }
        };

        Assert.Empty(composition.Validate());
    }
    [Fact]
    public void Composition_RejectsMoreThanFiveEnabledCommandLayers()
    {
        var layers = Enumerable.Range(0, 6).Select(index => new CommandLayerSelection
        {
            SlotId = $"layer.{index}",
            Kind = (CommandLayerKind)index,
            ImplementationId = $"implementation.{index}",
            PresentationProfileId = "classic",
            Enabled = true
        }).ToArray();

        var composition = new CommandLayerComposition { Layers = layers };

        Assert.Contains(composition.Validate(), error => error.Contains("at most 5 enabled command layers"));
    }

}
