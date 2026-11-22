# SingularityBot Runner

The Runner is the local process entry point for SingularityBot. Its ordinary launch path is unchanged:

```powershell
dotnet run --project src/SingularityBot.Runner/SingularityBot.Runner.csproj
```

## Optional Bot Studio bridge

Add `--studio-bridge` to expose the bot's latest immutable telemetry snapshot over HTTP:

```powershell
dotnet run --project src/SingularityBot.Runner/SingularityBot.Runner.csproj -- --studio-bridge
```

The bridge:

- Binds only to IPv4 loopback (`127.0.0.1`); it is not a LAN or remote-control service.
- Generates a fresh 256-bit bearer token for each process and prints it to the local console.
- Exposes `GET /api/health` for local process/telemetry readiness.
- Exposes `GET /api/snapshot` only when the caller supplies `Authorization: Bearer <token>`.
- Exposes `GET /api/events?afterSequence=N&limit=100` with the same bearer-token requirement. The cursor returns ordered structured events after `N`; `limit` is 1–256.
- Keeps a fixed-capacity 256-event journal; older entries are evicted. Events currently cover match lifecycle, observed SC2 action results, missing action results, and action errors. A submitted action is not reported as accepted until SC2 returns its per-action result.
- Returns `400 Bad Request` when the event `limit` is outside the supported range.
- Returns `503 Service Unavailable` for an authenticated snapshot request until the bot publishes its first observation.
- Serves read-only telemetry. There are no match-start, action, script, reflection, or arbitrary-command endpoints.
- Runs beside the existing SC2 connection. Stopping or disconnecting a client does not stop or pause the bot.

The default port is `43127`. Choose another local port with `--studio-bridge-port=PORT` (requires `--studio-bridge`):

```powershell
dotnet run --project src/SingularityBot.Runner/SingularityBot.Runner.csproj -- --studio-bridge --studio-bridge-port=43128
```

Treat the printed bearer token as a local secret. Do not paste it into issues, source control, screenshots, or remote logs. The bridge is intentionally HTTP-only because it is bound to loopback; it is not intended to be exposed through a reverse proxy or tunnel.

The bridge is opt-in. Without `--studio-bridge`, the Runner does not start an HTTP listener.
