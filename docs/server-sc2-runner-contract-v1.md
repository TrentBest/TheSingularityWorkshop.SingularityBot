# ServerSC2 Runner Contract v1

**Status:** runner-side protocol primitives and source inspection are implemented; three-client live SC2 topology is not yet verified.

**Scope:** this document defines the narrow boundary between the reusable SingularityBot/SC2 client and the ServerSC2 match host. It does not add a ServerSC2 dependency or move host policy into the bot.

## 1. Source-backed current behavior

| Concern | Current implementation |
|---|---|
| Runner entry point | `src/SingularityBot.Runner/Program.cs` delegates to `SingularityBot.Program.Run(args)`. |
| Local process launch | `GameConnection.RunSinglePlayer` reads SC2 `ExecuteInfo.txt`, calls `StartSC2Instance(5678)`, connects to loopback, creates a local participant-vs-computer game, joins as a participant, then runs the bot. |
| Process ownership | Local single-player calls `Process.Start` inside `StartSC2Instance`, but discards the returned `Process`; there is no retained process handle, process-tree supervision, or cancellation contract. |
| Host-launched process attachment | `RunLadder` calls `Connect(gamePort)` and joins an already-running SC2 process. It does not launch that process. This is the reusable attach path ServerSC2 should use. |
| Game creation | `CreateLadderGame` creates two Participant slots on the connected SC2 client. The `--CreateGame` ladder path then joins that same client as one participant. The second participant joins through another SC2 client. |
| Participant join | `JoinGameLadder` sends `RequestJoinGame.Race`, network port fields and interface options, and returns `ResponseJoinGame.PlayerId`. |
| Observer join | No observer join method or observer loop existed before this change. The pinned schema has `RequestJoinGame.ObservedPlayerId` as the observer participation oneof. |
| Readiness | A successful JoinGame response returns a player ID for participants. `SC2_JOINED` is emitted as a stdout diagnostic. There is no separate transport-ready / in-game-ready state machine. |
| Terminal result | `GameConnection.Run` polls `Response.Status` and sees `Ended` / `Quit`; it calls `OnEnd`. It previously indexed `PlayerResult[playerId - 1]`, incorrectly assuming repeated-field order. |
| Replay | The pinned protocol contains `RequestSaveReplay` → `ResponseSaveReplay.Data` and `RequestReplayInfo.ReplayData` → `ResponseReplayInfo`. The runner did not expose these operations before this change. |
| Raw protocol capture | `ProtobufProxy` parses WebSocket response bytes into generated protobuf messages and discards the original response frame. The runner does not currently retain byte-exact observation frames. |

### Pinned protocol facts verified in this repository

- `RequestJoinGame` has a oneof: `Race` (participant) or `ObservedPlayerId` (observer).
- `ResponseJoinGame.PlayerId` is the participant join response field. Do not use it as a participant assignment identity by itself.
- `ResponseObservation.PlayerResult` is a repeated collection of `PlayerResult` records. Each record has both `PlayerId` and `Result`. The generated schema says this field is populated only if the game ended during that step.
- `Status.Ended` means the game has ended and the client is ready for a new game; `Status.Quit` means the application is shutting down. Quit is not a successful terminal match result.
- `Result` includes Victory, Defeat, Tie and Undecided. Undecided must not satisfy ServerSC2's completion gate.
- `ResponseGameInfo.PlayerInfo` includes `PlayerId`, `Type`, `RaceRequested` and `RaceActual`; the generated comment says `RaceActual` is only populated for your player or when watching a replay.
- `RequestSaveReplay` is an empty request message; `ResponseSaveReplay.Data` contains replay bytes.
- `RequestReplayInfo` accepts replay bytes or a replay path; `ResponseReplayInfo` includes map name, player info/result metadata, game duration, game version, data version/build, base build and an error field.
- The generated protocol comment for observer player perspective says player ID 0 means Everyone. That comment is not a live proof that the exact three-client topology accepts `ObservedPlayerId=0` on `RequestJoinGame`; the integration test must establish that behavior against the pinned SC2 build.

## 2. Process/session ownership

ServerSC2 is the process and lifecycle authority. It launches and supervises three distinct SC2 processes:

1. Participant A SC2 process, connected to its own participant runner/client.
2. Participant B SC2 process, connected to its own participant runner/client.
3. Observer SC2 process, connected to a host-controlled observer client/session.

The first participant's SC2 process may issue CreateGame and then join as Participant A, matching the existing `--CreateGame` path. Participant B and the observer join from their own SC2 processes. This is a three-client topology, not a second participant disguised as an observer. Whether this exact topology works on the target SC2 installation remains a live-test gate.

