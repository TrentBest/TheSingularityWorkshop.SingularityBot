using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotManifestTests
{
    [Fact]
    public void EmbeddedDefaultManifestLoadsAndSelectsTheFiveInitialAdvisors()
    {
        var manifest = BotManifest.LoadEmbeddedDefault();

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.True(manifest.Bot.DeterministicScheduling);
        Assert.Equal(
            new[] { "military", "economy", "research", "base-layout", "intelligence" },
            manifest.Advisors.Select(advisor => advisor.Id).ToArray());
        Assert.Equal(0, manifest.Validate().Count);
    }

    [Fact]
    public void ManifestRejectsDuplicateProcessGroupOwnership()
    {
        var manifest = new BotManifest
        {
            SchemaVersion = 1,
            Bot = new BotManifestBotSettings
            {
                Race = "Terran",
                StrategyProfile = "terran.test.v1",
                DeterministicScheduling = true
            },
            Advisors =
            [
                new AdvisorManifestNode
                {
                    Id = "economy",
                    Definition = "advisor.economy.v1",
                    Enabled = true,
                    ProcessGroup = "shared",
                    Priority = 100
                },
                new AdvisorManifestNode
                {
                    Id = "military",
                    Definition = "advisor.military.v1",
                    Enabled = true,
                    ProcessGroup = "shared",
                    Priority = 100
                }
            ]
        };

        Assert.Contains(manifest.Validate(), error => error.Contains("more than one owner", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestRejectsUnknownCompiledAdvisorDefinitions()
    {
        var manifest = new BotManifest
        {
            SchemaVersion = 1,
            Bot = new BotManifestBotSettings
            {
                Race = "Terran",
                StrategyProfile = "terran.test.v1",
                DeterministicScheduling = true
            },
            Advisors =
            [
                new AdvisorManifestNode
                {
                    Id = "experimental",
                    Definition = "user.supplied.code",
                    Enabled = true,
                    ProcessGroup = "experimental",
                    Priority = 1
                }
            ]
        };

        Assert.Contains(manifest.Validate(), error => error.Contains("not a compiled advisor definition", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestRejectsNondeterministicScheduling()
    {
        var manifest = new BotManifest
        {
            SchemaVersion = 1,
            Bot = new BotManifestBotSettings
            {
                Race = "Terran",
                StrategyProfile = "terran.test.v1",
                DeterministicScheduling = false
            },
            Advisors =
            [
                new AdvisorManifestNode
                {
                    Id = "economy",
                    Definition = "advisor.economy.v1",
                    Enabled = true,
                    ProcessGroup = "economy",
                    Priority = 1
                }
            ]
        };

        Assert.Contains(manifest.Validate(), error => error.Contains("deterministicScheduling", StringComparison.Ordinal));
    }
}
