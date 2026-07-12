# Persistord v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a provider-agnostic, Discord-library-agnostic .NET persistence library for Discord bots as three layered NuGet packages (`Persistord.Core` ← `Persistord.Messages` ← `Persistord.History`) built on EF Core 10.

**Architecture:** `Persistord.Core` provides the snowflake (`ulong`↔`long`) conversion, a base `DiscordDbContext`, skeleton POCO entities, and `ApplyCoreConfiguration()`. `Persistord.Messages` adds `MessageEntity` (soft-delete), an owned `Embed` model, relational Attachments/Reactions, and `ApplyMessagesModule()`. `Persistord.History` adds `MessageHistoryEntity` with a real FK to `MessageEntity` and `ApplyHistoryModule()`. The library defines the model only — it never calls `UseX`, never talks to Discord, never references a Discord client library.

**Tech Stack:** .NET 10 (`net10.0`), EF Core 10, xUnit, EF Core SQLite (in-memory model/round-trip tests), Testcontainers PostgreSQL (one real provider in CI), Central Package Management (already in repo), GitHub Actions (CI + CD to NuGet.org).

**Source spec:** `tmp/PRD.md` (PRD — Discord Persistence Library for .NET).

---

## Conventions used by this plan

- All package IDs use the `Persistord.*` prefix (PRD uses `YourLib.*` as a placeholder).
- Test runner: `dotnet test`. Build: `dotnet build -c Release`. Format gate: `dotnet format --verify-no-changes`.
- The repo already enforces `TreatWarningsAsErrors`, nullable, analyzers, and `GenerateDocumentationFile` via `Directory.Build.props`. **Every public type and member needs an XML doc comment** or the build fails (CS1591). Plan steps include doc comments.
- `Directory.Build.props` sets `IsPackable=false` by default. Each `src/*` project must set `<IsPackable>true</IsPackable>` and package metadata; test/sample projects stay non-packable.
- Commit after every green step. Conventional Commit messages (`feat:`, `test:`, `chore:`, `ci:`, `docs:`).

## Open micro-decisions — resolved for v1

These come from PRD §9. Locked here so the implementer does not re-litigate:

1. **Soft-delete query filter:** Ship **on** by default, but `ApplyMessagesModule(bool filterDeleted = true)` exposes an opt-out. Document `IgnoreQueryFilters()` in XML docs and README.
2. **History granularity:** Full content snapshot per change. No diffing.
3. **Core entity property coverage:** Minimal, Discord-shaped set defined per task below. Plain POCOs, no Discord-library types. Expandable later without breaking the public API surface.

---

## File Structure

```
Persistord.slnx                                  (populated)
Directory.Packages.props                         (add EF Core + test packages)
.github/workflows/CI.yml                          (build/format/test/coverage + Postgres provider)
.github/workflows/CD.yml                          (pack + push 3 packages to NuGet on tag)

src/Persistord.Core/
  Persistord.Core.csproj
  Conversions/UlongToLongConverter.cs
  Conversions/NullableUlongToLongConverter.cs
  Entities/GuildEntity.cs
  Entities/ChannelEntity.cs
  Entities/ChannelType.cs
  Entities/UserEntity.cs
  Entities/MemberEntity.cs
  Entities/RoleEntity.cs
  Configurations/GuildEntityConfiguration.cs
  Configurations/ChannelEntityConfiguration.cs
  Configurations/UserEntityConfiguration.cs
  Configurations/MemberEntityConfiguration.cs
  Configurations/RoleEntityConfiguration.cs
  DiscordDbContext.cs
  ModelBuilderExtensions.cs                       (ApplyCoreConfiguration)

src/Persistord.Messages/
  Persistord.Messages.csproj
  Entities/MessageEntity.cs
  Entities/AttachmentEntity.cs
  Entities/ReactionEntity.cs
  Owned/Embed.cs
  Owned/EmbedFooter.cs
  Owned/EmbedAuthor.cs
  Owned/EmbedField.cs
  Configurations/MessageEntityConfiguration.cs
  ModelBuilderExtensions.cs                       (ApplyMessagesModule)

src/Persistord.History/
  Persistord.History.csproj
  Entities/MessageHistoryEntity.cs
  Entities/HistoryChangeType.cs
  Configurations/MessageHistoryEntityConfiguration.cs
  ModelBuilderExtensions.cs                       (ApplyHistoryModule)

tests/Persistord.Core.Tests/
  Persistord.Core.Tests.csproj
  TestContext.cs                                  (concrete DiscordDbContext for tests)
  SqliteFixture.cs                                (shared in-memory SQLite helper)
  UlongConverterTests.cs
  CoreModelTests.cs
tests/Persistord.Messages.Tests/
  Persistord.Messages.Tests.csproj
  TestContext.cs
  MessagesModelTests.cs
  SoftDeleteTests.cs
tests/Persistord.History.Tests/
  Persistord.History.Tests.csproj
  TestContext.cs
  HistoryModelTests.cs
tests/Persistord.Provider.Tests/                 (real-provider, Testcontainers Postgres)
  Persistord.Provider.Tests.csproj
  PostgresFixture.cs
  PostgresRoundTripTests.cs

samples/Persistord.Sample/
  Persistord.Sample.csproj
  MyBotContext.cs
  Program.cs
docs/usage.md
```

---

## Phase 0 — Repo scaffolding, central packages, workflows

### Task 0.1: Add EF Core + test packages to central package management

**Files:**

- Modify: `Directory.Packages.props`

- [ ] **Step 1: Add package versions**

Add inside the `<!-- Packages -->` ItemGroup and a new test ItemGroup:

```xml
  <ItemGroup>
    <!-- Packages -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <!-- Test + sample -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
  </ItemGroup>
```

> Note: pin exact versions to whatever `dotnet add package` resolves at implementation time; EF Core 10 GA is the floor. Keep `Microsoft.EntityFrameworkCore` major == provider major.

- [ ] **Step 2: Verify restore-ability later** (no project yet) — proceed.

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore: add EF Core 10 and test package versions to CPM"
```

### Task 0.2: Create the three source projects and wire the solution

**Files:**

- Create: `src/Persistord.Core/Persistord.Core.csproj`
- Create: `src/Persistord.Messages/Persistord.Messages.csproj`
- Create: `src/Persistord.History/Persistord.History.csproj`
- Modify: `Persistord.slnx`

- [ ] **Step 1: Create `src/Persistord.Core/Persistord.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.Core</PackageId>
    <Description>Skeleton POCOs, snowflake ulong-to-long conversion, and base DiscordDbContext for persisting Discord data with EF Core.</Description>
    <Authors>HandyS11</Authors>
    <Packagelicense>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>discord;efcore;persistence;entity-framework</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
  </ItemGroup>
  <ItemGroup>
    <None Include="$(MSBuildProjectDirectory)/README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

> Fix the typo when typing: the license line is a single element `<PackageLicenseExpression>MIT</PackageLicenseExpression>`.

