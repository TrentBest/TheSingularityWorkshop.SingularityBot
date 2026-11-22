using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace SingularityBot.Runner;

/// <summary>
/// Opt-in, loopback-only, read-only HTTP surface for local Bot Studio clients.
/// It never owns or controls the SC2 connection.
/// </summary>
public sealed class BotStudioBridge : IAsyncDisposable
{
    private readonly WebApplication _application;

    private BotStudioBridge(WebApplication application, string address, string token)
    {
        _application = application;
        Address = address;
        Token = token;
    }

    public string Address { get; }
    public string Token { get; }

    public static async Task<BotStudioBridge> StartAsync(SingularityBot bot, int port, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        if (port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Bridge port must be between 0 and 65535.");

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        app.MapGet("/api/health", (HttpContext context) =>
        {
            if (context.Connection.RemoteIpAddress is not { } address || !address.IsLoopback())
                return Results.NotFound();
            return Results.Ok(new { status = "ok", snapshotAvailable = bot.LatestStudioSnapshot is not null });
        });

        app.MapGet("/api/snapshot", (HttpContext context) =>
        {
            if (context.Connection.RemoteIpAddress is not { } address || !address.IsLoopback())
                return Results.NotFound();

            var authorization = context.Request.Headers.Authorization.ToString();
            var supplied = authorization.StartsWith("Bearer ", StringComparison.Ordinal)
                ? authorization["Bearer ".Length..]
                : string.Empty;
            if (!IsAuthorized(supplied, token))
                return Results.Unauthorized();

            var snapshot = bot.LatestStudioSnapshot;
            return snapshot is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : Results.Json(snapshot);
        });

        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            var address = app.Urls.Single(url => url.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
            return new BotStudioBridge(app, address, token);
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static bool IsAuthorized(string supplied, string expected)
    {
        if (string.IsNullOrEmpty(supplied) || string.IsNullOrEmpty(expected))
            return false;
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync().ConfigureAwait(false);
        await _application.DisposeAsync().ConfigureAwait(false);
    }
}
