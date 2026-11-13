using System.Net;
using System.Text;
using SingularityBot.Behavior;
using TheSingularityWorkshop.HeadlessAi;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotAiConsultationCoordinatorTests
{
    private const string RecommendationJson = """
        {
          "schemaVersion": 1,
          "decision": "recommend",
          "strategyId": "terran.counter-ground-tech.v1",
          "goalIds": ["defend.natural"],
          "evidenceIds": ["enemy.roach-warren"],
          "confidence": 0.82,
          "rationale": "Prepare defensively while preserving scouting coverage."
        }
        """;

    [Fact]
    public async Task OnStart_UsesAtMostOneCallAndReturnsValidatedRecommendation()
    {
        using var client = CreateClient(RecommendationJson);
        var coordinator = new BotAiConsultationCoordinator(
            CreateAgent(client),
            CreatePolicy(maxOnStart: 1, maxInMatch: 3));

        var result = await coordinator.ConsultOnStartAsync(ContextJson, CreateScope());

        Assert.Equal(BotAiConsultationStatus.Completed, result.Status);
        Assert.Equal("terran.counter-ground-tech.v1", result.Recommendation.StrategyId);
        Assert.Equal(0, coordinator.OnStartCallsRemaining);

        var second = await coordinator.ConsultOnStartAsync(ContextJson, CreateScope());
        Assert.Equal(BotAiConsultationStatus.BudgetExhausted, second.Status);
        Assert.Equal(1, coordinator.OnStartCallsUsed);
    }

    [Fact]
    public async Task InMatch_EnforcesTriggerSpacingAndThreeCallBudget()
    {
        using var client = CreateClient(RecommendationJson);
        var coordinator = new BotAiConsultationCoordinator(
            CreateAgent(client),
            CreatePolicy(maxOnStart: 0, maxInMatch: 3));

        var unregistered = await coordinator.ConsultInMatchAsync(
            "arbitrary.trigger", 1000, ContextJson, CreateScope());
        Assert.Equal(BotAiConsultationStatus.TriggerNotAllowed, unregistered.Status);
        Assert.Equal(0, coordinator.InMatchCallsUsed);

        var first = await coordinator.ConsultInMatchAsync(
            "enemy.tech.unanswered", 1000, ContextJson, CreateScope());
        var tooSoon = await coordinator.ConsultInMatchAsync(
            "enemy.tech.unanswered", 1100, ContextJson, CreateScope());
        var second = await coordinator.ConsultInMatchAsync(
            "enemy.tech.unanswered", 1200, ContextJson, CreateScope());
        var third = await coordinator.ConsultInMatchAsync(
            "enemy.tech.unanswered", 1400, ContextJson, CreateScope());
        var exhausted = await coordinator.ConsultInMatchAsync(
            "enemy.tech.unanswered", 1600, ContextJson, CreateScope());

        Assert.Equal(BotAiConsultationStatus.Completed, first.Status);
        Assert.Equal(BotAiConsultationStatus.TooSoon, tooSoon.Status);
        Assert.Equal(BotAiConsultationStatus.Completed, second.Status);
        Assert.Equal(BotAiConsultationStatus.Completed, third.Status);
        Assert.Equal(BotAiConsultationStatus.BudgetExhausted, exhausted.Status);
        Assert.Equal(3, coordinator.InMatchCallsUsed);
    }

    [Fact]
    public async Task Recommendation_CannotInventAnUnregisteredStrategy()
    {
        const string invalid = """
            {
              "schemaVersion": 1,
              "decision": "recommend",
              "strategyId": "invented.strategy",
              "goalIds": [],
              "evidenceIds": ["enemy.roach-warren"],
              "confidence": 0.9,
              "rationale": "Try something new."
            }
            """;
        using var client = CreateClient(invalid);
        var coordinator = new BotAiConsultationCoordinator(
            CreateAgent(client),
            CreatePolicy(maxOnStart: 1, maxInMatch: 0));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            coordinator.ConsultOnStartAsync(ContextJson, CreateScope()));
        Assert.Equal(1, coordinator.OnStartCallsUsed);
    }

    [Fact]
    public async Task DisabledPolicy_DoesNotContactProviderOrConsumeBudget()
    {
        using var client = CreateClient("not-json");
        var coordinator = new BotAiConsultationCoordinator(
            CreateAgent(client),
            new BotAiPolicy { Enabled = false, MaxOnStartCalls = 0, MaxInMatchCalls = 0 });

        var result = await coordinator.ConsultOnStartAsync(ContextJson, CreateScope());

        Assert.Equal(BotAiConsultationStatus.Disabled, result.Status);
        Assert.Equal(0, coordinator.OnStartCallsUsed);
    }

    private const string ContextJson = """{"gameLoop":1000,"ourState":{"minerals":250},"enemyState":{"lastSeenTech":"RoachWarren"}}""";

    private static BotAiConsultationScope CreateScope() => new(
        new[] { "terran.counter-ground-tech.v1" },
        new[] { "defend.natural" },
        new[] { "enemy.roach-warren" });

    private static BotAiPolicy CreatePolicy(int maxOnStart, int maxInMatch) => new()
    {
        Enabled = true,
        Provider = "fixture",
        Model = "fixture-model",
        MaxOnStartCalls = maxOnStart,
        MaxInMatchCalls = maxInMatch,
        TimeoutMilliseconds = 5000,
        MinimumGameLoopSpacing = 200,
        AllowedTriggers = new[] { "enemy.tech.unanswered" }
    };

    private static HeadlessAiAgent CreateAgent(HttpClient client)
    {
        var profile = new HeadlessAiProfile(
            "singularitybot-test",
            new Uri("https://fixture.invalid/strategic-consultation"),
            timeout: TimeSpan.FromSeconds(5));
        return new HeadlessAiAgent(client, profile, new FixtureAdapter());
    }

    private static HttpClient CreateClient(string response) =>
        new(new FixtureHandler(response));

    private sealed class FixtureHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }

    private sealed class FixtureAdapter : IHeadlessAiAdapter
    {
        public string Id => "fixture-json";

        public HttpRequestMessage CreateRequest(HeadlessAiProfile profile, HeadlessAiInput input) =>
            new(HttpMethod.Post, profile.Endpoint)
            {
                Content = new StringContent(input.Content, Encoding.UTF8, "text/plain")
            };

        public async ValueTask<HeadlessAiOutput> ReadResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken = default)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new HeadlessAiOutput(content, response.StatusCode, "application/json");
        }
    }
}