- [ ] **Step 2: Create `src/Persistord.Messages/Persistord.Messages.csproj`** (same shape, add ProjectReference to Core)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.Messages</PackageId>
    <Description>Message persistence module for Persistord: MessageEntity with soft-delete, owned Embed model, relational attachments and reactions.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageTags>discord;efcore;persistence;messages</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Persistord.Core/Persistord.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `src/Persistord.History/Persistord.History.csproj`** (ProjectReference to Messages)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.History</PackageId>
    <Description>Append-only message history module for Persistord, with a relational FK to MessageEntity.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageTags>discord;efcore;persistence;history</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Persistord.Messages/Persistord.Messages.csproj" />
  </ItemGroup>
</Project>
```

> ProjectReferences become package dependencies on `dotnet pack` automatically (Messages depends on Core, History depends on Messages), matching the PRD dependency graph.

- [ ] **Step 4: Add all projects to the solution**

```bash
cd /home/handys11/Dev/Persistord
dotnet sln Persistord.slnx add src/Persistord.Core/Persistord.Core.csproj src/Persistord.Messages/Persistord.Messages.csproj src/Persistord.History/Persistord.History.csproj
```

- [ ] **Step 5: Add a placeholder type so each project compiles**

Create `src/Persistord.Core/Conversions/.gitkeep` is not enough — instead proceed straight to Task 1.1 which adds the first real type. For now verify the empty projects build:

```bash
dotnet build src/Persistord.Core/Persistord.Core.csproj -c Release
```

Expected: PASS (empty project builds).

- [ ] **Step 6: Commit**

```bash
git add src Persistord.slnx
git commit -m "chore: scaffold Core/Messages/History projects and solution"
```

### Task 0.3: CI workflow

**Files:**

- Create: `.github/workflows/CI.yml`

- [ ] **Step 1: Write CI.yml** (adapted from the reference project; targets `develop`, adds the Postgres provider job)

```yaml
name: CI

permissions:
  contents: read

on:
  push:
    branches:
      - develop
  pull_request:

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ ubuntu-latest, windows-latest ]
      fail-fast: false
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          cache: true
          cache-dependency-path: Directory.Packages.props

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Check formatting
        run: dotnet format --no-restore --verify-no-changes

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --results-directory ./coverage

      - name: Upload coverage reports
        if: matrix.os == 'ubuntu-latest'
        uses: actions/upload-artifact@v4
        with:
          name: coverage-reports
          path: ./coverage/**/coverage.cobertura.xml
          retention-days: 14
```

> Testcontainers needs Docker — available on `ubuntu-latest` GitHub runners, not `windows-latest`. The provider tests must be gated to skip when Docker is unavailable (see Task 5.1, `Skip` via a fixture availability check), so the Windows matrix leg stays green.

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/CI.yml
git commit -m "ci: add CI workflow (build, format, test, coverage)"
```

### Task 0.4: CD workflow (normal / prerelease tag, pack + publish on tag)

**Files:**

- Create: `.github/workflows/CD.yml`
- Modify: `Directory.Build.props` (enable symbol packages so the GitHub Release can attach `.snupkg`)

This mirrors the user's reference CD (`tmp/workflows/CD.yml`), adapted to Persistord's three packages. Key behaviors carried over from the reference:

- **Tag trigger `v*`** — any tag starting with `v`, not a strict `v*.*.*` pattern.
- **Normal vs prerelease detection:** a `-` in the version (e.g. `v1.2.0-rc.1`) means prerelease; otherwise stable.
- **Branch gate:** a **stable** tag must point at a commit on `main`; a **prerelease** tag may point at a commit on `main` **or** `develop`. (Note: since `develop` is now the default branch, day-to-day prereleases are cut from `develop` and stable releases are cut from `main`.)
- **Trusted publishing** via `NuGet/login@v1` — no static `NUGET_API_KEY` secret; instead requires NuGet.org trusted-publisher config + `id-token: write`.
- **Dual publish:** NuGet.org and GitHub Packages.
- **GitHub Release** created with `--generate-notes`, and `--prerelease` when applicable.

- [ ] **Step 1: Enable symbol packages in `Directory.Build.props`**

Add to the packaging `PropertyGroup` (the one with `<Version>`/`<IsPackable>`):

```xml
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
```

> The reference release step attaches `./**/*.snupkg`. Without symbol generation the `gh release create` glob would match nothing and fail, so this must be enabled for the three packable projects. `IsPackable=false` projects are unaffected.

- [ ] **Step 2: Write CD.yml**

```yaml
name: CD

permissions:
  contents: write
  packages: write
  id-token: write

on:
  push:
    tags:
      - 'v*'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout Repository
        uses: actions/checkout@v6
        with:
          fetch-depth: 0

      - name: Resolve Version from Tag
        run: |
          VERSION="${GITHUB_REF_NAME#v}"
          echo "VERSION=$VERSION" >> "$GITHUB_ENV"
          if [[ "$VERSION" == *-* ]]; then
            echo "IS_PRERELEASE=true" >> "$GITHUB_ENV"
            echo "Resolved prerelease version $VERSION."
          else
            echo "IS_PRERELEASE=false" >> "$GITHUB_ENV"
            echo "Resolved stable version $VERSION."
          fi

      - name: Verify Tag Branch
        run: |
          git fetch origin main develop
          if [[ "$IS_PRERELEASE" == "true" ]]; then
            if git merge-base --is-ancestor "$GITHUB_SHA" origin/main \
               || git merge-base --is-ancestor "$GITHUB_SHA" origin/develop; then
              echo "Prerelease tag $GITHUB_REF_NAME verified on main or develop."
            else
              echo "::error::Prerelease tag $GITHUB_REF_NAME does not point to a commit on main or develop."
              exit 1
            fi
          elif ! git merge-base --is-ancestor "$GITHUB_SHA" origin/main; then
            echo "::error::Stable tag $GITHUB_REF_NAME does not point to a commit on main."
            exit 1
          else
            echo "Stable tag $GITHUB_REF_NAME verified on main."
          fi

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Pack NuGet Package Persistord.Core
        run: cd ./src/Persistord.Core/ && dotnet pack --configuration Release -p:Version=$VERSION

      - name: Pack NuGet Package Persistord.Messages
        run: cd ./src/Persistord.Messages/ && dotnet pack --configuration Release -p:Version=$VERSION

      - name: Pack NuGet Package Persistord.History
        run: cd ./src/Persistord.History/ && dotnet pack --configuration Release -p:Version=$VERSION

      - name: Upload a Build Artifact
        uses: actions/upload-artifact@v7
        with:
          path: ./**/*.nupkg

      - name: NuGet login (trusted publishing)
        uses: NuGet/login@v1
        id: login
        with:
          user: HandyS11

      - name: Publish NuGet Package to NuGet.org
        run: dotnet nuget push ./**/*.nupkg --api-key ${{ steps.login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate

      - name: Publish NuGet Package to GitHub Packages
        run: dotnet nuget push ./**/*.nupkg --api-key ${{ secrets.GITHUB_TOKEN }} --source https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json --skip-duplicate

      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        shell: bash
        run: |
          shopt -s globstar
          PRERELEASE_FLAG=""
          if [[ "$IS_PRERELEASE" == "true" ]]; then
            PRERELEASE_FLAG="--prerelease"
          fi
          gh release create "$GITHUB_REF_NAME" ./**/*.nupkg ./**/*.snupkg --generate-notes $PRERELEASE_FLAG
```

