# Documentation & README Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Persistord's docs to RustPlusApi parity — polished READMEs, a DocFX site on GitHub Pages, and the Codecov+Sonar / Stryker pipelines whose badges the README advertises.

**Architecture:** Three sequenced, independently-shippable phases (one PR each): (1) README overhaul + CD adapter-pack fix, (2) two dedicated quality-pipeline workflows, (3) DocFX site + Pages deploy workflow. No library code changes.

**Tech Stack:** Markdown, shields.io badges, GitHub Actions, DocFX 2.78.5 (local tool), Stryker.NET 4.14.2 (local tool), SonarScanner for .NET, Codecov.

**Source material for prose:** existing READMEs (`README.md`, `src/*/README.md`), `docs/usage.md`, and `tmp/PRD.md`. Reference workflows live in `tmp/workflows/`.

**Spec:** `docs/superpowers/specs/2026-06-14-docs-and-readme-overhaul-design.md`

---

## File Structure

**Phase 1 — README overhaul**
- Modify: `README.md` (root — centered header, badges, packages table, versions, docs links)
- Modify: `src/Persistord.Core/README.md`, `src/Persistord.Messages/README.md`, `src/Persistord.History/README.md`, `src/Persistord.Adapters.DiscordNet/README.md`, `src/Persistord/README.md` (header + per-package badges + back-nav)
- Create: `samples/README.md` (index of the 5 samples)
- Modify: `.github/workflows/CD.yml` (add adapter pack step)

**Phase 2 — Quality pipelines**
- Create: `.github/workflows/Sonar.yml`
- Create: `.github/workflows/Mutation.yml`
- Create: `tests/Persistord.Core.Tests/stryker-config.json`, `tests/Persistord.Messages.Tests/stryker-config.json`, `tests/Persistord.History.Tests/stryker-config.json`, `tests/Persistord.Adapters.DiscordNet.Tests/stryker-config.json`

**Phase 3 — DocFX site**
- Create: `docs/docfx.json`, `docs/index.md`, `docs/toc.yml`
- Create: `docs/articles/toc.yml` + 13 article pages
- Create: `docs/development/toc.yml` + `docs/development/index.md`
- Create: `.github/workflows/Documentation.yml`
- Modify: `.gitignore` (DocFX build artifacts)
- Delete: `docs/usage.md` (absorbed into articles)

---

# PHASE 1 — README overhaul

### Task 1.1: Restyle the root README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace the top of the README (lines 1–6) with a centered header**

Replace the current title + 4 badge lines with:

```markdown
<div align="center">

# Persistord

**A provider-agnostic, Discord-library-agnostic persistence layer for Discord bots, built on EF Core 10.**
Ships the model only — entities, conventions, and module configurations. You stay in control of the
database provider and the Discord gateway.

[![CI](https://github.com/HandyS11/Persistord/actions/workflows/CI.yml/badge.svg)](https://github.com/HandyS11/Persistord/actions/workflows/CI.yml)
[![CD](https://github.com/HandyS11/Persistord/actions/workflows/CD.yml/badge.svg)](https://github.com/HandyS11/Persistord/actions/workflows/CD.yml)
[![Docs](https://github.com/HandyS11/Persistord/actions/workflows/Documentation.yml/badge.svg)](https://handys11.github.io/Persistord/)

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![codecov](https://codecov.io/gh/HandyS11/Persistord/graph/badge.svg?token=0u3aaXW3DK)](https://codecov.io/gh/HandyS11/Persistord)
[![Mutation Score](https://img.shields.io/endpoint?style=flat&url=https%3A%2F%2Fbadge-api.stryker-mutator.io%2Fgithub.com%2FHandyS11%2FPersistord%2Fdevelop)](https://dashboard.stryker-mutator.io/reports/github.com/HandyS11/Persistord/develop)

[Getting Started](https://handys11.github.io/Persistord/articles/getting-started.html) ·
[Documentation](https://handys11.github.io/Persistord/) ·
[Samples](samples/README.md)

</div>
```

Keep the existing "Why" section that follows.

- [ ] **Step 2: Replace the Packages table with Version + Downloads columns**

Replace the existing packages table (the `| Package | Adds | Depends on |` block) with:

