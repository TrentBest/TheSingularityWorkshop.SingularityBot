# Repository Instructions for AI Agents

## Package-aware release branch semantics — mandatory

The permanent branch target is exactly `master` and `development`. Do not create additional persistent branches. Work on `development` unless the owner explicitly authorizes another branch.

For a repository that produces a NuGet package, branch meaning depends on verified publication history:

- **Never published:** `development` is authoritative. `master` is pre-release scaffolding, not a released-package baseline, and may be identical to `development`. Before the first release, the owner may replace/reset the pre-release `master` tree to the verified, approved `development` candidate. Do not preserve artificial pre-release history as though it represented a NuGet release.
- **Previously published:** `master` represents the exact source commit corresponding to the latest successfully published NuGet package. `development` remains the active forward-moving source. Do not move `master` merely to make it match newer development work.
- **First release transition:** after the exact candidate passes its release gates and the owner explicitly authorizes publication, publish that approved candidate and establish `master` at the exact released source commit. Afterward, development may advance independently.
- **Classification evidence:** verify publication history from the authoritative NuGet feed and release/publish evidence. Do not infer publication from a project file, version string, tag, successful pack job, skipped publish job, or branch name.
- **If this repository does not publish NuGet packages:** do not invent a NuGet baseline. Identify the actual shipped artifact and its authoritative release evidence before assigning release meaning to `master`.

Equality is expected for an unreleased package; divergence is expected after a released package's development branch advances. If publication state cannot be verified, classify it as `unknown` and preserve both refs until clarified.

This policy does not grant blanket permission to publish, force-push, replace a branch, merge, delete branches, or deploy. Obtain explicit owner authorization for each such operation. Never publish NuGet packages without explicit authorization. Branch equality and green CI are evidence, not release authorization.

## Agent operating rules

- Read the repository's existing architecture, task, and implementation documents before changing source.
- Preserve repository-specific architecture and dependency boundaries.
- Verify current branch heads and exact-commit CI evidence; do not rely on remembered state.
- Do not claim a build, test, release, or integration succeeded without current evidence.
- Do not discard useful work merely because a branch is old or divergent. Reconcile it and report exact safe-to-delete candidates; the owner performs deletion.
- Prefer implementation and verifiable results over speculative plans.