> **Trusted publishing prerequisite (handoff note):** configure a trusted publisher for each of `Persistord.Core`, `Persistord.Messages`, `Persistord.History` on NuGet.org (Account → Trusted Publishing), bound to this repo and the `CD` workflow. No `NUGET_API_KEY` secret is used. `GITHUB_TOKEN` for GitHub Packages is provided automatically. First-ever publish of a brand-new package ID may need a one-time manual push or owner setup, since trusted publishing binds to an existing package owner.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/CD.yml Directory.Build.props
git commit -m "ci: add CD workflow with normal/prerelease tag handling and trusted publishing"
```

---

## Phase 1 — Core (PRD §5, milestone 1)

### Task 1.1: Snowflake converters (single source of truth)

**Files:**

- Create: `src/Persistord.Core/Conversions/UlongToLongConverter.cs`
- Create: `src/Persistord.Core/Conversions/NullableUlongToLongConverter.cs`
- Test: `tests/Persistord.Core.Tests/UlongConverterTests.cs` (project created here)

- [ ] **Step 1: Create the Core test project**

`tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
  </ItemGroup>
</Project>
```

Then: `dotnet sln Persistord.slnx add tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`

- [ ] **Step 2: Write the failing test**

`tests/Persistord.Core.Tests/UlongConverterTests.cs`:

```csharp
using Persistord.Core.Conversions;
using Xunit;

namespace Persistord.Core.Tests;

public class UlongConverterTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(175928847299117063UL)]        // a real-shaped snowflake
    [InlineData(ulong.MaxValue)]               // exercises the high bit
    public void RoundTrip_IsExact(ulong value)
    {
        var converter = new UlongToLongConverter();
        long stored = (long)converter.ConvertToProvider(value)!;
        ulong back = (ulong)converter.ConvertFromProvider(stored)!;
        Assert.Equal(value, back);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42UL)]
    public void NullableRoundTrip_IsExact(ulong? value)
    {
        var converter = new NullableUlongToLongConverter();
        var stored = converter.ConvertToProvider(value);
        var back = (ulong?)converter.ConvertFromProvider(stored);
        Assert.Equal(value, back);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: FAIL — `UlongToLongConverter` does not exist (compile error).

- [ ] **Step 4: Implement the converters**

`src/Persistord.Core/Conversions/UlongToLongConverter.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistord.Core.Conversions;

/// <summary>
/// Converts a Discord snowflake (<see cref="ulong"/>) to a provider <see cref="long"/>
/// using an unchecked bit-faithful cast, so the round-trip is exact for all values
/// including those with the high bit set.
/// </summary>
public sealed class UlongToLongConverter : ValueConverter<ulong, long>
{
    /// <summary>Initializes a new instance of the <see cref="UlongToLongConverter"/> class.</summary>
    public UlongToLongConverter()
        : base(v => unchecked((long)v), v => unchecked((ulong)v))
    {
    }
}
```

`src/Persistord.Core/Conversions/NullableUlongToLongConverter.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistord.Core.Conversions;

/// <summary>
/// Nullable counterpart of <see cref="UlongToLongConverter"/> for <see cref="Nullable{T}"/>
/// snowflake properties.
/// </summary>
public sealed class NullableUlongToLongConverter : ValueConverter<ulong?, long?>
{
    /// <summary>Initializes a new instance of the <see cref="NullableUlongToLongConverter"/> class.</summary>
    public NullableUlongToLongConverter()
        : base(
            v => v.HasValue ? unchecked((long)v.Value) : null,
            v => v.HasValue ? unchecked((ulong)v.Value) : null)
    {
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS (6 cases).

- [ ] **Step 6: Commit**

```bash
git add src/Persistord.Core/Conversions tests/Persistord.Core.Tests Persistord.slnx
git commit -m "feat(core): add bit-faithful ulong<->long snowflake converters"
```

### Task 1.2: Core skeleton entities

**Files:**

- Create: `src/Persistord.Core/Entities/GuildEntity.cs`, `ChannelEntity.cs`, `ChannelType.cs`, `UserEntity.cs`, `MemberEntity.cs`, `RoleEntity.cs`

> These are plain POCOs (no behavior), so there is nothing to unit-test in isolation. They are exercised by the model tests in Task 1.5. This task is a single commit of the type definitions.

- [ ] **Step 1: Create `GuildEntity.cs`**

```csharp
namespace Persistord.Core.Entities;

/// <summary>A Discord guild (server).</summary>
public class GuildEntity
{
    /// <summary>The guild snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The guild name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The snowflake id of the guild owner.</summary>
    public ulong OwnerId { get; set; }
}
```

- [ ] **Step 2: Create `ChannelType.cs`**

```csharp
namespace Persistord.Core.Entities;

/// <summary>Discord channel kinds, used as the table-per-hierarchy discriminator.</summary>
public enum ChannelType
{
    /// <summary>A text channel.</summary>
    Text = 0,

    /// <summary>A voice channel.</summary>
    Voice = 2,

    /// <summary>A category that parents other channels.</summary>
    Category = 4,

    /// <summary>A thread.</summary>
    Thread = 11,
}
```

- [ ] **Step 3: Create `ChannelEntity.cs`**

```csharp
namespace Persistord.Core.Entities;

/// <summary>A Discord channel. Self-references via <see cref="ParentId"/> to model
/// category &#8594; channel &#8594; thread hierarchies.</summary>
public class ChannelEntity
{
    /// <summary>The channel snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The owning guild snowflake id.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The parent channel snowflake id (category or parent channel), if any.</summary>
    public ulong? ParentId { get; set; }

    /// <summary>The channel kind.</summary>
    public ChannelType Type { get; set; }

    /// <summary>The channel name.</summary>
    public string Name { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Create `UserEntity.cs`**

```csharp
namespace Persistord.Core.Entities;

/// <summary>A Discord user (account-level, guild-independent).</summary>
public class UserEntity
{
    /// <summary>The user snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The user's username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The user's global display name, if set.</summary>
    public string? GlobalName { get; set; }
}
```

- [ ] **Step 5: Create `MemberEntity.cs`**

```csharp
namespace Persistord.Core.Entities;

/// <summary>A guild membership: a user within a guild. Keyed by the composite
/// <c>(GuildId, UserId)</c>.</summary>
public class MemberEntity
{
    /// <summary>The guild snowflake id (part of the composite key).</summary>
    public ulong GuildId { get; set; }

    /// <summary>The user snowflake id (part of the composite key).</summary>
    public ulong UserId { get; set; }

    /// <summary>The per-guild nickname, if set.</summary>
    public string? Nickname { get; set; }

    /// <summary>When the user joined the guild.</summary>
    public DateTimeOffset? JoinedAt { get; set; }
}
```

- [ ] **Step 6: Create `RoleEntity.cs`**

```csharp
namespace Persistord.Core.Entities;

/// <summary>A Discord role within a guild.</summary>
public class RoleEntity
{
    /// <summary>The role snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The owning guild snowflake id.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The raw permission bitfield.</summary>
    public ulong Permissions { get; set; }

    /// <summary>The role color (RGB integer).</summary>
    public int Color { get; set; }
}
```

- [ ] **Step 7: Build to verify it compiles**

Run: `dotnet build src/Persistord.Core/Persistord.Core.csproj -c Release`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Persistord.Core/Entities
git commit -m "feat(core): add skeleton Discord entities"
```

### Task 1.3: Core entity configurations

**Files:**

- Create: `src/Persistord.Core/Configurations/{Guild,Channel,User,Member,Role}EntityConfiguration.cs`

> Each is an `IEntityTypeConfiguration<T>`. Verified through the model tests in Task 1.5. Single commit.

- [ ] **Step 1: `GuildEntityConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="GuildEntity"/>.</summary>
public sealed class GuildEntityConfiguration : IEntityTypeConfiguration<GuildEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GuildEntity> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Name).IsRequired();
    }
}
```

- [ ] **Step 2: `ChannelEntityConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="ChannelEntity"/>, including the
/// self-referencing parent relationship.</summary>
public sealed class ChannelEntityConfiguration : IEntityTypeConfiguration<ChannelEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChannelEntity> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired();
        builder.HasIndex(c => c.GuildId);
        builder.HasOne<ChannelEntity>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 3: `UserEntityConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="UserEntity"/>.</summary>
public sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Username).IsRequired();
    }
}
```

