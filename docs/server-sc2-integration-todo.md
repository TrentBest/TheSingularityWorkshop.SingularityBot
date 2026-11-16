# ServerSC2 Integration — Living To Do

**Status:** active planning baseline  
**Last reconciled:** 2026-11-16  
**Working branch:** `development`  
**Scope:** complete the SingularityBot runner/client side of the ServerSC2 integration and track the cross-repository proof needed to establish a trustworthy hosted match lifecycle.

This is the operational checklist for the ServerSC2 workstream. Keep it synchronized with source, tests, CI and live-match evidence. A checked box means the stated acceptance evidence exists; it does not mean that a neighboring phase or the overall integration is complete.

## Mission and definition of done

Provide ServerSC2 with a reusable, host-attachable SingularityBot runner and a verifiable integration boundary for a match with **two independent participant SC2 processes plus one separately controlled observer SC2 process**.

The integration is complete only when:

- ServerSC2 can launch, identify, supervise, stop and clean up all three SC2 processes and their runner/client sessions.
- Each participant is immutably correlated to its host assignment and SC2 player ID; the observer is a separate host-owned session.
- The observer can join the intended match and reliably observe the complete participant roster and terminal state on the pinned SC2 build.
- Terminal results are resolved by explicit player identity, not repeated-field position or stdout text.
- The observer can save a replay; the host persists bounded replay bytes, verifies SHA-256, requests replay metadata and cross-checks it against the live terminal result and participant mapping.
- Failure, timeout, disconnect, malformed evidence, missing results, replay mismatch and cleanup failure all fail closed.
- Deterministic tests, exact-head CI and a recorded opt-in live probe support the claims. Fake transports and unit tests are never presented as live SC2 proof.
- The current contract and operational runbook accurately describe the tested behavior.

**Not a completion criterion:** a green build alone, runner log markers, participant-only results, an unvalidated replay, or an observer topology that has not been exercised against the pinned SC2 installation.

## Current verified baseline

The companion [ServerSC2 Runner Contract v1](server-sc2-runner-contract-v1.md) records source-backed behavior and proposed host responsibilities.

| Capability | Baseline | Remaining proof/work |
|---|---|---|
| Attach participant runner to an already-running SC2 process | Supported through `Connect` / `RunLadder` | Exercise through ServerSC2's actual launch path |
| Create two participant slots | Existing ladder create-game path | Verify creator/joiner ordering in three-process probe |
| Participant JoinGame and returned player ID | Supported | Host must bind response to immutable assignment and cross-check observer roster |
| Independent observer attach/join | `JoinObserver(observedPlayerId, startPort)` primitive added | Exact perspective and three-client topology not live verified |
| One-shot typed observation/status | `Observe()` primitive added | Host polling, readiness transitions and terminal capture are host responsibilities |
| Participant result lookup by explicit PlayerId | Resolver and regression coverage added | Observer-authoritative host result gate remains to prove |
| Replay save and replay-info request | `SaveReplay()` and `GetReplayInfo(replayData)` primitives added | Not live verified; host persistence and cross-check remain |
| Exact original WebSocket response frame capture | Not supported by current proxy path | Decide whether host-side capture or a narrow runner transport hook is required |
| Process-tree supervision, cancellation, deadlines and cleanup | ServerSC2 responsibility | Not implemented in this repository |
| Immutable evidence envelope, bounded artifact store, digest and completion gate | ServerSC2 responsibility | Not implemented in this repository |
| End-to-end three-client match and replay validation | Not proven | Blocking live acceptance gate |

Source paths and protocol facts are documented in the contract. Reinspect them before implementation; this table is a planning snapshot, not a substitute for current source review.

## Cross-repository reconciliation — 2026-11-16

