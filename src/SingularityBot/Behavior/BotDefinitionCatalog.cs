using System.Text.Json;

namespace SingularityBot.Behavior;

/// <summary>
/// Compiled, versioned bot definitions. The live runner selects one of these
/// factories; it never needs to read a strategy file or load executable code.
/// Editor-facing JSON remains an import/export representation of this model.
/// </summary>
public static class BotDefinitionCatalog
{
    public const string DefaultDefinitionId = "singularitybot.terran.adaptive-marine";
    public const string DefaultModeId = "terran.marine.v1";

    public static BotDefinition CreateDefault()
    {
        const string strategyProfile = "terran.marine-economy.v1";
        return new BotDefinition
        {
            SchemaVersion = 1,
            Id = DefaultDefinitionId,
            Name = "SingularityBot — Adaptive Marine Economy",
            Race = "Terran",
            ModeId = DefaultModeId,
            StrategyProfile = strategyProfile,
            Runtime = new BotManifest
            {
                SchemaVersion = 1,
                Bot = new BotManifestBotSettings
                {
                    Race = "Terran",
                    StrategyProfile = strategyProfile,
                    DeterministicScheduling = true
                },
                Advisors = new[]
                {
                    new AdvisorManifestNode
                    {
                        Id = "military", Definition = "advisor.military.v1", Enabled = true,
                        ProcessGroup = "SingularityBot.Advisor.Military", Priority = 500,
                        Settings = Settings(new { })
                    },
                    new AdvisorManifestNode
                    {
                        Id = "economy", Definition = "advisor.economy.v1", Enabled = true,
                        ProcessGroup = "SingularityBot.Advisor.Economy", Priority = 700,
                        Settings = Settings(new
                        {
                            mineralWorkersPerNode = 2,
                            gasWorkersPerRefinery = 3,
                            miningAuthority = "Custom",
                            enableMules = true,
                            muleEnergyReserve = 50,
                            mulePolicy = "PreserveScan"
                        })
                    },
                    new AdvisorManifestNode
                    {
                        Id = "research", Definition = "advisor.research.v1", Enabled = true,
                        ProcessGroup = "SingularityBot.Advisor.Research", Priority = 400,
                        Settings = Settings(new { })
                    },
                    new AdvisorManifestNode
                    {
                        Id = "base-layout", Definition = "advisor.base-layout.v1", Enabled = true,
                        ProcessGroup = "SingularityBot.Advisor.BaseLayout", Priority = 600,
                        Settings = Settings(new { })
                    },
                    new AdvisorManifestNode
                    {
                        Id = "intelligence", Definition = "advisor.intelligence.v1", Enabled = true,
                        ProcessGroup = "SingularityBot.Advisor.Intelligence", Priority = 450,
                        Settings = Settings(new
                        {
                            preserveOrbitalScanBeforeMules = true,
                            orbitalScanEnergyReserve = 50
                        })
                    }
                }
            },
            Opening = new ProductionPlanDefinition
            {
                SchemaVersion = "0.1",
                Id = "terran.safe-marine-opening",
                Name = "Safe Marine Economy Opening",
                Race = "Terran",
                Steps = new[]
                {
                    new ProductionStepDefinition("supply-depot-1", "SupplyDepot", "SCV", 1, 0, 100,
                        Lane: ProductionLane.Buildings),
                    new ProductionStepDefinition("barracks-1", "Barracks", "SCV", 1, 0, 90,
                        DependsOn: new[] { "supply-depot-1" }, Lane: ProductionLane.Buildings),
                    new ProductionStepDefinition("marine-core", "Marine", "Barracks", 60, 0, 80,
                        DependsOn: new[] { "barracks-1" }, Lane: ProductionLane.Units)
                }
            },
            DefaultGoals = new GoalActionPlanDefinition
            {
                Version = 1,
                Id = "marine-contain-and-scout",
                Name = "Marine containment and early warning",
                DefaultRole = GoalActionRole.Engage,
                ScoutCount = 0,
                ScoutSectors = new(),
                ExcludeEnemyStartSector = true,
                StutterStepEnabled = true,
                FallbackHealthFraction = 0.35f,
                EngageVisibleThreats = true
            },
            StrategyTransitions = new[]
            {
                new StrategyTransitionDefinition(
                    "prepare-for-roach-tech",
                    "terran.defend-ground-tech.v1",
                    new[]
                    {
                        new StrategyEvidenceRequirement("enemy.structure.roach-warren", "enemy",
                            0.85f, 448, RequiresObservedFact: true)
                    },
                    new[]
                    {
                        "own.production.pipeline-healthy",
                        "own.defense.coverage-available",
                        "resources.transition-affordable"
                    },
                    0.1, 448, 896)
            },
            Ai = new BotAiPolicy
            {
                Enabled = true,
                Provider = "configured-by-host",
                Model = "configured-by-host",
                MaxOnStartCalls = 1,
                MaxInMatchCalls = 3,
                TimeoutMilliseconds = 15000,
                MinimumGameLoopSpacing = 448,
                AllowedTriggers = new[]
                {
                    "opening.review",
                    "enemy.tech.unanswered",
                    "strategic.stalemate",
                    "post-scouting.reassessment"
                }
            }
        };
    }

    private static System.Text.Json.JsonElement Settings<T>(T value) =>
        JsonSerializer.SerializeToElement(value);
}