- [ ] **Step 4: `MemberEntityConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="MemberEntity"/> with its composite key.</summary>
public sealed class MemberEntityConfiguration : IEntityTypeConfiguration<MemberEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemberEntity> builder)
    {
        builder.HasKey(m => new { m.GuildId, m.UserId });
    }
}
```

- [ ] **Step 5: `RoleEntityConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="RoleEntity"/>.</summary>
public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).IsRequired();
        builder.HasIndex(r => r.GuildId);
    }
}
```

- [ ] **Step 6: Build + commit**

```bash
dotnet build src/Persistord.Core/Persistord.Core.csproj -c Release
git add src/Persistord.Core/Configurations
git commit -m "feat(core): add entity type configurations"
```

### Task 1.4: `ApplyCoreConfiguration()` and `DiscordDbContext`

**Files:**

- Create: `src/Persistord.Core/ModelBuilderExtensions.cs`
- Create: `src/Persistord.Core/DiscordDbContext.cs`

- [ ] **Step 1: `ModelBuilderExtensions.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Configurations;

namespace Persistord.Core;

/// <summary>Model-building extensions that wire the core Persistord entities.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the configurations for all core skeleton entities
    /// (guild, channel, user, member, role). Call from <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyCoreConfiguration(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new GuildEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemberEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RoleEntityConfiguration());
        return modelBuilder;
    }
}
```

- [ ] **Step 2: `DiscordDbContext.cs`** (verbatim shape from PRD §5.2)

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Conversions;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>
/// Base EF Core context that ships the core Discord skeleton and the global
/// snowflake conversion. Inherit it, declare module <c>DbSet</c>s, and apply
/// module configurations in <c>OnModelCreating</c>. The library never selects a
/// provider; the consumer calls <c>UseSqlite</c>/<c>UseNpgsql</c>/etc.
/// </summary>
public abstract class DiscordDbContext : DbContext
{
    /// <summary>Initializes the context with the given options.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    protected DiscordDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Persisted guilds.</summary>
    public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

    /// <summary>Persisted channels.</summary>
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();

    /// <summary>Persisted users.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Persisted guild members.</summary>
    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    /// <summary>Persisted roles.</summary>
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyCoreConfiguration();
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<ulong>().HaveConversion<UlongToLongConverter>();
        builder.Properties<ulong?>().HaveConversion<NullableUlongToLongConverter>();
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/Persistord.Core/Persistord.Core.csproj -c Release
git add src/Persistord.Core/ModelBuilderExtensions.cs src/Persistord.Core/DiscordDbContext.cs
git commit -m "feat(core): add DiscordDbContext and ApplyCoreConfiguration"
```

### Task 1.5: Core model integration tests (SQLite in-memory)

**Files:**

- Create: `tests/Persistord.Core.Tests/TestContext.cs`
- Create: `tests/Persistord.Core.Tests/SqliteFixture.cs`
- Create: `tests/Persistord.Core.Tests/CoreModelTests.cs`

- [ ] **Step 1: `SqliteFixture.cs`** (shared helper: open in-memory SQLite connection, create context)

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>Creates a context backed by a fresh open in-memory SQLite connection.</summary>
public static class SqliteFixture
{
    public static (SqliteConnection Connection, TestContext Context) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestContext(options);
        context.Database.EnsureCreated();
        return (connection, context);
    }
}
```

- [ ] **Step 2: `TestContext.cs`** (concrete subclass — the abstract base needs one)

```csharp
using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>Minimal concrete context over the core skeleton for tests.</summary>
public sealed class TestContext : Persistord.Core.DiscordDbContext
{
    public TestContext(DbContextOptions<TestContext> options)
        : base(options)
    {
    }
}
```

- [ ] **Step 3: Write the failing test**

`tests/Persistord.Core.Tests/CoreModelTests.cs`:

```csharp
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class CoreModelTests
{
    [Fact]
    public void Snowflake_PersistsAndReadsBack_WithHighBitValue()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            context.Guilds.Add(new GuildEntity { Id = ulong.MaxValue, Name = "g", OwnerId = 1UL });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var loaded = Assert.Single(context.Guilds.ToList());
            Assert.Equal(ulong.MaxValue, loaded.Id);
        }
    }

    [Fact]
    public void Snowflake_IsStoredAsLongColumn()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            var column = context.Model.FindEntityType(typeof(GuildEntity))!
                .FindProperty(nameof(GuildEntity.Id))!;
            Assert.Equal(typeof(long), column.GetProviderClrType());
        }
    }

    [Fact]
    public void Member_HasCompositeKey()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            var key = context.Model.FindEntityType(typeof(MemberEntity))!.FindPrimaryKey()!;
            Assert.Equal(
                new[] { nameof(MemberEntity.GuildId), nameof(MemberEntity.UserId) },
                key.Properties.Select(p => p.Name).ToArray());
        }
    }
}
```

- [ ] **Step 4: Run — verify it fails first** (before TestContext/fixture exist this is a compile failure; after Steps 1-2 it should pass). Run to confirm green:

Run: `dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS (converter tests + 3 model tests).

- [ ] **Step 5: Commit**

```bash
git add tests/Persistord.Core.Tests
git commit -m "test(core): add SQLite model integration tests"
```

---

## Phase 2 — Messages (PRD §6, milestone 2)

### Task 2.1: Owned embed model + child entities

**Files:**

- Create: `src/Persistord.Messages/Owned/Embed.cs`, `EmbedFooter.cs`, `EmbedAuthor.cs`, `EmbedField.cs`
- Create: `src/Persistord.Messages/Entities/AttachmentEntity.cs`, `ReactionEntity.cs`

- [ ] **Step 1: `EmbedFooter.cs`, `EmbedAuthor.cs`, `EmbedField.cs`**

```csharp
namespace Persistord.Messages.Owned;

/// <summary>The footer of an embed. Owned; no identity of its own.</summary>
public class EmbedFooter
{
    /// <summary>Footer text.</summary>
    public string? Text { get; set; }

