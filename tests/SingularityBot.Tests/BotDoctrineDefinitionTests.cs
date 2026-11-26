using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotDoctrineDefinitionTests
{
    private static readonly Race[] Races = { Race.Terran, Race.Zerg, Race.Protoss };

    private static BotDoctrineNode UnitNode(string id) => new()
    {
        NodeId = id,
        Kind = BotDoctrineNodeKind.UnitFsm,
        Scope = BotDoctrineMethodScope.Unit,
        ImplementationId = "unit.marine.fsm.v1",
        PresentationProfileId = "unit.default"
    };

    private static BotDoctrineNode CommanderNode(string id, params BotDoctrineNode[] children) => new()
    {
        NodeId = id,
        Kind = BotDoctrineNodeKind.Commander,
        Scope = BotDoctrineMethodScope.Commander,
        ImplementationId = "commander.force.v1",
        PresentationProfileId = "role.commander",
        RequiresCapabilities = new[] { "spatial.query" },
        Children = children
    };

    private static BotDoctrineDefinition CreateValid() => new()
    {
        Id = "doctrine.test",
        Name = "Test doctrine",
        Presentation = new BotDoctrinePresentation
        {
            ProfileId = "presentation.test",
            DisplayName = "Test doctrine",
            PrimaryRoleLabel = "Hive Mind",
            SubordinateRoleLabel = "Broodmother"
        },
        Settings = new[]
        {
            new BotDoctrineSetting
            {
                Id = "bot.cheese.enabled",
                ValueType = BotDoctrineValueType.Boolean,
                ValueJson = "false",
                Label = "Cheese",
                EditorControl = "toggle"
            },
            new BotDoctrineSetting
            {
                Id = "bot.opening.chat",
                ValueType = BotDoctrineValueType.String,
                ValueJson = "\"GLHF\"",
                Label = "Opening chat",
                EditorControl = "text"
            }
        },
        Services = new[] { new BotDoctrineServiceRequirement { ServiceId = "spatial.query" } },
        Methods = new[]
        {
            new BotDoctrineMethodBinding
            {
                EventId = "observation.enemy-spotted",
                MethodId = "method.respond-to-contact.v1",
                Scope = BotDoctrineMethodScope.Bot
            }
        },
        Policies = new[]
        {
            new BotDoctrinePolicy
            {
                Id = "policy.retreat-health",
                MetricId = "unit.health.fraction",
                Unit = "fraction",
                Comparison = BotDoctrineComparison.LessThan,
                Threshold = 0.2,
                Effect = "method.evade-and-rejoin"
            }
        },
        InitializationPlans = Races.Select(race => new BotRaceInitializationPlan
        {
            Race = race,
            Steps = Enum.GetValues<BotDoctrineInitializationPhase>().Select((phase, index) =>
                new BotDoctrineInitializationStep
                {
                    Id = $"{race.ToString().ToLowerInvariant()}.{phase.ToString().ToLowerInvariant()}",
                    Phase = phase,
                    ServiceIds = phase == BotDoctrineInitializationPhase.RegisterServices
                        ? new[] { "spatial.query" }
                        : Array.Empty<string>()
                }).ToArray()
        }).ToArray(),
        Matchups = Races.SelectMany(own => Races.Select(opponent => new BotDoctrineMatchup
        {
            OwnRace = own,
            OpponentRace = opponent,
            PrimaryCommanders = new[]
            {
                CommanderNode($"{own}.{opponent}.supreme",
                    CommanderNode($"{own}.{opponent}.platoon", UnitNode($"{own}.{opponent}.unit")))
            }
        })).ToArray()
    };

    [Fact]
    public void MatchIdentity_RequiresExactlyOneConcreteOpponent()
    {
        Assert.Equal(Race.Zerg,
            BotDoctrineMatchIdentity.ResolveSingleOpponentRace(new[] { Race.Zerg }));
        Assert.Throws<InvalidOperationException>(() =>
            BotDoctrineMatchIdentity.ResolveSingleOpponentRace(Array.Empty<Race>()));
        Assert.Throws<InvalidOperationException>(() =>
            BotDoctrineMatchIdentity.ResolveSingleOpponentRace(new[] { Race.Zerg, Race.Terran }));
        Assert.Throws<InvalidOperationException>(() =>
            BotDoctrineMatchIdentity.ResolveSingleOpponentRace(new[] { Race.Random }));
    }

    [Fact]
    public void Definition_ValidatesNineMatchupsAndVariableDepthHierarchy()
    {
        Assert.Empty(CreateValid().Validate());
    }

    [Fact]
    public void Definition_RoundTripsStrictJsonWithoutChangingPresentationOrSettings()
    {
        var original = CreateValid();
        var parsed = BotDoctrineDefinition.Parse(original.Serialize());

        Assert.Equal("Hive Mind", parsed.Presentation.PrimaryRoleLabel);
        Assert.Equal("\"GLHF\"", parsed.Settings.Single(s => s.Id == "bot.opening.chat").ValueJson);
        Assert.Equal(9, parsed.Matchups.Count);
        Assert.Equal(1, parsed.Matchups.Single(m => m.OwnRace == Race.Zerg && m.OpponentRace == Race.Zerg)
            .PrimaryCommanders[0].Children[0].Children.Count);
    }

    [Fact]
    public void Definition_RejectsDuplicateNodeIdsAndUnknownMethodPolicies()
    {
        var definition = CreateValid();
        var matchups = definition.Matchups.ToArray();
        var duplicateRoot = matchups[0].PrimaryCommanders[0] with { NodeId = "shared.node" };
        var duplicateChild = matchups[0].PrimaryCommanders[0].Children[0] with { NodeId = "shared.node" };
        matchups[0] = matchups[0] with { PrimaryCommanders = new[] { duplicateRoot with { Children = new[] { duplicateChild } } } };
        var invalid = new BotDoctrineDefinition
        {
            Id = definition.Id, Name = definition.Name, Presentation = definition.Presentation,
            Settings = definition.Settings, Services = definition.Services,
            Methods = definition.Methods.Select(m => m with { PolicyId = "missing.policy" }).ToArray(),
            Policies = definition.Policies, InitializationPlans = definition.InitializationPlans, Matchups = matchups
        };

        Assert.Contains(invalid.Validate(), error => error.Contains("duplicate id 'shared.node'"));
        Assert.Contains(invalid.Validate(), error => error.Contains("unknown policy"));
    }

    [Fact]
    public void BuildArtifact_RoundTripsAndRejectsPayloadTampering()
    {
        var artifact = BotDoctrineBuildService.Build(CreateValid());
        var serialized = BotDoctrineBuildService.SerializeArtifact(artifact);
        var loaded = BotDoctrineBuildService.Load(serialized);

        Assert.Equal("doctrine.test", loaded.Id);
        Assert.Equal(9, loaded.Matchups.Count);

        var tampered = artifact with { PayloadJson = artifact.PayloadJson.Replace("Test doctrine", "Altered doctrine", StringComparison.Ordinal) };
        var tamperedJson = BotDoctrineBuildService.SerializeArtifact(tampered);
        Assert.Throws<InvalidOperationException>(() => BotDoctrineBuildService.Load(tamperedJson));
    }

    [Fact]
    public async Task RuntimeResolver_RequiresTrustedMethodsControllersAndServices()
    {
        var definition = CreateValid();
        var calls = 0;
        var catalog = new BotDoctrineRuntimeCatalog();
        catalog.RegisterService("spatial.query");
        catalog.RegisterMethod(new BotDoctrineMethodRegistration(
            "method.respond-to-contact.v1", BotDoctrineMethodScope.Bot,
            (_, _) => { calls++; return ValueTask.CompletedTask; }));
        catalog.RegisterController(new BotDoctrineControllerRegistration(
            "commander.force.v1", BotDoctrineNodeKind.Commander, BotDoctrineMethodScope.Commander,
            new HashSet<string>(new[] { "spatial.query" }, StringComparer.Ordinal)));
        catalog.RegisterController(new BotDoctrineControllerRegistration(
            "unit.marine.fsm.v1", BotDoctrineNodeKind.UnitFsm, BotDoctrineMethodScope.Unit,
            new HashSet<string>(StringComparer.Ordinal)));

        var plan = BotDoctrineRuntimeResolver.Resolve(definition, Race.Terran, Race.Zerg, catalog);
        var dispatcher = new BotDoctrineEventDispatcher(plan);
        var dispatched = await dispatcher.DispatchAsync(new BotDoctrineEvent(
            "observation.enemy-spotted", "event-1", 100, new Dictionary<string, string>()));

        Assert.Equal(1, dispatched);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() =>
            BotDoctrineRuntimeResolver.Resolve(definition, Race.Terran, Race.Random, catalog));
    }

    [Fact]
    public void RuntimeResolver_ActivatesOnlyBindingsForSelectedMatchup()
    {
        var definition = CreateValid();
        definition = new BotDoctrineDefinition
        {
            Id = definition.Id, Name = definition.Name, Presentation = definition.Presentation,
            Settings = definition.Settings, Services = definition.Services, Policies = definition.Policies,
            InitializationPlans = definition.InitializationPlans, Matchups = definition.Matchups,
            Methods = definition.Methods.Concat(new[]
            {
                new BotDoctrineMethodBinding
                {
                    EventId = "observation.enemy-spotted",
                    MethodId = "method.zerg-mirror-only.v1",
                    Scope = BotDoctrineMethodScope.Commander,
                    NodeId = "Zerg.Terran.supreme"
                }
            }).ToArray()
        };

        var catalog = new BotDoctrineRuntimeCatalog();
        catalog.RegisterService("spatial.query");
        catalog.RegisterMethod(new BotDoctrineMethodRegistration(
            "method.respond-to-contact.v1", BotDoctrineMethodScope.Bot,
            (_, _) => ValueTask.CompletedTask));
        catalog.RegisterMethod(new BotDoctrineMethodRegistration(
            "method.zerg-mirror-only.v1", BotDoctrineMethodScope.Commander,
            (_, _) => ValueTask.CompletedTask));
        catalog.RegisterController(new BotDoctrineControllerRegistration(
            "commander.force.v1", BotDoctrineNodeKind.Commander, BotDoctrineMethodScope.Commander,
            new HashSet<string>(new[] { "spatial.query" }, StringComparer.Ordinal)));
        catalog.RegisterController(new BotDoctrineControllerRegistration(
            "unit.marine.fsm.v1", BotDoctrineNodeKind.UnitFsm, BotDoctrineMethodScope.Unit,
            new HashSet<string>(StringComparer.Ordinal)));

        var plan = BotDoctrineRuntimeResolver.Resolve(definition, Race.Terran, Race.Zerg, catalog);

        Assert.Single(plan.MethodBindings);
        Assert.Equal("method.respond-to-contact.v1", plan.MethodBindings[0].MethodId);
    }

    [Fact]
    public void RuntimeResolver_FailsClosedWhenRequiredServiceIsMissing()
    {
        var catalog = new BotDoctrineRuntimeCatalog();
        catalog.RegisterMethod(new BotDoctrineMethodRegistration(
            "method.respond-to-contact.v1", BotDoctrineMethodScope.Bot,
            (_, _) => ValueTask.CompletedTask));

        var error = Assert.Throws<InvalidOperationException>(() =>
            BotDoctrineRuntimeResolver.Resolve(CreateValid(), Race.Terran, Race.Terran, catalog));

        Assert.Contains("required service 'spatial.query' is not registered", error.Message);
    }
}
