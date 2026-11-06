# GitHub Pages

The documentation site is built from `docs/`, with the strategy examples and JSON Schema copied alongside it.

## Required repository setting

In GitHub, open **Settings → Pages → Build and deployment** and set **Source** to **GitHub Actions**. The workflow in `.github/workflows/pages.yml` uploads the site artifact and deploys it; it does not use a `gh-pages` branch, preserving the Workshop's exactly-two-branches policy.

The artifact build and upload have succeeded. The deployment job is currently failing, so the public site should not be considered live until a deployment run completes successfully.

## Published content

- Architecture and integration boundaries
- Strategy format and example
- Machine-readable strategy JSON Schema
- Lineage, licensing and migration audit
- Development roadmap