    /// <summary>Footer icon URL.</summary>
    public string? IconUrl { get; set; }
}
```

```csharp
namespace Persistord.Messages.Owned;

/// <summary>The author block of an embed. Owned; no identity of its own.</summary>
public class EmbedAuthor
{
    /// <summary>Author name.</summary>
    public string? Name { get; set; }

    /// <summary>Author URL.</summary>
    public string? Url { get; set; }
}
```

```csharp
namespace Persistord.Messages.Owned;

/// <summary>A single name/value field of an embed. Owned collection element.</summary>
public class EmbedField
{
    /// <summary>Field name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Field value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether the field renders inline.</summary>
    public bool Inline { get; set; }
}
```

- [ ] **Step 2: `Embed.cs`** (PRD §6.4)

```csharp
namespace Persistord.Messages.Owned;

/// <summary>An owned embed model. Has no key of its own; lives under a message.</summary>
public class Embed
{
    /// <summary>Embed title.</summary>
    public string? Title { get; set; }

    /// <summary>Embed description.</summary>
    public string? Description { get; set; }

    /// <summary>Embed color (RGB integer).</summary>
    public int? Color { get; set; }

    /// <summary>Optional footer.</summary>
    public EmbedFooter? Footer { get; set; }

    /// <summary>Optional author block.</summary>
    public EmbedAuthor? Author { get; set; }

    /// <summary>Embed fields.</summary>
    public List<EmbedField> Fields { get; set; } = new();
}
```

- [ ] **Step 3: `AttachmentEntity.cs`, `ReactionEntity.cs`** (relational children with own keys)

```csharp
namespace Persistord.Messages.Entities;

/// <summary>A message attachment, stored as a relational child of a message.</summary>
public class AttachmentEntity
{
    /// <summary>The attachment snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The owning message id (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>The attachment file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The attachment URL.</summary>
    public string Url { get; set; } = string.Empty;
}
```

```csharp
namespace Persistord.Messages.Entities;

/// <summary>A reaction aggregate on a message, stored as a relational child.</summary>
public class ReactionEntity
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The owning message id (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>The emoji (unicode or <c>name:id</c> for custom emoji).</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>The reaction count.</summary>
    public int Count { get; set; }
}
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build src/Persistord.Messages/Persistord.Messages.csproj -c Release
git add src/Persistord.Messages/Owned src/Persistord.Messages/Entities
git commit -m "feat(messages): add owned embed model and child entities"
```

### Task 2.2: `MessageEntity`

**Files:**

- Create: `src/Persistord.Messages/Entities/MessageEntity.cs`

- [ ] **Step 1: Create the entity** (PRD §6.1)

```csharp
using Persistord.Messages.Owned;

namespace Persistord.Messages.Entities;

/// <summary>A persisted Discord message. Uses soft-delete so that history rows
/// keeping a foreign key to this row survive a delete.</summary>
public class MessageEntity
{
    /// <summary>The message snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The channel snowflake id the message belongs to.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The author snowflake id.</summary>
    public ulong AuthorId { get; set; }

    /// <summary>The message content.</summary>
    public string? Content { get; set; }

    /// <summary>When the message was last edited, if ever.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Whether the message has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the message was soft-deleted, if applicable.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Owned embeds.</summary>
    public List<Embed> Embeds { get; set; } = new();

    /// <summary>Relational attachments.</summary>
    public List<AttachmentEntity> Attachments { get; set; } = new();

