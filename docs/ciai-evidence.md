# CiAi evidence observation and acceptance gates

## Purpose

Use [The Singularity Workshop CiAi](https://github.com/TrentBest/TheSingularityWorkshop.CiAi) to observe and reconcile GitHub Actions evidence for an exact SingularityBot commit. GitHub Actions remains the authoritative build/test executor. CiAi is the read-only observer and evidence normalizer; it is not a second CI runner and does not dispatch bot changes.

This is deliberately a narrow first integration. The SingularityBot repository does not yet contain an automated CiAi workflow, replay evaluator, or cross-repository dispatch adapter. The operator can run CiAi's existing observer sample against this repository and a full commit SHA.

## Current candidate evidence

Candidate:

- Repository: `TrentBest/TheSingularityWorkshop.SingularityBot`
- Branch: `development`
- Commit: `42f943e14fcc2d85fb3b83ec01ae0f5f74729010`
- Build run: [Build #696](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42977688888) — success
- Sith Policy run: [Sith Policy #871](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42977688947) — success
- Build job steps: documentation validation, restore, build and test all completed successfully.
- Test result: 166 passed, 0 failed, 0 skipped.

These two workflow runs are tied to the same exact repository and commit SHA. They establish a green CI result for that candidate, not live-match behavior, release readiness, or permission to promote the branch.

## Observe this commit with CiAi

Requires the .NET 8 SDK and a checkout of the CiAi repository's `development` branch.

PowerShell:

```powershell
$env:CIAI_REQUIRED_WORKFLOW_PATHS = ".github/workflows/build.yml;.github/workflows/sith-policy.yml"
dotnet run --project samples/TheSingularityWorkshop.CiAi.ObserveRun -- TrentBest/TheSingularityWorkshop.SingularityBot 42f943e14fcc2d85fb3b83ec01ae0f5f74729010 artifacts/singularitybot-42f943e14fcc2d85fb3b83ec01ae0f5f74729010.json
```

Run the command from the CiAi repository root. Replace the SHA and output filename together when observing a newer SingularityBot candidate. The observer discovers matching workflow runs for that exact SHA, inspects attempts/jobs/steps with bounded pagination, and writes JSON and Markdown evidence summaries. Public repositories can be queried without a token; set `CIAI_GITHUB_TOKEN` only if the target requires authentication.

The required workflow policy is intentionally exact. A missing, duplicate, non-terminal, skipped, cancelled, neutral, unsuccessful, malformed or mismatched required result must remain non-passing. If no required workflow policy is configured, CiAi reports `complete = false`; do not interpret a partial observation as a green gate.

## What CiAi may and may not conclude

| Evidence | What it establishes | What it does not establish |
|---|---|---|
| Build workflow success | The exact SHA passed the configured documentation, restore, build and test steps | Correct behavior in a live SC2 match |
| Sith Policy success | The permanent branch policy evaluated successfully | Permission to merge, publish or release |
| Marine controller regression test | The tested rendezvous decision contract holds for its fixture | SC2 pathfinding, actual movement arrival or tactical safety |
| Local match/replay evidence | Observed runtime behavior for the recorded map, matchup, settings and game-loop interval | General competitive strength or ladder readiness |
| Explicit owner authorization | Permission for the specifically authorized action and candidate | Permission for a different SHA or target |

CiAi's output is a read-only evidence report, not a release verdict. It must never be treated as approval to merge, publish NuGet packages, deploy, change repository settings, or delete branches. Those actions remain subject to explicit owner authorization.

## Match evidence remains a separate gate

For the next local run, retain the default diagnostics capture (or explicitly enable it if the local runner configuration changes). The diagnostic bundle is described in [Development Diagnostics](diagnostics.md). Preserve the match-start input, opening CSV, and binary event timeline.

Review the next match against these concrete questions:

1. Does the economy reach the intended production gates without starving SCV production or creating avoidable supply blocks?
2. Are the first three Marines visibly ordered back to the producing Barracks while Marine four is in production?
3. Do all four Marines actually arrive at their rendezvous positions before the squad receives its forward movement assignment?
4. Does the first scout reach and hold a map-derived position below the enemy ramp, maintaining the intended vision without ascending the ramp?
5. Do submitted actions receive acceptable SC2 results, and do later observations confirm the intended unit/order/position changes?
6. Do combat decisions preserve the squad, focus fire appropriately, and retreat/regroup when the observed threat warrants it?

Movement-command submission is not proof of movement completion. A controller test is not a pathing test. If the trace does not establish arrival, safe ramp vision, or the intended state transition, keep that item open and use the trace to isolate the next change.

## Future integration

The next useful CiAi increment is a bot-specific evidence profile that pins required workflow paths and validates the exact commit/run-attempt identity, followed by read-only replay artifact observation when replay artifacts are produced by the owning workflow. Any future automation must preserve the same fail-closed identity and evidence rules. CiAi dispatch, generated commits, merge, publication and deployment are outside this first read-only slice.