**ServerSC2 development checkpoint:** `9c422d5390c06b4c31c72fa0ce84c4518a43f3db` (CI run [#238](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/actions/runs/43639841768) passed). The host-side issue remains open: [ServerSC2 #1 — host-authoritative SC2 match results and replay evidence](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/issues/1).

Verified host-side state at that checkpoint:

- `Sc2MatchEvidence` and `Sc2MatchEvidenceValidator` exist, with identity, terminal status, participant/player mapping, replay size/digest and replay metadata checks.
- The completion gate accepts validator-produced evidence, not caller-supplied booleans. Production `Sc2ProcessSessionRuntime.GetStatusAsync` still calls it with `evidence: null`, so production cannot report Completed.
- Production `Sc2ProcessSessionRuntime` still launches exactly two participant SC2 processes and two runner processes. It has no production observer controller/session, no observer terminal polling, no replay-save/ReplayInfo producer, and no persisted evidence envelope.
- The isolated `tools/sc2-probe` has a three-process setup candidate and fake/protocol preflight tests. CI does not launch SC2; live observer perspective, isolation, terminal results and replay remain unproven.
- ServerSC2's CI workflow is `.github/workflows/ci.yml`, job `build-and-check`; its contract-check JSON artifact is `server-sc2-contract-check-results`. The current workflow evidence is deterministic contract/preflight evidence, not live SC2 proof.

Runner source anchors verified on this repository's current `development` head:

- [Runner executable forwarding](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/src/SingularityBot.Runner/Program.cs#L1-L18).
- [Program dispatch to RunLadder](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/src/SingularityBot/Program.cs#L75-L93).
- [Host attach path and participant join](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/src/SC2API.CSharp/GameConnection.cs#L166-L190) and [RunLadder / CreateGame host sequence](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/src/SC2API.CSharp/GameConnection.cs#L380-L407).
- [Observer, typed observation and replay primitives](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/src/SC2API.CSharp/GameConnection.cs#L214-L282).
- [Player-ID result resolver](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/src/SC2API.CSharp/Sc2PlayerResultResolver.cs) and [resolver regression tests](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/blob/development/tests/SingularityBot.Tests/Sc2PlayerResultResolverTests.cs).

**Contract discrepancy to resolve before Phase 1 can exit:** the runner contract and current runner implementation assign CreateGame to Participant A, which then joins as a participant; Participant B and the observer join from their own SC2 processes. ServerSC2 issue #1's coordination comments also selected that participant-hosted sequence. However, the current [ServerSC2 observer-host design](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/blob/development/docs/OBSERVER_HOST_DESIGN.md) still describes a candidate sequence where the dedicated observer/host creates the game and then joins as observer. The issue comment is the latest explicit coordination decision, but the design document must be reconciled and the chosen sequence still requires live proof. Do not wire production topology from the stale alternative.

**Next cross-repository action:** reconcile the ServerSC2 design/runbook with the participant-A CreateGame sequence and freeze the exact versioned launch/join contract. Then extend the isolated live probe to prove the three-client join and PortSet/perspective behavior on a pinned local SC2 build before any production runtime topology change.

## Phase 0 — Keep the plan and contract truthful

- [x] Record the runner/host boundary and current capability gaps in [Runner Contract v1](server-sc2-runner-contract-v1.md).
- [x] Distinguish supported runner primitives from unverified live topology.
- [x] Reconcile this checklist against the current ServerSC2 repository issue/implementation for the 2026-11-16 checkpoint (see cross-repository reconciliation below).
- [x] Add source links/line anchors for the runner capabilities verified in this checkpoint; add further anchors when implementation changes.
- [ ] Record the pinned SC2 executable version, protocol/schema revision, map identity and test environment used for the live probe.
- [ ] Keep a dated evidence record for each live probe; do not rewrite a failed experiment as if it passed.
- [ ] Keep README, docs index, roadmap and this checklist consistent when status changes.

**Exit gate:** all status claims are source-backed, dated where environment-dependent, and clearly separated into implemented, tested, CI-verified and live-verified.

## Phase 1 — Freeze the runner/host contract

- [ ] Review the proposed `server-sc2.runner.v1` launch record with the ServerSC2 owner.
- [ ] Confirm required fields: contract version, match/session IDs, map identity, pinned SC2 build, participant assignment/build identities, process ports and observer session identity.
- [ ] Specify validation for missing, duplicate, malformed and conflicting IDs/ports before any process starts.
- [ ] Specify participant and observer connection configuration without adding a ServerSC2 dependency to SingularityBot.
- [ ] Specify event/state meanings: process-started, transport-ready, joined, in-game-ready, terminal-observed, replay-saved, stopped, exited, timed-out and failed.
- [ ] Define which component owns every state transition; ServerSC2 remains lifecycle authority.
- [ ] Define timeout/cancellation propagation and idempotent stop semantics.
- [ ] Define bounded output/diagnostic behavior and stable failure codes.
- [ ] Confirm loopback-only SC2 API binding unless a separately reviewed deployment requirement says otherwise.
- [ ] Version any incompatible contract changes rather than silently changing v1 semantics.

**Exit gate:** both repositories agree on a minimal versioned contract and ownership table; no host policy or assignment authority leaks into the reusable bot runner.

## Phase 2 — Verify and harden participant attachment

- [x] Trace `src/SingularityBot.Runner/Program.cs` → `SingularityBot.Program.Run` → `GameConnection.RunLadder` → `Connect(gamePort)` on current source.
- [ ] Verify host mode never starts a replacement SC2 process implicitly.
- [ ] Verify the runner attaches only to the assigned endpoint and rejects invalid/unavailable ports cleanly.
- [ ] Verify participant JoinGame sends the expected race, interface options and network ports.
- [ ] Preserve the full JoinGame response and surface the returned non-zero player ID to the host through a typed contract, not parsed stdout.
- [ ] Ensure the host binds that player ID to the exact immutable assignment and process/session that received the response.
- [ ] Test duplicate player IDs, player ID zero, wrong assignment, wrong endpoint, join rejection, connection loss and startup timeout.
- [ ] Verify participant callbacks and shutdown do not dispose or terminate a process owned by ServerSC2.
- [ ] Document the participant creator/joiner sequence and its constraints.

**Exit gate:** both participants can attach to host-launched processes and return typed, correlatable join evidence without taking process ownership away from ServerSC2.

## Phase 3 — Prove the independent observer topology

- [ ] Trace and review `JoinObserver(observedPlayerId, startPort)` against the pinned generated SC2 protocol.
- [ ] Confirm the exact meaning and accepted value of `ObservedPlayerId`; do not infer that zero means full-map/Everyone from a comment alone.
- [ ] Verify observer runs in its own SC2 process and client session, with unique ports and a retained host process handle.
- [ ] Verify observer JoinGame response and preserve it as typed evidence.
- [ ] Define and implement the observer readiness progression: process started → transport connected/Ping → joined → `Status.InGame` with non-null observation.
- [ ] Verify the observer sees exactly the two expected participant IDs and no unexpected participant.
- [ ] Verify the observer perspective exposes the visibility required for the match contract without leaking or assuming hidden information.
- [ ] Test wrong perspective, observer join rejection, observer disconnect before/after readiness, empty observation and participant roster mismatch.
- [ ] Keep observer process/session identity separate from participant identity throughout all events and logs.
- [ ] Record the exact SC2 build, map, ports, join sequence and raw diagnostic output from the successful live probe.

**Exit gate:** the pinned SC2 build accepts the three-client topology and the observer demonstrably sees the intended match and both participants. Until then, three-client support remains unverified.

## Phase 4 — Make readiness and lifecycle signals unambiguous

- [ ] Define typed runner outputs for transport-ready, participant-joined, observer-joined and in-game-ready.
- [ ] Ensure stdout markers such as `SC2_JOINED` are diagnostic only.
- [ ] Ensure `Status.Ended` is distinguished from `Status.Quit`, transport loss and process exit.
- [ ] Ensure a successful JoinGame response is not treated as proof that the match is in progress.
- [ ] Define bounded polling cadence, deadline behavior and cancellation behavior for observer status.
- [ ] Define race-safe handling when terminal status, disconnect and cancellation occur close together.
- [ ] Test repeated status, null observation, transient connection failure, terminal status without results and late process exit.
- [ ] Document which component emits, stores and consumes each lifecycle event.

**Exit gate:** ServerSC2 can make deterministic lifecycle decisions from typed evidence and host-owned process state, never from log text alone.

## Phase 5 — Establish authoritative terminal result evidence

- [ ] Confirm the observer response type and exact terminal observation field from the pinned protocol.
- [ ] Require observer status `Ended`; treat `Quit`, disconnect, timeout and missing observation as incomplete/failure.
- [ ] Require exactly one result for each of the two mapped participant player IDs.
- [ ] Resolve every result by `PlayerResult.PlayerId`, never by repeated-field index.
- [ ] Reject zero, unknown, duplicate, missing and `Undecided` player results.
- [ ] Cross-check the observer-visible roster against the immutable assignment-to-player-ID mapping.
- [ ] Preserve the typed terminal response plus capture time, SC2 build, map, process/session identity and correlation metadata.
- [ ] Decide and implement raw evidence provenance: capture original WebSocket response bytes before protobuf parsing, or document an explicitly accepted typed-message-plus-digest alternative.
- [ ] If byte-exact evidence is required, implement the smallest reusable transport hook without adding host-specific policy to SingularityBot.
- [ ] Add deterministic resolver and host-gate tests for every missing, duplicate, unknown, contradictory and out-of-order case.
- [ ] Ensure runner `SC2_RESULT` text remains diagnostic and cannot complete a match.

**Exit gate:** the host receives a complete, identity-correlated observer terminal result and rejects every incomplete or contradictory case.

## Phase 6 — Capture and validate replay evidence

- [ ] Call `SaveReplay()` only after authoritative observer terminal evidence is available.
- [ ] Require non-empty replay bytes and bounded request duration/size.
- [ ] Define replay-save failure, empty payload, timeout and observer-disconnect behavior as non-completed outcomes.
- [ ] Hand replay bytes to ServerSC2 without unbounded in-memory duplication.
- [ ] Persist bytes in the host's bounded artifact store with immutable match/session association.
- [ ] Calculate SHA-256 over the exact persisted bytes and verify it after write/readback.
- [ ] Call `GetReplayInfo(replayData)` and reject protocol errors or missing metadata.
- [ ] Cross-check replay map identity, SC2 version/build, duration and participant IDs/results against terminal evidence and immutable assignments.
- [ ] Reject mismatched map/build/player/result metadata and corrupted/truncated replay bytes.
- [ ] Record replay artifact ID/location, byte length, digest, metadata and validation outcome in the immutable evidence envelope.
- [ ] Add deterministic tests for size bounds, digest mismatch, replay-info error, metadata mismatch and persistence failure.
- [ ] Run the replay flow in the pinned live three-client probe and retain the artifact digest.

**Exit gate:** a completed match has both valid observer terminal results and a persisted, digest-verified replay whose metadata agrees with the same match.

## Phase 7 — Process supervision, cleanup and failure recovery

- [ ] ServerSC2 retains process handles for both participant SC2 processes and the observer process.
- [ ] Define process-tree ownership and termination semantics for graceful stop, abort, timeout and host shutdown.
- [ ] Define cleanup ordering for runner sessions, WebSockets, SC2 processes, temporary files and reserved ports.
- [ ] Make cleanup idempotent and safe when only a subset of processes started.
- [ ] Capture process exit codes and distinguish normal exit from forced termination.
- [ ] Ensure cancellation reaches all three sessions and does not leave orphaned SC2 processes.
- [ ] Define bounded cleanup deadline and escalation policy.
- [ ] Test failures at every startup/join/readiness/terminal/replay stage, including simultaneous failures.
- [ ] Verify no process, port lease, temp artifact or session survives a completed cleanup.
- [ ] Record cleanup evidence with process IDs, exit codes, timestamps and outstanding-resource count.
- [ ] Run repeated live matches to detect leaked processes, ports, files or stale session state.

**Exit gate:** normal completion and every failure path converge on verified cleanup; no orphaned process/session/artifact remains.

## Phase 8 — Host evidence envelope and fail-closed completion gate

- [ ] Version the immutable match evidence envelope.
- [ ] Include match/session IDs, contract version, map and SC2 build, timestamps, participant assignments/build IDs/digests/player IDs/races/results, observer identity, terminal evidence digest, replay artifact metadata and validation outcome.
- [ ] Validate all required fields and cross-record correlations before state transition.
- [ ] Make completion transition atomic/idempotent; duplicate evidence delivery must not create duplicate completion.
- [ ] Ensure missing, stale, contradictory or unknown-version evidence remains incomplete.
- [ ] Ensure a replay cannot be attached to a different match/session or participant build.
- [ ] Ensure diagnostic logs never override structured evidence.
- [ ] Define retention, artifact size limits, access boundaries and cleanup/retention policy in ServerSC2.
- [ ] Add adversarial tests for stale session, cross-match replay, wrong build digest, conflicting player mapping, duplicate terminal event and replay substitution.
- [ ] Document stable rejection reasons for operations and support.

**Exit gate:** only a complete, internally consistent, provenance-bearing terminal-plus-replay evidence set can transition the host match to Completed.

## Phase 9 — End-to-end verification and operational runbook

- [ ] Add deterministic runner tests for attach, observer join request, typed observations, result resolution and replay API errors.
- [ ] Add ServerSC2 contract tests for assignment correlation, lifecycle, evidence envelope, replay storage/hash and cleanup.
- [ ] Keep fake-transport tests explicitly labeled; do not count them as live SC2 proof.
- [ ] Verify Build and Sith Policy both pass on the exact same current `development` SHA.
- [ ] Execute the opt-in live probe on the pinned SC2 installation with two participants and a separate observer.
- [ ] Verify observer in-game readiness and participant roster.
- [ ] Verify terminal `Ended`, two identity-mapped non-Undecided results, replay save and replay-info cross-check.
- [ ] Verify replay persistence and SHA-256 readback.
- [ ] Exercise observer disconnect, invalid perspective, participant crash, replay failure, timeout and malformed evidence; all must fail closed.
- [ ] Verify process exit codes, port release and absence of orphaned processes/artifacts.
- [ ] Repeat successful and failing probes enough to establish lifecycle stability; record exact repetitions and outcomes rather than claiming generic reliability.
- [ ] Publish a dated live-probe report with environment, commit SHAs, test cases, logs, evidence artifact digests and known limitations.
- [ ] Update contract, docs index, README and roadmap to reflect only demonstrated capabilities.

**Exit gate:** exact-head CI is green, live topology/result/replay/cleanup evidence is retained, and all failure probes remain fail-closed.

## Phase 10 — Release and promotion readiness (separate authorization gate)

- [ ] Review the final source diff and dependency changes across both repositories.
- [ ] Confirm the long-lived branch policy remains exactly `master` and `development`.
- [ ] Confirm all acceptance evidence references the exact candidate commits.
- [ ] Confirm no unresolved security, process-isolation, artifact-retention or operational blockers remain.
- [ ] Prepare release notes and operator documentation only after the integration is genuinely verified.
- [ ] Obtain explicit owner authorization for any merge to `master`, deployment, release or publication.

**Exit gate:** readiness can be recommended with evidence, but no merge, deployment, release or publication occurs without explicit authorization.

## Cross-repository ownership

| Work area | Owner | SingularityBot contribution |
|---|---|---|
| Bot decisions, SC2 protocol adapter and reusable runner APIs | SingularityBot | Implement and test narrow reusable primitives |
| SC2 process launch/supervision, port allocation, cancellation and cleanup | ServerSC2 | Document attach contract; never take host process ownership |
| Match/assignment/session identity | ServerSC2 | Return typed join evidence for host correlation |
| Observer-authoritative terminal result and completion policy | ServerSC2 | Expose typed observation/replay primitives; no host completion policy |
| Replay artifact persistence, SHA-256 and retention | ServerSC2 | Return replay bytes and metadata primitives |
| End-to-end live probe and operational evidence | Joint | Support runner probe; retain clear ownership and exact commit/environment evidence |
| CI execution | GitHub Actions in each repository | Require exact-head Build and Sith Policy evidence |

## Immediate next actions

1. Reinspect current runner source and tests for `JoinObserver`, `Observe`, `SaveReplay`, `GetReplayInfo` and `Sc2PlayerResultResolver`; add source anchors to this plan.
2. Keep the ServerSC2 design record and runner contract aligned; the observer-host design was reconciled to Participant A creating/joining as a player, with the observer joining independently.
3. Freeze the v1 readiness and typed-evidence shape across repositories, including explicit observer identity and player-ID correlation.
4. Extend the isolated probe to exercise the participant-hosted three-client order and derive the PortSet footprint from the pinned protocol/reference coordinator.
5. Run the smallest reproducible live probe against a pinned local SC2 build; do not mark perspective, terminal results, replay or cleanup gates complete until evidence is retained.
6. Keep the plan updated as implementation and evidence change.

## Working rules

- Keep exactly `master` and `development` as long-lived branches; work on `development`.
- Prefer documentation, source inspection and deterministic tests before changing runtime behavior.
- Do not add ServerSC2-specific dependencies or host policy to SingularityBot.
- Never claim live topology, terminal result, replay validation or cleanup success without recorded live evidence.
- No merge, release, publication, deployment, Pages dispatch or branch deletion without explicit owner authorization.
- When an item is checked, include a commit, test, workflow or evidence-report reference where applicable.


## Cross-repository reconciliation — 2026-11-16 follow-up

- ServerSC2 `development` documentation commit: [`3291f3b`](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/commit/3291f3b0c59a2b187241eabc85a398fbaaf7bc74).
- Updated [`docs/OBSERVER_HOST_DESIGN.md`](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/blob/development/docs/OBSERVER_HOST_DESIGN.md) to identify the selected integration candidate: Participant A's SC2 connection creates the game and joins as Participant A; other entrants join from independent player processes; an independent observer joins separately.
- Removed the contradictory observer-created-game startup sequence from the design decision and candidate startup sequence. Participant A remains an ordinary competitor; it receives no observer privileges.
- This is documentation/contract alignment only. It does not prove the three-client handshake, observer perspective, PortSet layout, readiness, terminal results, replay capture, or cleanup on a live SC2 build.
- CI on the ServerSC2 documentation commit is currently **red for an unrelated cross-repository target-framework incompatibility**: [run #43817071342](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/actions/runs/43817071342) fails while building `ServerSC2.ServerAppAdapter` because the checked-out `ServerApp.Core` project targets `.NET 10.0` while the adapter targets `net8.0` (`NU1201`). The earlier intermediate docs commit failed identically in [run #43817041318](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/actions/runs/43817041318). The core build and Python preflight had passed before the adapter restore failed. No ServerApp target-framework or runtime change was made; this is now a separate CI integration blocker to reconcile with the ServerApp owner.
- SingularityBot CI for the checklist follow-up commit `7f1bccba258f5cd4b1ff99a7636022b2488adc0e`: Build in_progress/pending ([#43817140905](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43817140905)); Sith Policy completed/success ([#43817140904](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43817140904)).
- ServerSC2's latest known CI run [#238](https://github.com/TrentBest/TheSingularityWorkshop.ServerSC2/actions/runs/43639841768) passed on the prior source commit `9c422d5390c06b4c31c72fa0ce84c4518a43f3db`; it is not CI evidence for documentation commit `3291f3b`.
- SingularityBot exact-head Build [#43814317278](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43814317278) and Sith Policy [#43814317265](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43814317265) both passed on `dd37928568cf48cce99dae555956ef7f78221c88`.