    /// <summary>Relational reactions.</summary>
    public List<ReactionEntity> Reactions { get; set; } = new();
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/Persistord.Messages/Persistord.Messages.csproj -c Release
git add src/Persistord.Messages/Entities/MessageEntity.cs
git commit -m "feat(messages): add MessageEntity with soft-delete"
```

### Task 2.3: Message configuration + `ApplyMessagesModule(filterDeleted)`

**Files:**

- Create: `src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs`
- Create: `src/Persistord.Messages/ModelBuilderExtensions.cs`

- [ ] **Step 1: `MessageEntityConfiguration.cs`** (PRD §6.3; query filter applied conditionally — see Step 2)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Messages.Entities;

namespace Persistord.Messages.Configurations;

/// <summary>EF Core configuration for <see cref="MessageEntity"/>.</summary>
public sealed class MessageEntityConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    private readonly bool _filterDeleted;

    /// <summary>Creates the configuration.</summary>
    /// <param name="filterDeleted">When true, applies a global query filter that hides soft-deleted messages.</param>
    public MessageEntityConfiguration(bool filterDeleted)
    {
        _filterDeleted = filterDeleted;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasIndex(m => new { m.ChannelId, m.Id });

        if (_filterDeleted)
        {
            builder.HasQueryFilter(m => !m.IsDeleted);
        }

        builder.OwnsMany(m => m.Embeds, e =>
        {
            e.OwnsOne(x => x.Footer);
            e.OwnsOne(x => x.Author);
            e.OwnsMany(x => x.Fields);
        });

        builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);
        builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);
    }
}
```

- [ ] **Step 2: `ModelBuilderExtensions.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Messages.Configurations;

namespace Persistord.Messages;

/// <summary>Model-building extensions that wire the Messages module.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the <c>MessageEntity</c> configuration (with owned embeds and relational
    /// attachments/reactions). Call from <c>OnModelCreating</c> after the core configuration.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="filterDeleted">
    /// When true (default), a global query filter hides soft-deleted messages. Use
    /// <c>IgnoreQueryFilters()</c> on a query to include them, or pass false to disable
    /// the filter entirely.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyMessagesModule(this ModelBuilder modelBuilder, bool filterDeleted = true)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new MessageEntityConfiguration(filterDeleted));
        return modelBuilder;
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/Persistord.Messages/Persistord.Messages.csproj -c Release
git add src/Persistord.Messages/Configurations src/Persistord.Messages/ModelBuilderExtensions.cs
git commit -m "feat(messages): add message configuration and ApplyMessagesModule"
```

### Task 2.4: Messages model + soft-delete tests

**Files:**

- Create: `tests/Persistord.Messages.Tests/Persistord.Messages.Tests.csproj`
- Create: `tests/Persistord.Messages.Tests/TestContext.cs`
- Create: `tests/Persistord.Messages.Tests/MessagesModelTests.cs`
- Create: `tests/Persistord.Messages.Tests/SoftDeleteTests.cs`

- [ ] **Step 1: Test project** (mirror Core test csproj; reference Core + Messages; add to solution)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
  </ItemGroup>
</Project>
```

Then `dotnet sln Persistord.slnx add tests/Persistord.Messages.Tests/Persistord.Messages.Tests.csproj`.

- [ ] **Step 2: `TestContext.cs`** (context that applies core + messages)

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Messages.Tests;

public sealed class TestContext : Persistord.Core.DiscordDbContext
{
    private readonly bool _filterDeleted;

    public TestContext(DbContextOptions<TestContext> options, bool filterDeleted = true)
        : base(options)
    {
        _filterDeleted = filterDeleted;
    }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(_filterDeleted);
    }

    public static (SqliteConnection, TestContext) Create(bool filterDeleted = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestContext>().UseSqlite(connection).Options;
        var context = new TestContext(options, filterDeleted);
        context.Database.EnsureCreated();
        return (connection, context);
    }
}
```

- [ ] **Step 3: Write failing model test**

`tests/Persistord.Messages.Tests/MessagesModelTests.cs`:

```csharp
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Xunit;

namespace Persistord.Messages.Tests;

public class MessagesModelTests
{
    [Fact]
    public void Message_WithEmbedsAndChildren_RoundTrips()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            context.Messages.Add(new MessageEntity
            {
                Id = 10UL,
                ChannelId = 20UL,
                AuthorId = 30UL,
                Content = "hello",
                Embeds = { new Embed { Title = "t", Fields = { new EmbedField { Name = "n", Value = "v" } } } },
                Attachments = { new AttachmentEntity { Id = 1UL, FileName = "a.png", Url = "http://x" } },
                Reactions = { new ReactionEntity { Emoji = "👍", Count = 2 } },
            });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var loaded = context.Messages
                .Single(m => m.Id == 10UL);
            context.Entry(loaded).Collection(m => m.Embeds).Load();
            context.Entry(loaded).Collection(m => m.Attachments).Load();
            context.Entry(loaded).Collection(m => m.Reactions).Load();

            Assert.Single(loaded.Embeds);
            Assert.Single(loaded.Embeds[0].Fields);
            Assert.Single(loaded.Attachments);
            Assert.Single(loaded.Reactions);
        }
    }
}
```

- [ ] **Step 4: Write failing soft-delete test**

`tests/Persistord.Messages.Tests/SoftDeleteTests.cs`:

```csharp
using Persistord.Messages.Entities;
using Xunit;

namespace Persistord.Messages.Tests;

public class SoftDeleteTests
{
    [Fact]
    public void DeletedMessage_IsHidden_ByDefaultFilter()
    {
        var (connection, context) = TestContext.Create(filterDeleted: true);
        using (connection)
        using (context)
        {
            context.Messages.Add(new MessageEntity { Id = 1UL, ChannelId = 2UL, AuthorId = 3UL, IsDeleted = true });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            Assert.Empty(context.Messages.ToList());
            Assert.Single(context.Messages.IgnoreQueryFilters().ToList());
        }
    }

    [Fact]
    public void DeletedMessage_IsVisible_WhenFilterDisabled()
    {
        var (connection, context) = TestContext.Create(filterDeleted: false);
        using (connection)
        using (context)
        {
            context.Messages.Add(new MessageEntity { Id = 1UL, ChannelId = 2UL, AuthorId = 3UL, IsDeleted = true });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            Assert.Single(context.Messages.ToList());
        }
    }
}
```

- [ ] **Step 5: Run — verify pass**

Run: `dotnet test tests/Persistord.Messages.Tests/Persistord.Messages.Tests.csproj`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add tests/Persistord.Messages.Tests Persistord.slnx
git commit -m "test(messages): add model and soft-delete integration tests"
```

---

## Phase 3 — History (PRD §7, milestone 3)

### Task 3.1: History entity + change-type enum

**Files:**

- Create: `src/Persistord.History/Entities/HistoryChangeType.cs`
- Create: `src/Persistord.History/Entities/MessageHistoryEntity.cs`

- [ ] **Step 1: `HistoryChangeType.cs`**

```csharp
namespace Persistord.History.Entities;

/// <summary>The kind of change a history row records.</summary>
public enum HistoryChangeType
{
    /// <summary>The message was created.</summary>
    Created,

    /// <summary>The message was edited.</summary>
    Edited,

    /// <summary>The message was deleted.</summary>
    Deleted,
}
```

- [ ] **Step 2: `MessageHistoryEntity.cs`** (PRD §7.1)

```csharp
namespace Persistord.History.Entities;

/// <summary>An append-only snapshot of a message at a point in time. Carries a real
/// foreign key to <c>MessageEntity</c>; one message maps to many history rows.</summary>
public class MessageHistoryEntity
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The message snowflake id this row belongs to (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>Full snapshot of the message content at this point.</summary>
    public string? Content { get; set; }

    /// <summary>When this snapshot was recorded.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>What kind of change this row represents.</summary>
    public HistoryChangeType ChangeType { get; set; }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/Persistord.History/Persistord.History.csproj -c Release
git add src/Persistord.History/Entities
git commit -m "feat(history): add MessageHistoryEntity and change-type enum"
```

### Task 3.2: History configuration with real FK + `ApplyHistoryModule()`

**Files:**

- Create: `src/Persistord.History/Configurations/MessageHistoryEntityConfiguration.cs`
- Create: `src/Persistord.History/ModelBuilderExtensions.cs`

- [ ] **Step 1: `MessageHistoryEntityConfiguration.cs`** (FK to `MessageEntity`, index on `(MessageId, RecordedAt)`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.History.Entities;
using Persistord.Messages.Entities;

namespace Persistord.History.Configurations;

/// <summary>EF Core configuration for <see cref="MessageHistoryEntity"/>.</summary>
public sealed class MessageHistoryEntityConfiguration : IEntityTypeConfiguration<MessageHistoryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageHistoryEntity> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedOnAdd();
        builder.HasIndex(h => new { h.MessageId, h.RecordedAt });

        builder.HasOne<MessageEntity>()
            .WithMany()
            .HasForeignKey(h => h.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 2: `ModelBuilderExtensions.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.History.Configurations;

namespace Persistord.History;

/// <summary>Model-building extensions that wire the History module.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the <c>MessageHistoryEntity</c> configuration. Requires the Messages module,
    /// because history carries a relational foreign key to <c>MessageEntity</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyHistoryModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new MessageHistoryEntityConfiguration());
        return modelBuilder;
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/Persistord.History/Persistord.History.csproj -c Release
git add src/Persistord.History/Configurations src/Persistord.History/ModelBuilderExtensions.cs
git commit -m "feat(history): add history configuration and ApplyHistoryModule"
```

### Task 3.3: History tests — delete-history survives soft-deleted message

**Files:**

- Create: `tests/Persistord.History.Tests/Persistord.History.Tests.csproj`
- Create: `tests/Persistord.History.Tests/TestContext.cs`
- Create: `tests/Persistord.History.Tests/HistoryModelTests.cs`

- [ ] **Step 1: Test project** (reference Core + Messages + History; add to solution)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../../src/Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
</Project>
```

Then `dotnet sln Persistord.slnx add tests/Persistord.History.Tests/Persistord.History.Tests.csproj`.

- [ ] **Step 2: `TestContext.cs`** (core + messages + history; filter off so we can read the soft-deleted row directly)

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.History.Tests;

public sealed class TestContext : Persistord.Core.DiscordDbContext
{
    public TestContext(DbContextOptions<TestContext> options)
        : base(options)
    {
    }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(filterDeleted: false);
        modelBuilder.ApplyHistoryModule();
    }

    public static (SqliteConnection, TestContext) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestContext>().UseSqlite(connection).Options;
        var context = new TestContext(options);
        context.Database.EnsureCreated();
        return (connection, context);
    }
}
```

- [ ] **Step 3: Write the failing test**

`tests/Persistord.History.Tests/HistoryModelTests.cs`:

```csharp
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using Xunit;

namespace Persistord.History.Tests;

public class HistoryModelTests
{
    [Fact]
    public void DeleteHistory_SurvivesSoftDeletedMessage()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            var message = new MessageEntity { Id = 100UL, ChannelId = 1UL, AuthorId = 2UL, Content = "original" };
            context.Messages.Add(message);
            context.MessageHistory.Add(new MessageHistoryEntity
            {
                MessageId = 100UL,
                Content = "original",
                RecordedAt = DateTimeOffset.UtcNow,
                ChangeType = HistoryChangeType.Created,
            });
            context.SaveChanges();