The host owns all three process handles, process trees, port allocations, cancellation, startup/stop deadlines, and cleanup. The participant runner and observer client attach to already-running processes; neither is allowed to silently spawn a replacement SC2 process in host mode.

### Proposed host launch record

Version the host-owned assignment and process configuration. Suggested minimum shape:

```json
{
  "contractVersion": "server-sc2.runner.v1",
  "matchId": "host-generated-match-id",
  "sessionId": "host-generated-session-id",
  "map": { "identity": "trusted-map-identity", "path": "host-local-map-path" },
  "sc2": { "build": "pinned-build", "listenAddress": "127.0.0.1" },
  "participants": [
    { "assignmentId": "immutable-assignment-a", "buildId": "build-a", "gamePort": 5000, "startPort": 5100 },
    { "assignmentId": "immutable-assignment-b", "buildId": "build-b", "gamePort": 5200, "startPort": 5300 }
  ],
  "observer": { "sessionId": "immutable-observer-session", "gamePort": 5400, "startPort": 5500 }
}
```

This is a proposed contract example, not a claim that these example ports or IDs are currently configured. Allocate non-overlapping port blocks per SC2 process and validate availability before launch.

Each process is started with a distinct SC2 `-listen` / `-port` pair. The runner's existing `Connect(port)` attaches to `127.0.0.1:<port>/sc2api`. ServerSC2 should bind only to loopback unless a separately reviewed deployment design requires otherwise.

## 3. Join, readiness and identity

### Participant sequence

1. ServerSC2 launches the participant SC2 process and retains its process handle.
2. Participant runner connects to its assigned `gamePort`.
3. The designated creator runner sends CreateGame with exactly two Participant slots.
4. Each participant runner sends JoinGame as a participant using its configured race.
5. The host records the returned non-zero `ResponseJoinGame.PlayerId` against the immutable assignment ID and the exact process/session that received it.
6. ServerSC2 independently verifies the participant roster from the observer's `ResponseGameInfo.PlayerInfo` / in-game observations before declaring both participants correlated.

### Observer sequence

1. ServerSC2 launches the observer SC2 process separately and retains its process handle.
2. ServerSC2's observer client connects to its assigned `gamePort`.
3. The observer client sends `RequestJoinGame.ObservedPlayerId` with an explicit perspective selection; do not silently substitute a default.
4. The observer client retains the complete `ResponseJoinGame` and then polls observation/status.
5. Observer transport-ready means WebSocket connected and Ping succeeded. Observer joined means JoinGame returned successfully. Observer in-game-ready means the observer session reports `Status.InGame` and returns a non-null observation. These are distinct states.

### Correlation rules

- `assignmentId` and `sessionId` are generated and owned by ServerSC2; they are immutable for the match.
- A participant's SC2 `PlayerId` is bound to its assignment only from that assignment's own JoinGame response and then checked against the observer-visible roster.
- Require exactly two distinct participant player IDs. Reject zero, duplicate, unknown, or conflicting IDs.
- The observer is identified by its host-owned observer session ID and process identity, not by pretending it is a third participant.
- `SC2_JOINED` stdout text may assist diagnostics/readiness correlation but is not authority. The host must also verify the join response and observer roster.
- Do not infer that a particular player ID is Everyone. The observer perspective is a separate protocol field and requires a live probe.

## 4. Terminal result and replay contract

The observer is the only source used by ServerSC2 to authorize match completion.

1. Poll the observer's SC2 API response until `Status.Ended`. Treat `Quit`, disconnect, timeout, or missing observation as incomplete/failure, not a completed match.
2. Require a non-null `ResponseObservation` with exactly one terminal `PlayerResult` for each of the two mapped participant IDs.
3. Resolve by `PlayerResult.PlayerId`, never repeated-field index. Reject duplicate, missing, unknown, or undecided results.
4. Preserve the original typed response and a provenance record: match/session IDs, SC2 process/session identity, SC2 build/version, observed status, capture time, participant mapping, and raw-frame digest. ServerSC2 should capture the original WebSocket protobuf response frame before parsing if byte-exact raw evidence is required; the current runner proxy does not preserve that frame.
5. Once terminal evidence is present, issue `RequestSaveReplay` on the observer session and require non-empty `ResponseSaveReplay.Data`.
6. Persist replay bytes through ServerSC2's bounded artifact store; calculate SHA-256 over the exact persisted bytes and verify the digest after write.
7. Send `RequestReplayInfo` with the captured replay bytes. Require a successful `ResponseReplayInfo`; compare map identity, game version/build and the per-player result metadata against the observer terminal result and immutable assignment map.
8. Only after both live terminal results and replay metadata are internally consistent may ServerSC2 transition to Completed. A runner's `SC2_RESULT` text is diagnostic only and cannot advance lifecycle state.

