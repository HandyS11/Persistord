# Documentation & README Overhaul — Design

**Date:** 2026-06-14
**Status:** Approved (pending written-spec review)

## Goal

Bring Persistord's documentation to parity with
[RustPlusApi](https://github.com/HandyS11/RustPlusApi): polished READMEs, a full
DocFX documentation site published to GitHub Pages, and the supporting CI
pipelines (Codecov + Sonar, Stryker mutation testing) whose badges the README
advertises.

Delivered as **one cohesive spec in three sequenced phases, one PR each**:

1. **Phase 1 — README overhaul** (+ CD adapter-pack fix)
2. **Phase 2 — Quality pipelines** (Sonar+Codecov workflow, Stryker workflow)
3. **Phase 3 — DocFX site + GitHub Pages**

Phases are ordered so each PR is independently shippable. Phase 1's badges
reference workflows/integrations that Phases 2–3 make real; until then a badge may
render "no data", which is acceptable and self-heals.

## Non-goals

- No changes to library code, entities, or EF configuration.
- No changes to `docs/superpowers/` (specs/plans stay untouched).
- No new sample projects (the 5 existing samples are documented, not changed).

---

## Phase 1 — README overhaul

### Root `README.md`

Restyle to the RustPlusApi pattern while keeping the existing strong prose
(Quickstart, Soft-delete & history).

**Centered header** (`<div align="center">`):

- Title `# Persistord`
- One-line tagline (provider-agnostic, Discord-library-agnostic persistence layer
  for Discord bots on EF Core 10).
- **Badge row:**
  - CI — `https://github.com/HandyS11/Persistord/actions/workflows/CI.yml/badge.svg`
  - CD — `.../CD.yml/badge.svg`
  - Docs — `.../Documentation.yml/badge.svg`, linking `https://handys11.github.io/Persistord/`
  - `.NET 10` — `https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet`
  - License MIT — `https://img.shields.io/badge/License-MIT-yellow.svg`
  - Codecov — `[![codecov](https://codecov.io/gh/HandyS11/Persistord/graph/badge.svg?token=0u3aaXW3DK)](https://codecov.io/gh/HandyS11/Persistord)`
  - Stryker mutation — `https://img.shields.io/endpoint?style=flat&url=https%3A%2F%2Fbadge-api.stryker-mutator.io%2Fgithub.com%2FHandyS11%2FPersistord%2Fdevelop`
  - **No SonarQube badge.**
- **Nav links:** `Getting Started · Documentation · Samples`, pointing at the DocFX
  site articles (Phase 3 URLs) and `samples/README.md`.

**Packages table** — gain **Version** + **Downloads** badge columns, all 5 packages:

| Column | Source |
| --- | --- |
| Version | `https://img.shields.io/nuget/v/<PKG>.svg` |
| Downloads | `https://img.shields.io/nuget/dt/<PKG>.svg` |

Packages: `Persistord`, `Persistord.Core`, `Persistord.Messages`,
`Persistord.History`, `Persistord.Adapters.DiscordNet`.

**Versions section** — `skillicons.dev` (`?i=cs,dotnet`) + note that Persistord
targets **.NET 10 only** (EF Core 10).

**Documentation section** — replace the link to `docs/usage.md` with links to the
DocFX site: Getting Started · Guides · Recipes · Troubleshooting, plus
`samples/README.md`.

Keep Install, Quickstart, Soft-delete & history, Building, License sections (prose
already accurate).

### Per-package READMEs

`Persistord.Core`, `.Messages`, `.History`, `.Adapters.DiscordNet`, meta
`Persistord`: add a small centered header with the package's own NuGet
**version + downloads** badges and a "← Persistord docs" nav line back to the root
README / docs site. Body content stays as-is (already accurate and well-written).

### New `samples/README.md`

An index of the 5 sample projects (none exists today), each with a one-line
purpose and a link:

- `Persistord.Sample` — minimal quick-start (all three modules, generated migration)
- `Persistord.Sample.CoreGraph` — guilds/channels/users/members/roles + snowflake round-trip
- `Persistord.Sample.Messages` — embeds, attachments, reactions
- `Persistord.Sample.History` — soft-delete, query filters, append-only history
- `Persistord.Sample.DiscordNet` — `.To*Entity()` mappers via faked Discord.Net types

### CD fix — pack the adapter

`CD.yml` currently packs Core/Messages/History/meta but **not**
`Persistord.Adapters.DiscordNet`, so its NuGet badge would 404. Add a pack step:

```yaml
- name: Pack NuGet Package Persistord.Adapters.DiscordNet
  run: cd ./src/Persistord.Adapters.DiscordNet/ && dotnet pack --configuration Release -p:Version=$VERSION
```

placed alongside the other `dotnet pack` steps.

---

## Phase 2 — Quality pipelines

Adapted from the reference workflows in `tmp/workflows/`. Two **dedicated**
workflows; `CI.yml` is unchanged.

### `.github/workflows/Sonar.yml` (Sonar + Codecov)

- Trigger: **push to `develop`** (Community-Edition Sonar is single-branch).
- Steps: JDK 17 → .NET 10 → cache Sonar packages/scanner → install
  `dotnet-sonarscanner` → `begin` → `dotnet build` → `dotnet test` with
  `--collect:"XPlat Code Coverage;Format=opencover"` → `end`.
- Exclusions: `**/samples/**` from analysis; `**/samples/**,**/tests/**` from coverage.
- Then **Codecov upload** (`codecov/codecov-action@v7`) for coverage and test
  results, using `CODECOV_TOKEN`.
- **Secrets (manual prereq):** `SONAR_PROJECT_KEY`, `SONAR_TOKEN`,
  `SONAR_HOST_URL`, `CODECOV_TOKEN`.

### `.github/workflows/Mutation.yml` (Stryker)

- Trigger: `workflow_dispatch` + weekly schedule (`0 3 * * 1`, Mondays 03:00 UTC).
  Not on PRs (mutation runs are slow).
- Matrix (source project → test dir):
  - `Persistord.Core.csproj` → `tests/Persistord.Core.Tests`
  - `Persistord.Messages.csproj` → `tests/Persistord.Messages.Tests`
  - `Persistord.History.csproj` → `tests/Persistord.History.Tests`
  - `Persistord.Adapters.DiscordNet.csproj` → `tests/Persistord.Adapters.DiscordNet.Tests`
  - (meta `Persistord` omitted — nothing to mutate. Core is included; unlike
    RustPlusApi there is no source-generator blocker.)
- `dotnet tool restore` (manifest already has `dotnet-stryker` 4.14.2) →
  `dotnet stryker --config-file stryker-config.json --project <src> --version <ref>
  --reporter cleartext --reporter html --reporter dashboard` from the test dir.
- Add a **`stryker-config.json` per test dir** (4 files). Minimal config: project
  reference resolution + dashboard project name
  `github.com/HandyS11/Persistord`.
- Upload the HTML mutation report as an artifact.
- **Secret (manual prereq):** `STRYKER_DASHBOARD_API_KEY`.

---

## Phase 3 — DocFX site + GitHub Pages

### Files under `docs/`

- **`docfx.json`** — metadata from `../src` (targets `net10.0`); build conceptual
  articles + API reference. XML doc comments already emitted
  (`GenerateDocumentationFile` is on in `Directory.Build.props`).
- **`index.md`** — landing page (what Persistord is + entry links).
- **`toc.yml`** — top nav: Articles · Development · API.
- **`articles/toc.yml`** + article pages:
  - **Get Started:** `introduction.md` · `getting-started.md` · `migrations.md`
  - **Guides:** `snowflake-conversion.md` · `core-graph.md` · `messages.md` ·
    `soft-delete-and-query-filters.md` · `history.md` · `dbcontext-lifetime.md` ·
    `discord-net-adapter.md`
  - **Resources:** `samples.md` · `recipes.md` · `troubleshooting.md`
- **`development/`** — `toc.yml` + contributor notes (build, test, ReSharper
  formatting, running Stryker locally).
- **Theme:** default DocFX **modern template** with light branding (accent color;
  logo/favicon if assets exist). No bespoke theme.

Content for the guides is drawn from the existing READMEs, `docs/usage.md`, and
the PRD (`tmp/PRD.md`). `docs/usage.md` is **absorbed then removed**; any link to it
is updated. `docs/superpowers/` is untouched.

### `.github/workflows/Documentation.yml`

Adapted from `tmp/workflows/Documentation.yml`:

- Trigger: push to `main` + `workflow_dispatch`.
- Permissions: `pages: write`, `id-token: write`; concurrency group `pages`.
- Build job: .NET 10 → `docfx docs/docfx.json` → `upload-pages-artifact` (`docs/_site`).
- Deploy job: `actions/deploy-pages@v5` to the `github-pages` environment.
- Publishes to `https://handys11.github.io/Persistord/`.
- **Manual prereq:** repo Settings → Pages → Source = "GitHub Actions".

### `.gitignore`

Add DocFX build artifacts: `docs/_site/`, `docs/api/`, `docs/obj/`.

---

## Manual prerequisites (out of band, flagged to the user)

| Prereq | For |
| --- | --- |
| Codecov project + `CODECOV_TOKEN` secret | Sonar.yml upload, README badge |
| `SONAR_PROJECT_KEY` / `SONAR_TOKEN` / `SONAR_HOST_URL` secrets | Sonar.yml |
| `STRYKER_DASHBOARD_API_KEY` secret | Mutation.yml, README badge |
| Settings → Pages → "GitHub Actions" source | Documentation.yml |

## Verification

- **Phase 1:** Markdown renders; `markdownlint` passes; all internal links resolve;
  badge URLs are well-formed.
- **Phase 2:** `Mutation.yml` runs green via `workflow_dispatch`; `Sonar.yml` runs
  on a `develop` push (or is validated for YAML + step correctness if secrets are
  not yet provisioned). `dotnet stryker` config validates locally.
- **Phase 3:** `docfx docs/docfx.json` builds locally with no errors/warnings;
  `_site` renders; nav/TOC links resolve; API reference is generated from the
  packages.