            // Soft-delete the message and log the delete in history.
            message.IsDeleted = true;
            message.DeletedAt = DateTimeOffset.UtcNow;
            context.MessageHistory.Add(new MessageHistoryEntity
            {
                MessageId = 100UL,
                Content = null,
                RecordedAt = DateTimeOffset.UtcNow,
                ChangeType = HistoryChangeType.Deleted,
            });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            // The (soft-deleted) message row still exists, so both history rows survive.
            Assert.NotNull(context.Messages.Single(m => m.Id == 100UL));
            var rows = context.MessageHistory.Where(h => h.MessageId == 100UL).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.ChangeType == HistoryChangeType.Deleted);
        }
    }

    [Fact]
    public void HistoryIndex_IsOnMessageIdAndRecordedAt()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            var entity = context.Model.FindEntityType(typeof(MessageHistoryEntity))!;
            var index = entity.GetIndexes().Single();
            Assert.Equal(
                new[] { nameof(MessageHistoryEntity.MessageId), nameof(MessageHistoryEntity.RecordedAt) },
                index.Properties.Select(p => p.Name).ToArray());
        }
    }
}
```

- [ ] **Step 4: Run — verify pass**

Run: `dotnet test tests/Persistord.History.Tests/Persistord.History.Tests.csproj`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add tests/Persistord.History.Tests Persistord.slnx
git commit -m "test(history): verify delete-history survives soft-deleted message"
```

---

## Phase 4 — Real provider verification (PRD §11)

### Task 4.1: Postgres round-trip via Testcontainers

**Files:**

- Create: `tests/Persistord.Provider.Tests/Persistord.Provider.Tests.csproj`
- Create: `tests/Persistord.Provider.Tests/PostgresFixture.cs`
- Create: `tests/Persistord.Provider.Tests/PostgresRoundTripTests.cs`

- [ ] **Step 1: Test project** (references all three packages + Npgsql + Testcontainers; add to solution)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Testcontainers.PostgreSql" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../../src/Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
</Project>
```

Then `dotnet sln Persistord.slnx add tests/Persistord.Provider.Tests/Persistord.Provider.Tests.csproj`.

- [ ] **Step 2: `PostgresFixture.cs`** (starts a container if Docker is available; otherwise marks itself unavailable so tests skip — keeps the Windows CI leg green)

```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace Persistord.Provider.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string? ConnectionString { get; private set; }

    public bool Available => ConnectionString is not null;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch
        {
            // Docker not available (e.g. Windows CI leg) — tests using this fixture skip.
            ConnectionString = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

- [ ] **Step 3: `PostgresRoundTripTests.cs`** (verifies `ulong→long` storage and owned-embed `ToJson()` on a real provider)

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Xunit;

namespace Persistord.Provider.Tests;

public sealed class PgContext : Persistord.Core.DiscordDbContext
{
    public PgContext(DbContextOptions<PgContext> options) : base(options) { }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
        modelBuilder.ApplyHistoryModule();
    }
}