```markdown
## Packages

| Package | Version | Downloads | Adds | Depends on |
| --- | --- | --- | --- | --- |
| [`Persistord`](src/Persistord) | [![NuGet](https://img.shields.io/nuget/v/Persistord.svg)](https://www.nuget.org/packages/Persistord) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.svg)](https://www.nuget.org/packages/Persistord) | meta package — bundles Core, Messages, and History | Core, Messages, History |
| [`Persistord.Core`](src/Persistord.Core) | [![NuGet](https://img.shields.io/nuget/v/Persistord.Core.svg)](https://www.nuget.org/packages/Persistord.Core) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.Core.svg)](https://www.nuget.org/packages/Persistord.Core) | snowflake conversion, base `DiscordDbContext`, core skeleton entities | — |
| [`Persistord.Messages`](src/Persistord.Messages) | [![NuGet](https://img.shields.io/nuget/v/Persistord.Messages.svg)](https://www.nuget.org/packages/Persistord.Messages) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.Messages.svg)](https://www.nuget.org/packages/Persistord.Messages) | `MessageEntity` (soft-delete), embeds, attachments, reactions | Core |
| [`Persistord.History`](src/Persistord.History) | [![NuGet](https://img.shields.io/nuget/v/Persistord.History.svg)](https://www.nuget.org/packages/Persistord.History) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.History.svg)](https://www.nuget.org/packages/Persistord.History) | append-only `MessageHistoryEntity` with a real FK to messages | Messages |
| [`Persistord.Adapters.DiscordNet`](src/Persistord.Adapters.DiscordNet) | [![NuGet](https://img.shields.io/nuget/v/Persistord.Adapters.DiscordNet.svg)](https://www.nuget.org/packages/Persistord.Adapters.DiscordNet) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.Adapters.DiscordNet.svg)](https://www.nuget.org/packages/Persistord.Adapters.DiscordNet) | `.To*Entity()` mappers from [Discord.Net](https://github.com/discord-net/Discord.Net) types | Core, Messages, History |

The core packages are independent of any Discord client library. Install the DiscordNet adapter **only** if you use Discord.Net.

## Versions

![skills](https://skillicons.dev/icons?i=cs,dotnet)

Targets **.NET 10** only — EF Core 10 requires `net10.0` (LTS, supported to Nov 2028).
```

- [ ] **Step 3: Update the Documentation section to point at the DocFX site**

Replace the existing `## Documentation` list. Change the first bullet from the `docs/usage.md` link to:

```markdown
## Documentation

Full guides and the API reference live on the **[documentation site](https://handys11.github.io/Persistord/)**
(built with DocFX). Start with [Getting Started](https://handys11.github.io/Persistord/articles/getting-started.html),
browse the [Guides](https://handys11.github.io/Persistord/articles/snowflake-conversion.html) and
[Recipes](https://handys11.github.io/Persistord/articles/recipes.html), or check
[Troubleshooting](https://handys11.github.io/Persistord/articles/troubleshooting.html) if something isn't working.

- Per-package READMEs: [Core](src/Persistord.Core), [Messages](src/Persistord.Messages),
  [History](src/Persistord.History), [Adapters.DiscordNet](src/Persistord.Adapters.DiscordNet).
- Samples — runnable, focused walkthroughs (all SQLite): [`samples/`](samples/README.md).
```

Keep the existing per-sample bullet list under a sub-section if desired, or rely on `samples/README.md`. Keep the Building and License sections unchanged.

- [ ] **Step 4: Lint the README**

Run: `npx --yes markdownlint-cli2 "README.md"` (config `.markdownlint.json` is respected)
Expected: exits 0 (no errors). If the tool is unavailable offline, instead verify manually that no link is broken and all `<div>`/table syntax is balanced.

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: restyle root README with centered header, badges, and packages table"
```

---

### Task 1.2: Add headers + badges to per-package READMEs

**Files:**
- Modify: `src/Persistord/README.md`, `src/Persistord.Core/README.md`, `src/Persistord.Messages/README.md`, `src/Persistord.History/README.md`, `src/Persistord.Adapters.DiscordNet/README.md`

- [ ] **Step 1: Prepend a centered header block to each package README**

For each file, insert immediately after the existing `# <PackageName>` H1 line a block using that package's id. Template (substitute `<PKG>` with the exact package id, e.g. `Persistord.Core`):

```markdown
<div align="center">

[![NuGet](https://img.shields.io/nuget/v/<PKG>.svg?label=<PKG>)](https://www.nuget.org/packages/<PKG>)
[![Downloads](https://img.shields.io/nuget/dt/<PKG>.svg)](https://www.nuget.org/packages/<PKG>)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>
```

Per-file `<PKG>` values: `Persistord`, `Persistord.Core`, `Persistord.Messages`, `Persistord.History`, `Persistord.Adapters.DiscordNet`. Leave each file's body unchanged.

- [ ] **Step 2: Lint the package READMEs**

Run: `npx --yes markdownlint-cli2 "src/**/README.md"`
Expected: exits 0.

- [ ] **Step 3: Commit**

