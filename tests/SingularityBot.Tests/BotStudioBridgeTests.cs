using SingularityBot.Runner;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotStudioBridgeTests
{
    [Fact]
    public async Task Bridge_IsLoopbackOnlyAndRequiresBearerTokenForSnapshot()
    {
        await using var bridge = await BotStudioBridge.StartAsync(new SingularityBot.SingularityBot(), 0);
        using var client = new HttpClient { BaseAddress = new Uri(bridge.Address) };

        using var health = await client.GetAsync("/api/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, health.StatusCode);

        using var missingToken = await client.GetAsync("/api/snapshot");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, missingToken.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-the-token");
        using var wrongToken = await client.GetAsync("/api/snapshot");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, wrongToken.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bridge.Token);
        using var authorizedButNotReady = await client.GetAsync("/api/snapshot");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, authorizedButNotReady.StatusCode);
    }
}
