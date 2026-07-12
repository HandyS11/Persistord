# Persistord Meta Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a code-less NuGet meta package named `Persistord` that bundles `Persistord.Core` + `Persistord.Messages` + `Persistord.History` into one reference.

**Architecture:** A dependency-only SDK project (`src/Persistord/`) with no `.cs` files. `IncludeBuildOutput=false` drops the empty assembly; explicit `ProjectReference`s to all three packages become NuGet dependencies on `dotnet pack`. The package is registered in the solution, packed in CD, and documented in the root README. The Discord.Net adapter is intentionally excluded to keep the bundle library-neutral.

**Tech Stack:** .NET 10 SDK-style project, NuGet packaging, central package management, GitHub Actions (CD.yml).

**Note on verification:** This is a packaging-only change — there is no compilable code to unit-test. The "test" for a meta package is packing it and inspecting the produced `.nuspec`/`.nupkg`. Verification steps below do exactly that.

---

### Task 1: Create the meta package project and README

**Files:**

- Create: `src/Persistord/Persistord.csproj`
- Create: `src/Persistord/README.md`

- [ ] **Step 1: Create the project README**

Create `src/Persistord/README.md`:

```markdown
# Persistord

The convenience meta package for [Persistord](https://github.com/HandyS11/Persistord).

Installing `Persistord` pulls in the full library-neutral stack in one reference:

| Package | Adds |
| --- | --- |
| `Persistord.Core` | snowflake conversion, base `DiscordDbContext`, core skeleton entities |
| `Persistord.Messages` | `MessageEntity` (soft-delete), embeds, attachments, reactions |
| `Persistord.History` | append-only `MessageHistoryEntity` with a real FK to messages |

```bash
dotnet add package Persistord
```

This package selects **no** database provider and **no** Discord client library —
you stay in control of both. If you use [Discord.Net](https://github.com/discord-net/Discord.Net),
add the adapter separately:

```bash
dotnet add package Persistord.Adapters.DiscordNet
```

```

- [ ] **Step 2: Create the meta package project**

Create `src/Persistord/Persistord.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <NoWarn>$(NoWarn);NU5128</NoWarn>
    <PackageId>Persistord</PackageId>
    <Description>Meta package that bundles the library-neutral Persistord stack — Core, Messages, and History — in a single reference.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>discord;efcore;persistence;meta</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="$(MSBuildProjectDirectory)/README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Verify it packs cleanly with no warnings**

Run: `dotnet pack src/Persistord/Persistord.csproj --configuration Release`
Expected: build succeeds, `Successfully created package '.../Persistord.1.0.0.nupkg'`, and **no** NU5128 warning (treated as error otherwise).

- [ ] **Step 4: Verify the package dependencies and absence of a lib assembly**

Run:

```bash
unzip -p src/Persistord/bin/Release/Persistord.1.0.0.nupkg Persistord.nuspec
unzip -l src/Persistord/bin/Release/Persistord.1.0.0.nupkg
```

Expected: the `.nuspec` `<dependencies>` group lists `Persistord.Core`, `Persistord.Messages`, and `Persistord.History`. The archive listing contains **no** `lib/` entry (only `README.md`, `.nuspec`, and package metadata).

- [ ] **Step 5: Commit**

```bash
git add src/Persistord/Persistord.csproj src/Persistord/README.md
git commit -m "feat(meta): add Persistord meta package bundling Core, Messages, History

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Register the project in the solution

**Files:**

- Modify: `Persistord.slnx`

- [ ] **Step 1: Add the project to the /src/ folder**

In `Persistord.slnx`, inside the existing `<Folder Name="/src/">` element, add the project line. The folder becomes:

```xml
  <Folder Name="/src/">
    <Project Path="src/Persistord/Persistord.csproj" />
    <Project Path="src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj" />
    <Project Path="src/Persistord.Core/Persistord.Core.csproj" />
    <Project Path="src/Persistord.History/Persistord.History.csproj" />
    <Project Path="src/Persistord.Messages/Persistord.Messages.csproj" />
  </Folder>
```

- [ ] **Step 2: Verify the whole solution builds**

Run: `dotnet build Persistord.slnx --configuration Release`
Expected: build succeeds; `Persistord` appears among the built projects.

- [ ] **Step 3: Commit**

```bash
git add Persistord.slnx
git commit -m "build: register Persistord meta package in the solution

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Add the CD pack step

**Files:**

- Modify: `.github/workflows/CD.yml`

- [ ] **Step 1: Add a pack step after the History pack step**

In `.github/workflows/CD.yml`, immediately after the `Pack NuGet Package Persistord.History` step, insert:

```yaml
      - name: Pack NuGet Package Persistord
        run: cd ./src/Persistord/ && dotnet pack --configuration Release -p:Version=$VERSION
```

The existing `Upload a Build Artifact`, `dotnet nuget push ./**/*.nupkg`, and `Create GitHub Release` steps already glob all `.nupkg` files, so they need no changes.

- [ ] **Step 2: Verify the workflow YAML is valid**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/CD.yml'))" && echo OK`
Expected: `OK` with no traceback.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/CD.yml
git commit -m "ci: pack and publish the Persistord meta package

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Document the meta package in the root README

**Files:**

- Modify: `README.md`

- [ ] **Step 1: Add a Persistord row to the Packages table**

In `README.md`, in the `## Packages` table, add a row as the first entry (above `Persistord.Core`):

```markdown
| [`Persistord`](src/Persistord) | meta package — bundles Core, Messages, and History | — |
```

- [ ] **Step 2: Add the one-line install to the Install section**

In `README.md`, in the `## Install` section, replace the existing fenced `bash` block with:

```bash
# Recommended: the full library-neutral stack in one package
dotnet add package Persistord

# Or install modules individually:
dotnet add package Persistord.Core
dotnet add package Persistord.Messages      # optional: message persistence
dotnet add package Persistord.History       # optional: requires Messages
dotnet add package Persistord.Adapters.DiscordNet   # optional: Discord.Net mappers
```

- [ ] **Step 3: Verify markdown lints**

Run: `npx --yes markdownlint-cli README.md` (config: `.markdownlint.json`)
Expected: no errors. (If `markdownlint-cli` is unavailable offline, skip and visually confirm the table and code fence render correctly.)

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: document the Persistord meta package

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Final verification

- [ ] Run `dotnet build Persistord.slnx --configuration Release` — solution builds with the new project.
- [ ] Run `dotnet pack src/Persistord/Persistord.csproj --configuration Release` — produces `Persistord.1.0.0.nupkg` with Core/Messages/History dependencies and no `lib/` assembly.
- [ ] Confirm `git log --oneline -4` shows the four commits.