```bash
git add src/Persistord/README.md src/Persistord.Core/README.md src/Persistord.Messages/README.md src/Persistord.History/README.md src/Persistord.Adapters.DiscordNet/README.md
git commit -m "docs: add badge headers and back-nav to per-package READMEs"
```

---

### Task 1.3: Create the samples index README

**Files:**
- Create: `samples/README.md`

- [ ] **Step 1: Write `samples/README.md`**

```markdown
# Persistord Samples

Runnable, focused walkthroughs. Every sample uses **SQLite** for a zero-setup run;
the library itself is provider-agnostic, so the same code works on PostgreSQL, SQL
Server, etc.

Run any sample with:

```bash
dotnet run --project samples/<SampleName>
```

| Sample | Shows |
| --- | --- |
| [`Persistord.Sample`](Persistord.Sample) | Minimal quick-start — all three modules with a generated migration. |
| [`Persistord.Sample.CoreGraph`](Persistord.Sample.CoreGraph) | Guilds, channels, users, members, roles, and the snowflake `ulong ↔ long` round-trip. |
| [`Persistord.Sample.Messages`](Persistord.Sample.Messages) | Messages with embeds, attachments, and reactions. |
| [`Persistord.Sample.History`](Persistord.Sample.History) | Soft-delete, query filters, and append-only history. |
| [`Persistord.Sample.DiscordNet`](Persistord.Sample.DiscordNet) | `.To*Entity()` mappers driven by faked Discord.Net types. |
```

- [ ] **Step 2: Verify the sample directory links resolve**

Run: `ls samples/Persistord.Sample samples/Persistord.Sample.CoreGraph samples/Persistord.Sample.Messages samples/Persistord.Sample.History samples/Persistord.Sample.DiscordNet`
Expected: all five directories list without error.

- [ ] **Step 3: Commit**

```bash
git add samples/README.md
git commit -m "docs: add samples index README"
```

---

### Task 1.4: Pack the DiscordNet adapter in CD

**Files:**
- Modify: `.github/workflows/CD.yml`

- [ ] **Step 1: Add the adapter pack step**

In `.github/workflows/CD.yml`, immediately after the `Pack NuGet Package Persistord` step (the one that packs `./src/Persistord/`), insert:

```yaml
      - name: Pack NuGet Package Persistord.Adapters.DiscordNet
        run: cd ./src/Persistord.Adapters.DiscordNet/ && dotnet pack --configuration Release -p:Version=$VERSION
```

- [ ] **Step 2: Validate the workflow YAML**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/CD.yml')); print('ok')"`
Expected: prints `ok`.

- [ ] **Step 3: Verify the adapter project packs locally**

Run: `cd src/Persistord.Adapters.DiscordNet && dotnet pack --configuration Release && cd -`
Expected: build succeeds and a `.nupkg` is produced under `bin/Release/`.

- [ ] **Step 4: Commit and open the Phase 1 PR**

```bash
git add .github/workflows/CD.yml
git commit -m "ci: pack Persistord.Adapters.DiscordNet in CD"
git push -u origin docs/readme-and-docfx-overhaul
gh pr create --base develop --title "docs: README overhaul + CD adapter pack" --body "Phase 1 of the docs/README overhaul (see spec). Restyles all READMEs, adds the samples index, and fixes CD to pack the DiscordNet adapter."
```

---

# PHASE 2 — Quality pipelines

> Manual prerequisites before these workflows succeed (flag to the user, not blocking the PR): repo secrets `CODECOV_TOKEN`, `SONAR_PROJECT_KEY`, `SONAR_TOKEN`, `SONAR_HOST_URL` (Sonar.yml) and `STRYKER_DASHBOARD_API_KEY` (Mutation.yml).

### Task 2.1: Add the Sonar + Codecov workflow

**Files:**
- Create: `.github/workflows/Sonar.yml`

- [ ] **Step 1: Write `.github/workflows/Sonar.yml`**

