# Documentation site

The documentation source lives in `docs/`. GitHub Pages is built from that Markdown source with Jekyll; the workflow stages the docs together with the strategy examples, military plans and JSON schemas before building the site.

## Publishing boundary

- The Pages workflow is **manual-only** (`workflow_dispatch`). A normal `development` commit does not deploy the public site.
- The workflow builds and uploads a Pages artifact, then deploys it through the `github-pages` environment.
- The build stage and deploy stage are separate. A successful artifact build is not a successful deployment.
- The workflow does not create or depend on a `gh-pages` branch; the repository retains the two-branch policy.

## Publish procedure

1. Confirm the exact `development` commit intended for publication.
2. Review the rendered home page, navigation links, diagrams, examples and schemas from that commit.
3. Confirm the GitHub repository's **Settings → Pages → Build and deployment → Source** is **GitHub Actions**.
4. Run **Actions → Publish documentation → Run workflow** against the reviewed ref.
5. Verify both the build and deploy jobs succeed, then open the deployment URL and check the rendered site.
6. Record the deployed commit and URL in the project change record.

Do not report the site as live based only on a successful build job. If deployment fails, retain the failure as an explicit blocker and keep the last known published site distinct from the current source.

## Site contents

- Architecture, behavior model and implementation boundaries
- Terran economy, production, movement and engagement doctrine
- Strategy authoring, examples and machine-readable schemas
- Diagnostics, local match workflow and evidence acceptance
- ServerSC2 integration contract and roadmap
- Lineage, licensing and migration provenance