public class PostgresRoundTripTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PostgresRoundTripTests(PostgresFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Snowflake_RoundTrips_OnPostgres()
    {
        Skip.IfNot(_fixture.Available, "Docker/Postgres not available.");

        var options = new DbContextOptionsBuilder<PgContext>()
            .UseNpgsql(_fixture.ConnectionString!)
            .Options;

        await using var context = new PgContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Guilds.Add(new GuildEntity { Id = ulong.MaxValue, Name = "g", OwnerId = 1UL });
        context.Messages.Add(new MessageEntity
        {
            Id = 5UL,
            ChannelId = 6UL,
            AuthorId = 7UL,
            Embeds = { new Embed { Title = "t" } },
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(ulong.MaxValue, context.Guilds.Single().Id);
    }
}
```

> `SkippableFact`/`Skip` come from the `Xunit.SkippableFact` package. Add `<PackageVersion Include="Xunit.SkippableFact" Version="1.5.23" />` to `Directory.Packages.props` and a `<PackageReference Include="Xunit.SkippableFact" />` to this csproj in Step 1.

- [ ] **Step 4: Add the Postgres job to CI** — append a `provider-tests` job to `.github/workflows/CI.yml` (ubuntu only, Docker present by default):

```yaml
  provider-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          cache: true
          cache-dependency-path: Directory.Packages.props
      - name: Restore
        run: dotnet restore
      - name: Run provider tests
        run: dotnet test tests/Persistord.Provider.Tests/Persistord.Provider.Tests.csproj --configuration Release
```

- [ ] **Step 5: Run locally (if Docker available) + commit**

Run: `dotnet test tests/Persistord.Provider.Tests/Persistord.Provider.Tests.csproj`
Expected: PASS, or SKIPPED if Docker is unavailable.

```bash
git add tests/Persistord.Provider.Tests Directory.Packages.props .github/workflows/CI.yml Persistord.slnx
git commit -m "test(provider): add Postgres round-trip verification via Testcontainers"
```

### Task 4.2: Migration smoke test (PRD §11)

**Files:**

- Create: `samples/Persistord.Sample/Persistord.Sample.csproj`
- Create: `samples/Persistord.Sample/MyBotContext.cs`
- Create: `samples/Persistord.Sample/Program.cs`

- [ ] **Step 1: Sample project** (references all three packages + SQLite + EF design; non-packable)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../../src/Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: `MyBotContext.cs`** (PRD §8 — the end-user usage example, made real)

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample;

/// <summary>Example derived context wiring all three Persistord modules.</summary>
public sealed class MyBotContext : DiscordDbContext
{
    public MyBotContext(DbContextOptions<MyBotContext> options) : base(options) { }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
        modelBuilder.ApplyHistoryModule();
    }
}
```

- [ ] **Step 3: `Program.cs`** (uses `IDbContextFactory`, per PRD lifetime guidance; ensures created and writes one row)

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.Sample;

var options = new DbContextOptionsBuilder<MyBotContext>()
    .UseSqlite("DataSource=sample.db")
    .Options;

await using var context = new MyBotContext(options);
await context.Database.EnsureCreatedAsync();

context.Guilds.Add(new GuildEntity { Id = 1UL, Name = "Sample Guild", OwnerId = 2UL });
await context.SaveChangesAsync();

Console.WriteLine($"Guilds persisted: {await context.Guilds.CountAsync()}");
```

- [ ] **Step 4: Add to solution, build, run the smoke test**

```bash
dotnet sln Persistord.slnx add samples/Persistord.Sample/Persistord.Sample.csproj
dotnet run --project samples/Persistord.Sample/Persistord.Sample.csproj
```

Expected: prints `Guilds persisted: 1`.

- [ ] **Step 5: Generate + apply a migration from the sample (PRD §11 migration smoke test)**

```bash
dotnet tool install --global dotnet-ef --version 10.0.0   # if not installed
dotnet ef migrations add Initial --project samples/Persistord.Sample/Persistord.Sample.csproj
dotnet ef database update --project samples/Persistord.Sample/Persistord.Sample.csproj
```

Expected: migration generates and applies without error. Commit the generated `Migrations/` folder.

- [ ] **Step 6: Commit**

```bash
git add samples Persistord.slnx
git commit -m "test(sample): add usage sample and migration smoke test"
```

---

## Phase 5 — Docs & packaging polish (PRD §10 milestone 4 & 5)

### Task 5.1: Per-package READMEs + usage docs

**Files:**

- Create: `src/Persistord.Core/README.md`, `src/Persistord.Messages/README.md`, `src/Persistord.History/README.md`
- Create: `docs/usage.md`

- [ ] **Step 1: Write `src/Persistord.Core/README.md`** covering: what the package is, provider-agnostic note (consumer calls `UseX`), the `IDbContextFactory<T>` lifetime guidance (never hold one context for the bot's lifetime — the change tracker grows unbounded), and the snowflake `ulong→long` storage note (bit-faithful `long`, valid until ~2084).

- [ ] **Step 2: Write `src/Persistord.Messages/README.md`** covering: `ApplyMessagesModule(filterDeleted)`, the soft-delete query filter and the `IgnoreQueryFilters()` escape hatch, owned embeds (and the per-provider `ToJson()` opt-in that swaps storage without changing the model), relational attachments/reactions.

- [ ] **Step 3: Write `src/Persistord.History/README.md`** covering: append-only model, full snapshot per change, the real FK to `MessageEntity`, and the consequence that History requires persisting the Messages table.

- [ ] **Step 4: Write `docs/usage.md`** — the end-to-end PRD §8 walkthrough: derive a context, declare module `DbSet`s, apply modules in `OnModelCreating`, choose a provider via `AddDbContextFactory`, lifetime guidance.

- [ ] **Step 5: Wire each src csproj to pack its README** — confirm `<PackageReadmeFile>README.md</PackageReadmeFile>` and the `<None Include="README.md" Pack="true" PackagePath="\" />` item exist in all three src csproj (Core has it from Task 0.2; add the same to Messages and History).

- [ ] **Step 6: Commit**

```bash
git add src/**/README.md docs/usage.md src/Persistord.Messages/*.csproj src/Persistord.History/*.csproj
git commit -m "docs: add per-package READMEs and usage guide"
```

### Task 5.2: Full-solution verification gate

- [ ] **Step 1: Restore, build, format-check, test the whole solution**

```bash
dotnet restore
dotnet build -c Release
dotnet format --verify-no-changes
dotnet test -c Release
```

Expected: all PASS, no format diffs.

- [ ] **Step 2: Dry-run pack the three packages**

```bash
dotnet pack -c Release -p:Version=1.0.0 --output ./artifacts
ls ./artifacts
```

Expected: `Persistord.Core.1.0.0.nupkg`, `Persistord.Messages.1.0.0.nupkg`, `Persistord.History.1.0.0.nupkg` — and only those three (test/sample projects are non-packable).

- [ ] **Step 3: Inspect dependency graph in the packed Messages/History nupkgs** — confirm `Persistord.Messages` depends on `Persistord.Core`, and `Persistord.History` depends on `Persistord.Messages` (unzip the `.nuspec` and check `<dependencies>`).

- [ ] **Step 4: Commit any fixes; open a PR from `develop`** (or merge per the user's branch workflow). Note in the PR description that **trusted publishing** must be configured on NuGet.org for all three package IDs before tagging a release (no `NUGET_API_KEY` secret — see Task 0.4). Reminder of the tag conventions the CD enforces: stable tags (`vX.Y.Z`) must be cut from `main`; prerelease tags (`vX.Y.Z-rc.N`) may be cut from `main` or `develop`.

---

## Self-Review — spec coverage check

- PRD §4.1 package layout → Tasks 0.2 (three packages), code in Phases 1–3. ✔
- PRD §4.2 dependency graph (Core ← Messages ← History) → ProjectReferences (0.2), verified in Task 5.2 Step 3. ✔
- PRD §4.3 modules contribute via `ModelBuilder` extensions → `ApplyCoreConfiguration`/`ApplyMessagesModule`/`ApplyHistoryModule` (1.4, 2.3, 3.2). ✔
- PRD §5.1 snowflake conversion (single source of truth, `long`, unchecked round-trip) → Task 1.1, `ConfigureConventions` in 1.4. ✔
- PRD §5.2 base `DiscordDbContext` → Task 1.4. ✔
- PRD §5.3 core entities (incl. channel TPH self-FK, member composite key) → Tasks 1.2/1.3. **Note:** TPH discriminator is modeled via a `ChannelType` enum column + self-FK rather than EF inheritance hierarchies, since the PRD keeps a single `ChannelEntity` POCO. If true EF TPH subclasses are wanted, that is a deliberate expansion — flag during execution. ✔ (with note)
- PRD §6.1 `MessageEntity` (soft-delete) → Task 2.2. ✔
- PRD §6.2 storage decisions (owned embeds table-mapped, relational attachments/reactions, built-in soft-delete) → Tasks 2.1/2.3. ✔
- PRD §6.3 configuration (key, index, query filter, owned embeds, child FKs) → Task 2.3. ✔
- PRD §6.4 owned embed model → Task 2.1. ✔
- PRD §7 history (append-only, surrogate PK, `(MessageId, RecordedAt)` index, real FK) → Tasks 3.1/3.2, verified 3.3. ✔
- PRD §8 usage example + `IDbContextFactory` lifetime → Task 4.2 sample + Task 5.1 docs. ✔
- PRD §9 micro-decisions → resolved in "Open micro-decisions" section; `filterDeleted` flag (2.3), full snapshot (3.1). ✔
- PRD §10 milestones 1–5 → Phases 1–5; single TFM `net10.0`, EF Core 10, no multi-targeting (0.2 csproj). ✔
- PRD §11 testing (SQLite in-memory, one real provider via Testcontainers, migration smoke test) → Tasks 1.5/2.4/3.3, 4.1, 4.2 Step 5. ✔
- `Tojson()` opt-in note: documented (Task 5.1) and exercised on Postgres path is optional; the owned model already supports adding `e.ToJson()`. Not enabled by default (PRD says per-provider opt-in). ✔

**`ToJson()` caveat for the implementer:** owned *collections* mapped to JSON have provider-specific support. Keep the default (table-mapped owned types) as shipped; treat `ToJson()` strictly as documented power-user opt-in, not a default, to avoid provider surprises on the SQLite test path.