```yaml
name: SonarQube Analysis

permissions:
  contents: read

# Analyze only the long-lived branch. Community-Edition Sonar has no branch/PR
# support — every analysis writes to the single main branch. PR build/test feedback
# is covered by CI.yml.
on:
  push:
    branches:
      - develop

jobs:
  build:
    name: Build and analyze
    runs-on: ubuntu-latest
    timeout-minutes: 15

    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0

      - name: Set up JDK 17
        uses: actions/setup-java@v5
        with:
          java-version: 17
          distribution: 'zulu'

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Cache SonarQube packages
        uses: actions/cache@v5
        with:
          path: ~/.sonar/cache
          key: ${{ runner.os }}-sonar
          restore-keys: ${{ runner.os }}-sonar

      - name: Cache SonarQube scanner
        id: cache-sonar-scanner
        uses: actions/cache@v5
        with:
          path: ./.sonar/scanner
          key: ${{ runner.os }}-sonar-scanner
          restore-keys: ${{ runner.os }}-sonar-scanner

      - name: Install SonarQube scanner
        if: steps.cache-sonar-scanner.outputs.cache-hit != 'true'
        run: |
          mkdir -p ./.sonar/scanner
          dotnet tool update dotnet-sonarscanner --tool-path ./.sonar/scanner

      - name: Build and analyze
        run: |
          ./.sonar/scanner/dotnet-sonarscanner begin /k:"${{ secrets.SONAR_PROJECT_KEY }}" /d:sonar.token="${{ secrets.SONAR_TOKEN }}" /d:sonar.host.url="${{ secrets.SONAR_HOST_URL }}" /d:sonar.exclusions="**/samples/**" /d:sonar.coverage.exclusions="**/samples/**,**/tests/**" /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
          dotnet build
          dotnet test --no-build --collect:"XPlat Code Coverage;Format=opencover" --blame-hang-timeout 60s
          ./.sonar/scanner/dotnet-sonarscanner end /d:sonar.token="${{ secrets.SONAR_TOKEN }}"

      - name: Upload coverage reports to Codecov
        uses: codecov/codecov-action@v7
        with:
          token: ${{ secrets.CODECOV_TOKEN }}

      - name: Upload test results to Codecov
        if: ${{ !cancelled() }}
        uses: codecov/codecov-action@v7
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
          report_type: test_results
```

- [ ] **Step 2: Validate the workflow YAML**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/Sonar.yml')); print('ok')"`
Expected: prints `ok`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/Sonar.yml
git commit -m "ci: add SonarQube analysis + Codecov upload workflow"
```

---

### Task 2.2: Add per-test-project Stryker configs

**Files:**
- Create: `tests/Persistord.Core.Tests/stryker-config.json`
- Create: `tests/Persistord.Messages.Tests/stryker-config.json`
- Create: `tests/Persistord.History.Tests/stryker-config.json`
- Create: `tests/Persistord.Adapters.DiscordNet.Tests/stryker-config.json`

- [ ] **Step 1: Write each `stryker-config.json`**

Identical content in all four files (the `--project` flag in the workflow selects the source project per matrix entry; the dashboard project is the repo):

```json
{
  "stryker-config": {
    "reporters": [
      "html",
      "cleartext"
    ],
    "dashboard": {
      "project": "github.com/HandyS11/Persistord",
      "module": "default"
    },
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 0
    }
  }
}
```

- [ ] **Step 2: Validate each JSON file parses**

Run: `for f in tests/*/stryker-config.json; do python3 -c "import json; json.load(open('$f'))" && echo "$f ok"; done`
Expected: four `... ok` lines.

- [ ] **Step 3: Commit**

```bash
git add tests/Persistord.Core.Tests/stryker-config.json tests/Persistord.Messages.Tests/stryker-config.json tests/Persistord.History.Tests/stryker-config.json tests/Persistord.Adapters.DiscordNet.Tests/stryker-config.json
git commit -m "ci: add Stryker configs for library test projects"
```

---

### Task 2.3: Add the Stryker mutation workflow

**Files:**
- Create: `.github/workflows/Mutation.yml`

- [ ] **Step 1: Write `.github/workflows/Mutation.yml`**

```yaml
name: Mutation Testing

permissions:
  contents: read

# Mutation runs rebuild + rerun the suite per mutant, so they are slow. Gate them
# behind manual dispatch and a weekly schedule rather than every PR.
on:
  workflow_dispatch:
  schedule:
    - cron: "0 3 * * 1" # Mondays 03:00 UTC

jobs:
  stryker:
    name: Stryker mutation testing
    runs-on: ubuntu-latest
    timeout-minutes: 60
    strategy:
      fail-fast: false
      matrix:
        include:
          - source: Persistord.Core.csproj
            testdir: tests/Persistord.Core.Tests
          - source: Persistord.Messages.csproj
            testdir: tests/Persistord.Messages.Tests
          - source: Persistord.History.csproj
            testdir: tests/Persistord.History.Tests
          - source: Persistord.Adapters.DiscordNet.csproj
            testdir: tests/Persistord.Adapters.DiscordNet.Tests
    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Stryker (${{ matrix.source }}) with dashboard
        env:
          STRYKER_DASHBOARD_API_KEY: ${{ secrets.STRYKER_DASHBOARD_API_KEY }}
        working-directory: ${{ matrix.testdir }}
        run: >
          dotnet stryker
          --config-file stryker-config.json
          --project ${{ matrix.source }}
          --version ${{ github.ref_name }}
          --reporter cleartext
          --reporter html
          --reporter dashboard

      - name: Upload mutation report
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: stryker-report-${{ matrix.source }}
          path: ${{ matrix.testdir }}/StrykerOutput/**/reports/mutation-report.html
          if-no-files-found: ignore