### Proposed immutable evidence envelope

The host's evidence record should be versioned and include at least:

- `contractVersion`, `matchId`, `sessionId`, capture timestamp and terminal observation timestamp.
- SC2 client/build/version and map identity.
- For each participant: immutable assignment ID, build ID and digest, SC2 player ID, requested/actual race where available, authoritative result.
- Observer process/session identity and the raw terminal response frame digest (plus bounded raw bytes or an immutable artifact reference).
- Replay artifact identity/location, byte length, SHA-256, and replay-info metadata (map, version/build, duration, per-player IDs/results).
- Validation outcome and stable diagnostics for every rejected/missing/contradictory field.

The runner does not own this envelope, assignment authority, artifact persistence, or the completion gate. Those remain ServerSC2 responsibilities.

## 5. Runner-side changes in this revision

The reusable SC2 client now exposes these primitives on `GameConnection`:

- `JoinObserver(observedPlayerId, startPort)`: observer JoinGame with an explicit perspective and its own port block. It attaches to an already-running SC2 process and does not launch one.
- `Observe()`: one raw typed `Response` containing status and observation.
- `SaveReplay()`: requests and returns non-empty replay bytes.
- `GetReplayInfo(replayData)`: requests replay metadata and rejects protocol-level errors.

The existing participant terminal callback now resolves its result by explicit `PlayerResult.PlayerId` using `Sc2PlayerResultResolver`, rejecting missing or duplicate matches. This fixes the old positional-index assumption. It does not make participant stdout markers authoritative and does not make participant results the ServerSC2 completion source.

No ServerSC2 dependency, host assignment policy, process supervisor, artifact store, or completion gate was added to SingularityBot.

## 6. Capability status

| Capability | Status |
|---|---|
| Attach runner to a host-launched participant SC2 process | Supported by existing `Connect` / `RunLadder` path |
| Participant JoinGame and returned player ID | Supported |
| Create two participant slots | Supported by existing `CreateLadderGame` |
| Attach an independent observer client | Protocol primitive added; not live verified |
| Observer JoinGame request | Added as `JoinObserver`; exact perspective/topology not live verified |
| Poll observer status/observation | One-shot `Observe` primitive added; host polling loop remains host-owned |
| Terminal result correlation by explicit PlayerId | Supported by new resolver; tests added |
| Replay save bytes | Primitive added; not live verified |
| Replay metadata from replay bytes | Primitive added; not live verified |
| Exact original WebSocket response-frame capture in runner | Unsupported; proxy currently parses and discards original frame |
| ServerSC2 process-tree lifecycle, timeout, cancellation and cleanup | Host responsibility; not implemented in this repository |
| Immutable assignment evidence, replay persistence/hash and fail-closed completion | Host responsibility; not implemented in this repository |
| Three-client live observer topology and replay cross-check | Not yet proven |

## 7. Deterministic and live validation

### Deterministic tests

- Resolve participant results correctly when repeated-field order differs from player-ID order.
- Reject missing and duplicate player IDs and player ID zero.
- Add host-side contract tests for exact two-participant mapping, observer-session separation, result validity, replay metadata agreement, artifact digest, size bounds, and all fail-closed conditions.
- Keep fake-transport tests labeled as contract/unit tests; they are not live SC2 proof.

### Opt-in live probe

Against the pinned SC2 installation/build:

1. Record exact SC2 executable version/build and map identity.
2. Have ServerSC2 launch three independent SC2 processes on unique loopback ports and retain their process handles.
3. Participant A creates a trusted local map game with two Participant slots and joins. Participant B joins from its own SC2 process.
4. Observer joins from its own SC2 process using an explicit observed-player perspective.
5. Verify the observer sees `Status.InGame`, exactly the two participant IDs, and the intended perspective.
6. Run a deterministic short match to terminal state; verify observer `Status.Ended` and two mapped non-Undecided results.
7. Save replay from observer, request replay info from bytes, compare map/build/player results, persist and hash the replay.
8. Repeat with observer disconnect, bad perspective, missing/duplicate player result, replay-save failure and timeout; all must remain non-completed.
9. Record logs, raw response evidence, replay digest, process exit codes and cleanup results.

A passing fake test is not evidence that this topology works. Do not advertise three-client support until this live probe passes.

## 8. Remaining decision / blocker

No product decision is required to implement the runner primitives. The unresolved technical gate is whether the pinned SC2 build permits a separately launched observer client to join the exact match created by Participant A while Participant B is already joined, and what observer perspective value yields full-match visibility. This requires the opt-in live probe. If it fails, keep the completion gate closed and investigate a separately controlled host-side observation connection; do not fall back to stdout markers or a two-participant-only topology.