```

- [ ] **Step 2: Validate the workflow YAML**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/Mutation.yml')); print('ok')"`
Expected: prints `ok`.

- [ ] **Step 3: Smoke-test Stryker locally on the smallest project**

Run: `cd tests/Persistord.Core.Tests && dotnet stryker --config-file stryker-config.json --project Persistord.Core.csproj --reporter cleartext --reporter dashboard:false 2>&1 | tail -20; cd -`
Expected: Stryker starts, builds the project, and runs mutants (it may take a few minutes; the dashboard reporter is disabled here since there's no API key locally). A non-zero mutation score or a clean run both confirm the config resolves the project. If Stryker cannot find the project, fix the `--project`/path before continuing.

- [ ] **Step 4: Commit and open the Phase 2 PR**

```bash
git add .github/workflows/Mutation.yml
git commit -m "ci: add Stryker mutation-testing workflow"
git push
gh pr create --base develop --title "ci: Sonar+Codecov and Stryker pipelines" --body "Phase 2 of the docs overhaul. Adds the SonarQube+Codecov analysis workflow and the scheduled Stryker mutation workflow with per-project configs. Requires secrets: CODECOV_TOKEN, SONAR_PROJECT_KEY, SONAR_TOKEN, SONAR_HOST_URL, STRYKER_DASHBOARD_API_KEY."
```

---

# PHASE 3 — DocFX site + GitHub Pages

> Each article's prose is **adapted from existing sources** — cited per task. Write real, complete prose (no "TODO"); the cited source contains the substance to expand. After all articles exist, the gate is a clean `dotnet docfx docs/docfx.json` build.

### Task 3.1: Scaffold DocFX config and ignore artifacts

**Files:**
- Create: `docs/docfx.json`
- Create: `docs/toc.yml`
- Modify: `.gitignore`

- [ ] **Step 1: Write `docs/docfx.json`**

```json
{
  "metadata": [
    {
      "src": [
        {
          "src": "../src",
          "files": [
            "Persistord.Core/**.csproj",
            "Persistord.Messages/**.csproj",
            "Persistord.History/**.csproj",
            "Persistord.Adapters.DiscordNet/**.csproj"
          ]
        }
      ],
      "dest": "api",
      "properties": {
        "TargetFramework": "net10.0"
      }
    }
  ],
  "build": {
    "content": [
      { "files": [ "api/**.yml", "api/index.md" ] },
      { "files": [ "articles/**.md", "articles/**/toc.yml", "toc.yml", "*.md" ] },
      { "files": [ "development/**.md", "development/**/toc.yml" ] }
    ],
    "resource": [
      { "files": [ "images/**" ] }
    ],
    "output": "_site",
    "template": [ "default", "modern" ],
    "globalMetadata": {
      "_appName": "Persistord",
      "_appTitle": "Persistord",
      "_enableSearch": true,
      "pdf": false
    }
  }
}
```

- [ ] **Step 2: Write `docs/toc.yml`**

```yaml
- name: Articles
  href: articles/
- name: Development
  href: development/
- name: API
  href: api/
```

- [ ] **Step 3: Add DocFX artifacts to `.gitignore`**

Append to `.gitignore`:

```gitignore

# DocFX
docs/_site/
docs/api/
docs/obj/
```

- [ ] **Step 4: Commit**

```bash
git add docs/docfx.json docs/toc.yml .gitignore
git commit -m "docs: scaffold DocFX config and ignore build artifacts"
```

---

### Task 3.2: Write the landing page and article TOC

**Files:**
- Create: `docs/index.md`
- Create: `docs/articles/toc.yml`

- [ ] **Step 1: Write `docs/index.md`**

```markdown
# Persistord

A **provider-agnostic, Discord-library-agnostic** persistence layer for Discord bots,
built on EF Core 10. Persistord ships the **model only** — entities, conventions, and
module configurations. It never selects a database provider, never talks to Discord,
and never references a Discord client library.

## Where to start

- **[Introduction](articles/introduction.md)** — what Persistord is and the design philosophy.
- **[Getting Started](articles/getting-started.md)** — install, derive a context, pick a provider, save data.
- **[Migrations](articles/migrations.md)** — generate migrations against your own context.

## Guides

Snowflake conversion, the core graph, messages, soft-delete, history, `DbContext`
lifetime, and the Discord.Net adapter — see the **Articles** tab.

## Reference

The **API** tab is generated from the XML doc comments in the packages.
```

- [ ] **Step 2: Write `docs/articles/toc.yml`**

```yaml
- name: Get Started
  items:
    - name: Introduction
      href: introduction.md
    - name: Getting Started
      href: getting-started.md
    - name: Migrations
      href: migrations.md
- name: Guides
  items:
    - name: Snowflake Conversion
      href: snowflake-conversion.md
    - name: Core Graph
      href: core-graph.md
    - name: Messages
      href: messages.md
    - name: Soft-delete & Query Filters
      href: soft-delete-and-query-filters.md
    - name: History
      href: history.md
    - name: DbContext Lifetime
      href: dbcontext-lifetime.md
    - name: Discord.Net Adapter
      href: discord-net-adapter.md
- name: Resources
  items:
    - name: Samples
      href: samples.md
    - name: Recipes
      href: recipes.md
    - name: Troubleshooting
      href: troubleshooting.md
```

- [ ] **Step 3: Commit**

```bash
git add docs/index.md docs/articles/toc.yml
git commit -m "docs: add DocFX landing page and articles TOC"
```

---

### Task 3.3: Write the Get Started articles

**Files:**
- Create: `docs/articles/introduction.md`
- Create: `docs/articles/getting-started.md`
- Create: `docs/articles/migrations.md`

- [ ] **Step 1: Write `docs/articles/introduction.md`**

Adapt from root `README.md` "Why" section and `tmp/PRD.md` §1–§3. Required sections and substance:
- `# Introduction`
- **What it is** — provider-agnostic, Discord-library-agnostic; ships the model only.
- **The promise** — "persist whatever you choose to" (PRD §1): not state replication or gateway sync.
- **Why it exists** — Discord ids are 64-bit `ulong` snowflakes; relational providers store signed `long`; Persistord handles the bit-faithful round-trip and models the core Discord graph.
- **What it is not** (PRD §3 non-goals) — no gateway handling, no auto-sync, no upsert engine.
- **Package layout** — link the five packages with one-line descriptions (mirror the README table, prose form).

- [ ] **Step 2: Write `docs/articles/getting-started.md`**

Adapt from `docs/usage.md` §1–§3 and root README "Quick start". Required sections, each with the exact code blocks from those sources:
- `# Getting Started`
- **Install** — the `dotnet add package` commands (meta + individual modules + adapter).
- **1. Derive a context** — the `MyBotContext : DiscordDbContext` example with `ApplyMessagesModule()` / `ApplyHistoryModule()` (copy verbatim from `docs/usage.md` lines 20–45).
- **2. Choose a provider** — `AddDbContextFactory` + `UseNpgsql` example; note any EF Core 10 relational provider works.
- **3. Use short-lived contexts** — the `await using var db = ...` add+save example (copy from `docs/usage.md` lines 64–83).
- **Next steps** — link Migrations, Snowflake Conversion, Messages, DbContext Lifetime.

- [ ] **Step 3: Write `docs/articles/migrations.md`**

Adapt from `docs/usage.md` §4 (lines 86–96). Required:
- `# Migrations`
- Explain Persistord ships the model, not migrations — you generate them against your own context/provider.
- The `dotnet ef migrations add Initial --project YourBot.csproj` + `dotnet ef database update` commands.
- Point to `samples/Persistord.Sample` for a runnable end-to-end example (SQLite, generated migration).

- [ ] **Step 4: Commit**

```bash
git add docs/articles/introduction.md docs/articles/getting-started.md docs/articles/migrations.md
git commit -m "docs: add Get Started articles"
```

---

### Task 3.4: Write the Guides articles

**Files:**
- Create: `docs/articles/snowflake-conversion.md`
- Create: `docs/articles/core-graph.md`
- Create: `docs/articles/messages.md`
- Create: `docs/articles/soft-delete-and-query-filters.md`
- Create: `docs/articles/history.md`
- Create: `docs/articles/dbcontext-lifetime.md`
- Create: `docs/articles/discord-net-adapter.md`

- [ ] **Step 1: Write `docs/articles/snowflake-conversion.md`**

Adapt from `src/Persistord.Core/README.md` (Snowflake conversion + storage note) and `tmp/PRD.md` §5.1. Required: `# Snowflake Conversion`; why ids are `ulong` and providers store `long`; `UlongToLongConverter` / `NullableUlongToLongConverter` bit-faithful `unchecked` round-trip; registered globally in `ConfigureConventions` (show the `builder.Properties<ulong>().HaveConversion<...>()` snippet from PRD §5.1); the year-2084 storage note.

- [ ] **Step 2: Write `docs/articles/core-graph.md`**

Adapt from `tmp/PRD.md` §5.2–§5.3. Required: `# Core Graph`; the base `DiscordDbContext` and its skeleton `DbSet`s (Guilds/Channels/Users/Members/Roles); per-entity shape (Guild PK, Channel TPH with self-`ParentId`, composite `(GuildId, UserId)` Member key, Role); note these are plain POCOs with no Discord-library types.

- [ ] **Step 3: Write `docs/articles/messages.md`**

Adapt from `src/Persistord.Messages/README.md` and `tmp/PRD.md` §6. Required: `# Messages`; `ApplyMessagesModule()`; `MessageEntity` shape; storage decisions — embeds stored relationally (owned `Embed` with `EmbedFooter`/`EmbedAuthor`/`EmbedField`), the `e.ToJson()` power-user opt-in and why it's not the default; attachments/reactions as relational children with their key semantics (`AttachmentEntity.Id` = caller-supplied snowflake, `ReactionEntity.Id` = surrogate).

- [ ] **Step 4: Write `docs/articles/soft-delete-and-query-filters.md`**

Adapt from `src/Persistord.Messages/README.md` "Soft-delete" and root README "Soft-delete & history". Required: `# Soft-delete & Query Filters`; `IsDeleted`/`DeletedAt`; the default global query filter; `ApplyMessagesModule(filterDeleted: false)` to disable; `IgnoreQueryFilters()` per-query escape hatch; why soft-delete exists (keeps the History FK valid).

- [ ] **Step 5: Write `docs/articles/history.md`**

Adapt from `src/Persistord.History/README.md` and `tmp/PRD.md` §7. Required: `# History`; `ApplyHistoryModule()` (requires `ApplyMessagesModule()`); append-only full-snapshot rows; `HistoryChangeType` (Created/Edited/Deleted); `RecordedAt`; index on `(MessageId, RecordedAt)`; the real FK with `DeleteBehavior.Restrict`; "History is not a standalone audit log".

- [ ] **Step 6: Write `docs/articles/dbcontext-lifetime.md`**

Adapt from `src/Persistord.Core/README.md` "Context lifetime" and `tmp/PRD.md` §8. Required: `# DbContext Lifetime`; bots are long-lived/concurrent, `DbContext` is neither thread-safe nor long-lived; use `IDbContextFactory<T>` + short-lived context per unit of work; the `await using` example; warning about unbounded change-tracker growth.

- [ ] **Step 7: Write `docs/articles/discord-net-adapter.md`**

Adapt from `src/Persistord.Adapters.DiscordNet/README.md`. Required: `# Discord.Net Adapter`; install note (only if you use Discord.Net); the `.To*Entity()` usage example; the full mapper table (`ToGuildEntity` … `ToHistoryEntity`); interface-based (works for `Socket*` and `Rest*`); persistence-managed fields left at defaults; the `[3.20.0, 4.0.0)` version range.

- [ ] **Step 8: Build-check then commit**

Run: `dotnet docfx metadata docs/docfx.json` then `dotnet docfx build docs/docfx.json`
Expected: build completes; warnings about missing `samples.md`/`recipes.md`/`troubleshooting.md` xref links are acceptable until Task 3.5.

```bash
git add docs/articles/snowflake-conversion.md docs/articles/core-graph.md docs/articles/messages.md docs/articles/soft-delete-and-query-filters.md docs/articles/history.md docs/articles/dbcontext-lifetime.md docs/articles/discord-net-adapter.md
git commit -m "docs: add Guides articles"
```

---

### Task 3.5: Write the Resources articles

**Files:**
- Create: `docs/articles/samples.md`
- Create: `docs/articles/recipes.md`
- Create: `docs/articles/troubleshooting.md`

- [ ] **Step 1: Write `docs/articles/samples.md`**

Mirror `samples/README.md` (Task 1.3) in article form: `# Samples`; the run command; the table of the five samples and what each shows; link back to the repo `samples/` directory.

- [ ] **Step 2: Write `docs/articles/recipes.md`**

`# Recipes` — short, copy-pasteable patterns drawn from the README/usage examples:
- **Persist a guild** — `db.Guilds.Add(new GuildEntity { ... }); SaveChanges()`.
- **Log a message create + edit + delete** — add `MessageEntity`, then append `MessageHistoryEntity` rows per change.
- **Read soft-deleted messages** — `db.Messages.IgnoreQueryFilters()`.
- **Query a message's history chronologically** — `db.MessageHistory.Where(h => h.MessageId == id).OrderBy(h => h.RecordedAt)`.
- **Swap the provider** — same model, change `UseNpgsql` → `UseSqlite`/`UseSqlServer`.
- **Map from Discord.Net** — `socketMessage.ToMessageEntity()` (link the adapter article).

Each recipe is a heading + 1–2 sentences + a code block.

- [ ] **Step 3: Write `docs/articles/troubleshooting.md`**

`# Troubleshooting` — problem/cause/fix entries:
- **Stored ids look negative** — expected; snowflakes with the high bit set store as negative `long` and round-trip exactly. (link Snowflake Conversion)
- **Soft-deleted messages missing from queries** — the default query filter hides them; use `IgnoreQueryFilters()` or `ApplyMessagesModule(filterDeleted: false)`.
- **`dotnet ef` can't find the context** — pass `--project YourBot.csproj`; the library ships no migrations. (link Migrations)
- **Owned embed collections create extra tables** — expected (EF synthesizes shadow keys); use `e.ToJson()` on a JSON-capable provider if you prefer document storage.
- **History FK violation on delete** — don't hard-delete messages; the row must survive (soft-delete). (link Soft-delete)

- [ ] **Step 4: Full DocFX build**

Run: `dotnet docfx docs/docfx.json`
Expected: build completes with **no broken-link warnings** for the article TOC; `docs/_site/index.html` exists.

- [ ] **Step 5: Commit**

```bash
git add docs/articles/samples.md docs/articles/recipes.md docs/articles/troubleshooting.md
git commit -m "docs: add Resources articles"
```

---

### Task 3.6: Write the Development section

**Files:**
- Create: `docs/development/toc.yml`
- Create: `docs/development/index.md`

- [ ] **Step 1: Write `docs/development/toc.yml`**

```yaml
- name: Building & Contributing
  href: index.md
```

- [ ] **Step 2: Write `docs/development/index.md`**

Adapt from root README "Building" section. Required: `# Building & Contributing`; `.NET 10 SDK` requirement; `dotnet restore` / `build` / `test`; formatting enforced with ReSharper (`dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder"`); running mutation tests locally (`dotnet tool restore` then `dotnet stryker` from a test dir); note CI runs on Linux+Windows and Sonar/Codecov on `develop`.

- [ ] **Step 3: Build-check and commit**

Run: `dotnet docfx docs/docfx.json`
Expected: build completes; Development section appears in the output.

```bash
git add docs/development/toc.yml docs/development/index.md
git commit -m "docs: add Development section"
```

---

### Task 3.7: Add the Documentation deploy workflow and remove usage.md

**Files:**
- Create: `.github/workflows/Documentation.yml`
- Delete: `docs/usage.md`

> Manual prerequisite (flag to user): repo Settings → Pages → Source = "GitHub Actions".

- [ ] **Step 1: Write `.github/workflows/Documentation.yml`**

```yaml
name: Documentation

on:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

# Allow one concurrent deployment.
concurrency:
  group: pages
  cancel-in-progress: true

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Build site
        run: dotnet docfx docs/docfx.json

      - name: Upload Pages artifact
        uses: actions/upload-pages-artifact@v5
        with:
          path: docs/_site

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v5
```

- [ ] **Step 2: Validate the workflow YAML**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/Documentation.yml')); print('ok')"`
Expected: prints `ok`.

- [ ] **Step 3: Remove the absorbed usage guide and check for stragglers**

Run: `git rm docs/usage.md` then `grep -rn "usage.md" README.md src docs --include="*.md" | grep -v superpowers`
Expected: no remaining references (the README link was updated in Task 1.1). Fix any that remain.

- [ ] **Step 4: Commit and open the Phase 3 PR**

```bash
git add .github/workflows/Documentation.yml docs/usage.md
git commit -m "docs: add GitHub Pages deploy workflow and remove absorbed usage guide"
git push
gh pr create --base develop --title "docs: DocFX documentation site + Pages deploy" --body "Phase 3 of the docs overhaul. Adds the DocFX site (Get Started / Guides / Resources articles, API reference, Development section) and the Documentation workflow that deploys it to GitHub Pages. Requires enabling Pages -> 'GitHub Actions' source."
```

---

## Final verification (after all phases merge)

- [ ] Root + per-package READMEs render on GitHub with working badges and links.
- [ ] `samples/README.md` links resolve.
- [ ] CD packs all five packages on the next tag.
- [ ] `Sonar.yml` runs on a `develop` push (with secrets set); Codecov receives coverage.
- [ ] `Mutation.yml` runs green via `workflow_dispatch` (with `STRYKER_DASHBOARD_API_KEY` set).
- [ ] `Documentation.yml` deploys to `https://handys11.github.io/Persistord/`; the Docs badge goes green.
- [ ] `dotnet docfx docs/docfx.json` builds locally with no broken-link warnings.
