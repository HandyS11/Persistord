# Persistord.Adapters.NetCord Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Persistord.Adapters.NetCord`, an opt-in package of extension methods that map NetCord model types to Persistord entities.

**Architecture:** A single static `NetCordMappingExtensions` class exposes `.To*Entity()` extension methods. Unlike Discord.Net, NetCord exposes almost no mapping interfaces, so each mapper binds to the **concrete base class** that both the gateway and REST variants derive from (`RestGuild`, `User`, `GuildUser`, `Role`, `RestMessage`); only channels bind to an interface (`IGuildChannel`). Mappers are pure functions: they copy data fields only, never touch persistence-managed fields (`IsDeleted`, surrogate keys), and leave EF to fill foreign keys from navigation collections.

**Tech Stack:** .NET 10, NetCord 1.0.0-beta.19, xunit, NSubstitute, EF Core entities from `Persistord.Core`/`.Messages`/`.History`.

**Scope note:** This is sibling plan ② of the adapter sub-project. `Persistord.Adapters.DiscordNet` (plan `2026-06-14-persistord-adapter-discordnet.md`) is the shipped reference adapter and proves the API shape; DSharpPlus gets a third sibling plan.

**Spec:** `docs/superpowers/specs/2026-06-14-persistord-adapter-packages-design.md` (the NetCord half of §1–§5)

---

## Global Constraints

- **Target framework:** `net10.0`. Inherited from `Directory.Build.props`; do not restate in the csproj beyond the single `<TargetFramework>` line.
- **Nullable:** `enable` (inherited). **`TreatWarningsAsErrors` is `true`** — a warning fails the build. XML doc comments are required on every public member.
- **Analyzers are strict.** `AnalysisLevel=latest-all` with NetAnalyzers, Roslynator and SonarAnalyzer, all as errors. `.editorconfig` turns off 55 specific rules (including `CA1707` so underscored test names are fine, `CA1062` so nullable annotations carry argument validation, and `IDE0005` so an unused `using` is not fatal). **Anything not on that list is a build error** — notably `S3358` (nested ternaries) is still on.
- **Central package management** is on. Versions live in `Directory.Packages.props`; `PackageReference` in a csproj carries **no** `Version` attribute.
- **NetCord version range:** `[1.0.0-beta.19, 2.0.0)` — floor plus major ceiling, matching the existing `Discord.Net` entry `[3.20.1, 4.0.0)`. See "Spec drift" §3 below.
- **Method names are identical across all three adapters:** `ToGuildEntity`, `ToChannelEntity`, `ToUserEntity`, `ToMemberEntity`, `ToRoleEntity`, `ToMessageEntity`, `ToHistoryEntity`. Only the `this` parameter type differs.
- **No changes to `Persistord.Core` / `.Messages` / `.History`.** They stay Discord-library-agnostic. This plan adds files under `src/Persistord.Adapters.NetCord`, `tests/Persistord.Adapters.NetCord.Tests`, and edits only `Directory.Packages.props`, `Persistord.slnx`, `README.md`, `docs/articles/introduction.md`.
- **Mapping contract (spec §3):** map only data fields the source provides; never set `IsDeleted`/`DeletedAt`; leave DB-generated surrogate keys (`Embed.Id`, `EmbedField.Id`, `ReactionEntity.Id`) at `0`; add children to navigation collections and let EF fill their FKs; unknown channel types map to a documented default rather than throwing; the one throwing case is a null source (`ArgumentNullException`).
- **Author/licence metadata:** `Authors` = `HandyS11`, `PackageLicenseExpression` = `MIT`, packed `README.md`.

---

## Target entity fields (authoritative — read before writing any mapper)

These are the **current** shapes in the repo, after the bot-fit release (#50). They differ from the spec's §3 table, which was written 2026-06-14.

| Entity | Fields this adapter sets |
| --- | --- |
| `GuildEntity` | `Id` (`ulong`), `Name` (`string?`), `OwnerId` (`ulong?`) |
| `ChannelEntity` | `Id`, `GuildId`, `ParentId` (`ulong?`), `Type` (`ChannelType`), `Name` (`string`) |
| `UserEntity` | `Id`, `Username` (`string`), `GlobalName` (`string?`) |
| `MemberEntity` | `GuildId`, `UserId`, `Nickname` (`string?`), `JoinedAt` (`DateTimeOffset?`) |
| `RoleEntity` | `Id`, `GuildId`, `Name` (`string`), `Permissions` (`ulong`), `Color` (`int`) |
| `MessageEntity` | `Id`, `ChannelId`, `AuthorId`, `Content` (`string?`), `EditedAt` (`DateTimeOffset?`) + `Embeds`/`Attachments`/`Reactions` |
| `AttachmentEntity` | `Id` (snowflake), `FileName`, `Url` |
| `ReactionEntity` | `Emoji` (`string`), `Count` (`int`) — `Id` (`long`) left `0` |
| `Embed` (owned) | `Title?`, `Description?`, `Color` (`int?`), `Footer?`, `Author?`, `Fields` — `Id` (`long`) left `0` |
| `MessageHistoryEntity` | `MessageId`, `Content`, `RecordedAt`, `ChangeType` |

`ChannelType` has exactly four members: `Text = 0`, `Voice = 2`, `Category = 4`, `Thread = 11`.

**`GuildEntity.JoinedAt` and `GuildEntity.LeftAt` are deliberately NOT mapped.** They record when *the bot* joined and left the guild — bot-lifecycle state owned by the consumer's persistence logic, not data carried on a Discord guild object. Setting them from a mapper would silently overwrite the consumer's records. This is the same reasoning that keeps `IsDeleted`/`DeletedAt` unmapped.

---

## NetCord type facts (verified against netcord.dev, 2026-09-09)

Do not re-derive these; they were checked page by page.

**Channel hierarchy** — every class below implements `IGuildChannel`:

```
Channel (abstract)                     Id
├── TextChannel
│   └── TextGuildChannel               GuildId, Name, ParentId
│       ├── VoiceGuildChannel          + IVoiceGuildChannel
│       ├── StageGuildChannel          + IVoiceGuildChannel
│       ├── AnnouncementGuildChannel
│       └── GuildThread (abstract)
│           ├── PublicGuildThread
│           ├── PrivateGuildThread
│           ├── AnnouncementGuildThread
│           └── ForumGuildThread
├── CategoryGuildChannel               GuildId, Name — NO ParentId
└── ForumGuildChannel                  GuildId, Name, ParentId
    └── MediaForumGuildChannel
```

`IGuildChannel : INamedChannel, IEntity` gives `ulong GuildId`, `string Name`, `ulong Id`, `int? Position` — but **neither `ParentId` nor any channel-type member**.

**Other bindings:**

| NetCord type | Namespace | Notes |
| --- | --- | --- |
| `RestGuild` | `NetCord.Rest` | `Gateway.Guild : RestGuild`. `Id` (`ulong`), `Name` (`string`, non-null), `OwnerId` (`ulong`) |
| `User` | `NetCord` | base of `PartialGuildUser`, `CurrentUser`. `Id`, `Username` (`string`), `GlobalName` (`string?`) |
| `GuildUser` | `NetCord` | `GuildUser : PartialGuildUser : User`. **`GuildId` is declared on `GuildUser`, not on `PartialGuildUser`** — binding to the partial type would not compile. `Nickname` (`string?`) and `JoinedAt` (`DateTimeOffset?`) are inherited from `PartialGuildUser` |
| `Role` | `NetCord` | `Id`, `GuildId`, `Name` (`string`), `Permissions` (`[Flags] enum : ulong`), `Colors` (`RoleColors`) |
| `RoleColors` | `NetCord` | `PrimaryColor` (`Color`, non-null), `SecondaryColor`/`TertiaryColor` (`Color?`) |
| `Color` | `NetCord` | struct; `RawValue` is **`int`** — no cast needed, unlike Discord.Net's `uint` |
| `RestMessage` | `NetCord.Rest` | `Gateway.Message : RestMessage`. `Id`, `ChannelId`, `Author` (`User`), `Content` (`string`, non-null), `EditedAt` (`DateTimeOffset?`), `Embeds` (`IReadOnlyList<Embed>`), `Attachments` (`IReadOnlyList<Attachment>`), `Reactions` (`IReadOnlyList<MessageReaction>`) |
| `Attachment` | `NetCord` | `Id` (`ulong`), `FileName` (`string`), `Url` (`string`) |
| `MessageReaction` | `NetCord` | `Count` (`int`), `Emoji` (`MessageReactionEmoji`) |
| `MessageReactionEmoji` | `NetCord` | `Id` (`ulong?` — null for unicode), `Name` (`string?`) |
| `Embed` | `NetCord` | `Title?`, `Description?`, `Color` (`Color?`), `Footer` (`EmbedFooter?`), `Author` (`EmbedAuthor?`), `Fields` (`IReadOnlyList<EmbedField>`) |
| `EmbedFooter` | `NetCord` | `Text` (`string`, non-null), `IconUrl` (`string?`) |
| `EmbedAuthor` | `NetCord` | `Name` (`string?`), `Url` (`string?`) |
| `EmbedField` | `NetCord` | `Name` (`string`), `Value` (`string`), `Inline` (`bool`) |

**Nullability differs from Discord.Net.** NetCord declares `Name`, `Username`, `Content`, `FileName`, `Url` as non-nullable. The Discord.Net adapter writes `?? string.Empty` on these because Discord.Net declares them nullable. **Do not copy that defensive coalesce here** — on a non-nullable source it is dead code, and Roslynator/Sonar under `TreatWarningsAsErrors` may flag it. Assign directly.

---

## ⚠️ Namespace collision — read before writing the first `using`

The package namespace is `Persistord.Adapters.NetCord` and the library namespace is `NetCord`. Inside the package namespace, the identifier `NetCord` binds to **`Persistord.Adapters.NetCord` itself**, so a qualified reference like `NetCord.Embed` fails to compile with CS0234.

Rules that keep this from biting:

1. `using NetCord;` and `using NetCord.Rest;` sit **outside** the namespace declaration, at the top of the file, where they resolve globally. These are fine.
2. Never write a qualified `NetCord.Something` inside the namespace body. Use the unqualified name, or a `global::NetCord.` alias declared at the top of the file.
3. Four type names collide between `NetCord` and `Persistord.Messages.Owned`: `Embed`, `EmbedAuthor`, `EmbedField`, `EmbedFooter`. Resolve them with `using` aliases, exactly as the Discord.Net adapter does for its own collisions. The Persistord types get the plain alias (they appear far more often); the one NetCord type that must be named — the `MapEmbed` parameter — gets a `global::`-qualified alias.

The canonical header block, used verbatim in Tasks 2–5:

```csharp
using NetCord;
using NetCord.Rest;
using Persistord.Core.Entities;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using Embed = Persistord.Messages.Owned.Embed;
using EmbedAuthor = Persistord.Messages.Owned.EmbedAuthor;
using EmbedField = Persistord.Messages.Owned.EmbedField;
using EmbedFooter = Persistord.Messages.Owned.EmbedFooter;
using NetCordEmbed = global::NetCord.Embed;
```

---

## Spec drift — three places this plan overrides the 2026-06-14 spec

1. **§2 "NetCord binds to concrete model types because they do not expose comparable mapping interfaces."** Partly wrong. `IGuildChannel` exists and is the right binding for `ToChannelEntity`. The other six mappers do bind to concrete types — but to *base classes* (`RestGuild`, `User`, `RestMessage`) that cover both the gateway and REST variants, which is the same "works for `Socket*` and `Rest*`" property the spec credits to Discord.Net's interfaces.

2. **§3 mapping table.** Superseded by the "Target entity fields" table above: `GuildEntity.Name` is now `string?`, `OwnerId` is now `ulong?`, and `GuildEntity` has gained `JoinedAt`/`LeftAt`, which this adapter deliberately does not map.

3. **§1 "Versioning policy: minimum-version floor, no upper ceiling."** The repo did not adopt this. `Directory.Packages.props` uses floor-plus-major-ceiling ranges for every shipped dependency, with a comment explaining that these are the versions baked into the published nupkgs. Follow the repo. The NetCord README must document the range that is actually shipped, not the spec's policy.

**One risk the spec never anticipated:** NetCord has **no stable release** — 509 published versions, all prerelease, latest `1.0.0-beta.19`. Persistord currently ships as `1.0.0-beta4`, so a prerelease dependency raises no NuGet warning today. The moment Persistord publishes a stable `1.0.0`, `dotnet pack` on this project will emit **NU5104** ("a stable release should not have a prerelease dependency"), and `TreatWarningsAsErrors` will turn that into a hard pack failure. Task 6 records this in the README so it is not discovered during a release. Do not suppress NU5104 pre-emptively; there is nothing to suppress yet.

---

## Testability — what is and is not covered, and why

Spec §4 predicted that NetCord's concrete types would be "hard or impossible to construct in isolation" so that full field-by-field coverage is not feasible. That prediction is correct and worth stating precisely, because it drives the task structure:

- NetCord model types have non-public constructors taking `(JsonModel, RestClient)`, and **their properties are not `virtual`**. So they can neither be `new`-ed nor proxied by NSubstitute. `ToGuildEntity`, `ToUserEntity`, `ToMemberEntity`, `ToRoleEntity`, `ToMessageEntity` and `ToHistoryEntity` therefore have **no unit-testable happy path**.
- `IGuildChannel` *is* an interface, so `ToChannelEntity` is unit-testable — but only down the default arm, since the type switch keys off concrete classes a substitute can never be.

This plan claws back the two pieces of logic that would otherwise ship untested, by shaping them so they take **primitives instead of NetCord objects**:

- `MapChannelType(Type)` takes a `System.Type` rather than a channel instance, so every arm of the translation table is provable with `typeof(PublicGuildThread)` and friends — no instance required. This is strictly better coverage than the Discord.Net adapter achieves.
- `FormatEmoji(ulong?, string?)` takes the two fields it needs rather than a `MessageReaction`, so the custom-vs-unicode branch is provable.

Both are `internal`, exposed to the test assembly via `InternalsVisibleTo`. Everything else is covered by null-argument tests plus, ultimately, the flagship NetCord bot sample (spec's sub-project ③). **Say this in the README** rather than letting a reader assume the mappers are unverified by oversight.

---

## File Structure

**Created:**

- `src/Persistord.Adapters.NetCord/Persistord.Adapters.NetCord.csproj` — packable project, refs Core+Messages+History+NetCord, declares `InternalsVisibleTo`
- `src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs` — all seven `.To*Entity()` methods plus the private/internal helpers
- `src/Persistord.Adapters.NetCord/README.md` — packed readme (API, versioning, coverage note)
- `tests/Persistord.Adapters.NetCord.Tests/Persistord.Adapters.NetCord.Tests.csproj` — xunit + NSubstitute
- `tests/Persistord.Adapters.NetCord.Tests/stryker-config.json` — mutation-testing config, mirrors the Discord.Net one
- `tests/Persistord.Adapters.NetCord.Tests/ChannelTypeMappingTests.cs` — the full translation table
- `tests/Persistord.Adapters.NetCord.Tests/ChannelEntityMappingTests.cs` — field mapping via `IGuildChannel` substitute
- `tests/Persistord.Adapters.NetCord.Tests/EmojiFormattingTests.cs` — custom vs unicode emoji
- `tests/Persistord.Adapters.NetCord.Tests/NullArgumentTests.cs` — all seven guards

**Modified:**

- `Directory.Packages.props` — add the `NetCord` version range
- `Persistord.slnx` — add the two new projects
- `README.md` — package table row, install line, adapter prose, per-package README link
- `docs/articles/introduction.md` — adapter availability sentence and package count

One source file holds all seven mappers, matching `DiscordNetMappingExtensions.cs` (225 lines). The mappers share the alias header and the private helpers; splitting them across files would duplicate the header block and scatter one cohesive responsibility.

---

## Task 1: Project scaffolding & package wiring

**Files:**

- Modify: `Directory.Packages.props`
- Create: `src/Persistord.Adapters.NetCord/Persistord.Adapters.NetCord.csproj`
- Create: `tests/Persistord.Adapters.NetCord.Tests/Persistord.Adapters.NetCord.Tests.csproj`
- Create: `tests/Persistord.Adapters.NetCord.Tests/stryker-config.json`
- Modify: `Persistord.slnx`

**Interfaces:**

- Consumes: nothing.
- Produces: two buildable projects; the test project can see `internal` members of the adapter.

- [ ] **Step 1: Add the NetCord version range**

In `Directory.Packages.props`, in the first `<ItemGroup>` (the "Packages" group, whose comment explains floors-not-pins), add immediately above the `Discord.Net` line so the group stays alphabetical:

```xml
    <PackageVersion Include="Discord.Net" Version="[3.20.1, 4.0.0)" />
    <PackageVersion Include="Microsoft.AspNetCore.DataProtection.Abstractions" Version="[10.0.0, 11.0.0)" />
```

becomes:

```xml
    <PackageVersion Include="Discord.Net" Version="[3.20.1, 4.0.0)" />
    <!-- NetCord has no stable release; 1.0.0-beta.19 is the latest of 509 prereleases.
         Ranged like the other shipped deps: floor = the version the nupkg is compiled
         against, ceiling = the next major. See the NU5104 note in the adapter README
         before publishing Persistord as a stable 1.0.0. -->
    <PackageVersion Include="NetCord" Version="[1.0.0-beta.19, 2.0.0)" />
    <PackageVersion Include="Microsoft.AspNetCore.DataProtection.Abstractions" Version="[10.0.0, 11.0.0)" />
```

- [ ] **Step 2: Create the adapter project**

Create `src/Persistord.Adapters.NetCord/Persistord.Adapters.NetCord.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.Adapters.NetCord</PackageId>
    <Description>NetCord adapter for Persistord: extension methods mapping NetCord model types to Persistord entities.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>discord;netcord;efcore;persistence;adapter</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NetCord" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- MapChannelType and FormatEmoji are internal so they stay out of the public API
         while remaining unit-testable; NetCord model types cannot be constructed or
         mocked, so these two helpers carry the adapter's real test coverage. -->
    <InternalsVisibleTo Include="Persistord.Adapters.NetCord.Tests" />
  </ItemGroup>
  <ItemGroup>
    <None Include="$(MSBuildProjectDirectory)/README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create the test project**

Create `tests/Persistord.Adapters.NetCord.Tests/Persistord.Adapters.NetCord.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Adapters.NetCord/Persistord.Adapters.NetCord.csproj" />
  </ItemGroup>
</Project>
```

Note: unlike `Persistord.Adapters.DiscordNet.Tests`, this project does **not** reference `Persistord.Testing`. That reference is unused in the Discord.Net test project too — no test there touches `SqliteTestDatabase` — and these mappers are pure functions that never open a database.

- [ ] **Step 4: Create the Stryker config**

Create `tests/Persistord.Adapters.NetCord.Tests/stryker-config.json`:

```json
{
  "stryker-config": {
    "reporters": [
      "html",
      "cleartext"
    ],
    "project-info": {
      "name": "github.com/HandyS11/Persistord",
      "module": "Persistord.Adapters.NetCord"
    },
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 0
    }
  }
}
```

- [ ] **Step 5: Add both projects to the solution**

In `Persistord.slnx`, add to the `/src/` folder, keeping alphabetical order (after `Persistord.Adapters.DiscordNet`):

```xml
    <Project Path="src/Persistord.Adapters.NetCord/Persistord.Adapters.NetCord.csproj" />
```

and to the `/tests/` folder, after `Persistord.Adapters.DiscordNet.Tests`:

```xml
    <Project Path="tests/Persistord.Adapters.NetCord.Tests/Persistord.Adapters.NetCord.Tests.csproj" />
```

- [ ] **Step 6: Verify the solution restores and builds**

Run: `dotnet build Persistord.slnx`
Expected: PASS. Both new projects appear in the build output. A restore error naming `NetCord` means the prerelease range was mistyped — NuGet needs the full `1.0.0-beta.19` including the `.19`, and the range must not be quoted differently from the sibling entries.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props Persistord.slnx src/Persistord.Adapters.NetCord tests/Persistord.Adapters.NetCord.Tests
git commit -m "feat(adapters): scaffold Persistord.Adapters.NetCord package and test project"
```

---

## Task 2: Channel-type translation table

NetCord encodes channel kind in the **class**, not in a property — there is no `Type` member anywhere on `Channel` or `IGuildChannel`. This helper centralises the translation to Persistord's four-member `ChannelType`.

It takes a `System.Type` rather than a channel instance. That is deliberate: NetCord channels cannot be constructed or mocked, so an instance-keyed switch would be entirely untestable, while a `Type`-keyed one is fully provable with `typeof(...)`. `ToChannelEntity` (Task 3) calls it as `MapChannelType(channel.GetType())`, which is semantically identical to a type-pattern switch on the instance.

**Files:**

- Create: `src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.NetCord.Tests/ChannelTypeMappingTests.cs`

**Interfaces:**

- Consumes: Task 1's projects.
- Produces: `internal static ChannelType MapChannelType(Type channelType)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Adapters.NetCord.Tests/ChannelTypeMappingTests.cs`:

```csharp
using NetCord;
using Persistord.Adapters.NetCord;
using Xunit;
using ChannelType = Persistord.Core.Entities.ChannelType;

namespace Persistord.Adapters.NetCord.Tests;

/// <summary>
/// Covers every arm of the class-to-ChannelType table. These use typeof(...) rather
/// than instances because NetCord channels have non-public constructors and
/// non-virtual properties, so they can be neither constructed nor substituted.
/// </summary>
public class ChannelTypeMappingTests
{
    [Fact]
    public void TextGuildChannel_maps_to_Text() =>
        Assert.Equal(ChannelType.Text, NetCordMappingExtensions.MapChannelType(typeof(TextGuildChannel)));

    [Fact]
    public void AnnouncementGuildChannel_maps_to_Text() =>
        Assert.Equal(ChannelType.Text, NetCordMappingExtensions.MapChannelType(typeof(AnnouncementGuildChannel)));

    [Fact]
    public void VoiceGuildChannel_maps_to_Voice() =>
        Assert.Equal(ChannelType.Voice, NetCordMappingExtensions.MapChannelType(typeof(VoiceGuildChannel)));

    [Fact]
    public void StageGuildChannel_maps_to_Voice() =>
        Assert.Equal(ChannelType.Voice, NetCordMappingExtensions.MapChannelType(typeof(StageGuildChannel)));

    [Fact]
    public void CategoryGuildChannel_maps_to_Category() =>
        Assert.Equal(ChannelType.Category, NetCordMappingExtensions.MapChannelType(typeof(CategoryGuildChannel)));

    [Fact]
    public void PublicGuildThread_maps_to_Thread() =>
        Assert.Equal(ChannelType.Thread, NetCordMappingExtensions.MapChannelType(typeof(PublicGuildThread)));

    [Fact]
    public void PrivateGuildThread_maps_to_Thread() =>
        Assert.Equal(ChannelType.Thread, NetCordMappingExtensions.MapChannelType(typeof(PrivateGuildThread)));

    [Fact]
    public void AnnouncementGuildThread_maps_to_Thread() =>
        Assert.Equal(ChannelType.Thread, NetCordMappingExtensions.MapChannelType(typeof(AnnouncementGuildThread)));

    [Fact]
    public void GuildThread_base_maps_to_Thread() =>
        Assert.Equal(ChannelType.Thread, NetCordMappingExtensions.MapChannelType(typeof(GuildThread)));

    [Fact]
    public void ForumGuildChannel_falls_back_to_Text() =>
        Assert.Equal(ChannelType.Text, NetCordMappingExtensions.MapChannelType(typeof(ForumGuildChannel)));

    [Fact]
    public void MediaForumGuildChannel_falls_back_to_Text() =>
        Assert.Equal(ChannelType.Text, NetCordMappingExtensions.MapChannelType(typeof(MediaForumGuildChannel)));

    [Fact]
    public void Bare_IGuildChannel_falls_back_to_Text() =>
        Assert.Equal(ChannelType.Text, NetCordMappingExtensions.MapChannelType(typeof(IGuildChannel)));
}
```

The threads matter most here. `GuildThread` derives from `TextGuildChannel`, and `VoiceGuildChannel`/`StageGuildChannel` do too — so if the arms are ordered wrong, threads and voice channels silently become `Text` and these tests are the only thing that catches it.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.NetCord.Tests --filter ChannelTypeMappingTests`
Expected: FAIL to compile — `NetCordMappingExtensions` does not exist.

- [ ] **Step 3: Create the extensions file with the type helper**

Create `src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs`:

```csharp
using NetCord;
using Persistord.Core.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;

namespace Persistord.Adapters.NetCord;

/// <summary>
/// Extension methods mapping NetCord model types to Persistord entities.
/// Mappers copy data fields only; persistence-managed fields and EF-generated keys
/// are left at their defaults.
/// </summary>
public static class NetCordMappingExtensions
{
    /// <summary>
    /// Maps a NetCord channel class to a <see cref="ChannelType"/>.
    /// <para>
    /// NetCord carries no channel-type property: the kind is the class itself. Order
    /// matters, because <c>GuildThread</c>, <c>VoiceGuildChannel</c> and
    /// <c>StageGuildChannel</c> all derive from <c>TextGuildChannel</c> — the more
    /// derived arms must be tested first or they fall through to the text fallback.
    /// </para>
    /// <para>
    /// Takes a <see cref="Type"/> rather than a channel instance so the table stays
    /// unit-testable: NetCord channels have non-public constructors and non-virtual
    /// properties, so no test can build or mock one.
    /// </para>
    /// </summary>
    /// <param name="channelType">The runtime type of the channel to classify.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channelType"/> is <see langword="null"/>.</exception>
    internal static ChannelType MapChannelType(Type channelType)
    {
        ArgumentNullException.ThrowIfNull(channelType);

        if (channelType.IsAssignableTo(typeof(GuildThread)))
        {
            return ChannelType.Thread;
        }

        if (channelType.IsAssignableTo(typeof(IVoiceGuildChannel)))
        {
            return ChannelType.Voice;
        }

        if (channelType.IsAssignableTo(typeof(CategoryGuildChannel)))
        {
            return ChannelType.Category;
        }

        // covers TextGuildChannel, AnnouncementGuildChannel, forum, media forum,
        // directory, and any channel class NetCord adds later
        return ChannelType.Text;
    }
}
```

`IVoiceGuildChannel` is used rather than the concrete `VoiceGuildChannel` because `StageGuildChannel` implements that interface while deriving from `TextGuildChannel` — matching on the interface catches both audio kinds in one arm.

Written as a sequence of `if` statements rather than a ternary chain on purpose. The
repo runs `SonarAnalyzer.CSharp` at `AnalysisLevel=latest-all` with
`TreatWarningsAsErrors`, and **S3358 (nested ternary operators) is not suppressed** in
`.editorconfig` — a chained ternary here fails the build rather than merely warning.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.NetCord.Tests --filter ChannelTypeMappingTests`
Expected: PASS, 12 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs tests/Persistord.Adapters.NetCord.Tests/ChannelTypeMappingTests.cs
git commit -m "feat(adapters): map NetCord channel classes to Persistord ChannelType"
```

---

## Task 3: Channel entity mapper

**Files:**

- Modify: `src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.NetCord.Tests/ChannelEntityMappingTests.cs`

**Interfaces:**

- Consumes: `MapChannelType(Type)` from Task 2.
- Produces: `public static ChannelEntity ToChannelEntity(this IGuildChannel channel)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Adapters.NetCord.Tests/ChannelEntityMappingTests.cs`:

```csharp
using NetCord;
using NSubstitute;
using Persistord.Adapters.NetCord;
using Xunit;
using ChannelType = Persistord.Core.Entities.ChannelType;

namespace Persistord.Adapters.NetCord.Tests;

/// <summary>
/// Field-level coverage for <c>ToChannelEntity</c>. IGuildChannel is the one NetCord
/// binding that is an interface, so it is the one mapper NSubstitute can drive — but
/// only down the fallback arm, since a substitute is never a concrete channel class.
/// The concrete arms are covered by <see cref="ChannelTypeMappingTests"/>.
/// </summary>
public class ChannelEntityMappingTests
{
    private static IGuildChannel CreateChannel()
    {
        var channel = Substitute.For<IGuildChannel>();
        channel.Id.Returns(915_000_000_000_000_001UL);
        channel.GuildId.Returns(842_000_000_000_000_002UL);
        channel.Name.Returns("general");
        return channel;
    }

    [Fact]
    public void Maps_id() => Assert.Equal(915_000_000_000_000_001UL, CreateChannel().ToChannelEntity().Id);

    [Fact]
    public void Maps_guild_id() => Assert.Equal(842_000_000_000_000_002UL, CreateChannel().ToChannelEntity().GuildId);

    [Fact]
    public void Maps_name() => Assert.Equal("general", CreateChannel().ToChannelEntity().Name);

    [Fact]
    public void Falls_back_to_Text_for_a_bare_guild_channel() =>
        Assert.Equal(ChannelType.Text, CreateChannel().ToChannelEntity().Type);

    [Fact]
    public void Leaves_parent_id_null_when_the_channel_exposes_none() =>
        Assert.Null(CreateChannel().ToChannelEntity().ParentId);

    [Fact]
    public void Round_trips_a_snowflake_near_ulong_MaxValue()
    {
        var channel = Substitute.For<IGuildChannel>();
        channel.Id.Returns(ulong.MaxValue - 1);
        channel.GuildId.Returns(ulong.MaxValue - 2);
        channel.Name.Returns("edge");

        var entity = channel.ToChannelEntity();

        Assert.Equal(ulong.MaxValue - 1, entity.Id);
        Assert.Equal(ulong.MaxValue - 2, entity.GuildId);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.NetCord.Tests --filter ChannelEntityMappingTests`
Expected: FAIL to compile — `ToChannelEntity` does not exist.

- [ ] **Step 3: Add the mapper**

In `NetCordMappingExtensions.cs`, add this method **above** `MapChannelType` (public API first, helpers last):

```csharp
    /// <summary>Maps a NetCord guild channel to a <see cref="ChannelEntity"/>.</summary>
    /// <remarks>
    /// <c>IGuildChannel</c> carries <c>Id</c>, <c>GuildId</c> and <c>Name</c> but neither
    /// a parent nor a channel kind, so both are recovered from the concrete class:
    /// <c>ParentId</c> lives on <c>TextGuildChannel</c> (and so on its voice, stage,
    /// announcement and thread subclasses) and separately on <c>ForumGuildChannel</c>;
    /// categories have no parent by definition.
    /// </remarks>
    /// <param name="channel">The guild channel to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public static ChannelEntity ToChannelEntity(this IGuildChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ChannelEntity
        {
            Id = channel.Id,
            GuildId = channel.GuildId,
            ParentId = channel switch
            {
                TextGuildChannel text => text.ParentId,
                ForumGuildChannel forum => forum.ParentId,
                _ => null,
            },
            Type = MapChannelType(channel.GetType()),
            Name = channel.Name,
        };
    }
```

`channel.Name` is assigned directly: NetCord declares `INamedChannel.Name` as non-nullable `string`, unlike Discord.Net's nullable one, so the `?? string.Empty` used in the Discord.Net adapter would be dead code here.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dtk dotnet test tests/Persistord.Adapters.NetCord.Tests --filter ChannelEntityMappingTests`
Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs tests/Persistord.Adapters.NetCord.Tests/ChannelEntityMappingTests.cs
git commit -m "feat(adapters): map NetCord guild channels to ChannelEntity"
```

---

## Task 4: Guild, user, member and role mappers

These four have no unit-testable happy path — `RestGuild`, `User`, `GuildUser` and `Role` can be neither constructed nor mocked. They are grouped into one task because they are four near-identical field copies that a reviewer will assess as a single unit, and because their only automated coverage (the null guards, Task 6's build, and eventually the sample bot) is shared.

**Files:**

- Modify: `src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs`

**Interfaces:**

- Consumes: Task 1's project references.
- Produces:
  - `public static GuildEntity ToGuildEntity(this RestGuild guild)`
  - `public static UserEntity ToUserEntity(this User user)`
  - `public static MemberEntity ToMemberEntity(this GuildUser member)`
  - `public static RoleEntity ToRoleEntity(this Role role)`

- [ ] **Step 1: Extend the using block**

At the top of `NetCordMappingExtensions.cs`, replace the three-line using block from Task 2 with:

```csharp
using NetCord;
using NetCord.Rest;
using Persistord.Core.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
```

`NetCord.Rest` brings in `RestGuild`. The remaining aliases arrive in Task 5, when the embed types collide.

- [ ] **Step 2: Add the four mappers**

Add below `ToChannelEntity`, above `MapChannelType`:

```csharp
    /// <summary>Maps a NetCord guild to a <see cref="GuildEntity"/>.</summary>
    /// <remarks>
    /// Binds <c>RestGuild</c> rather than the gateway <c>Guild</c> because the gateway
    /// type derives from it, so one method serves both. <c>JoinedAt</c> and
    /// <c>LeftAt</c> are intentionally not set: they track the bot's own membership
    /// lifecycle, which the consumer owns, not data carried on a Discord guild.
    /// </remarks>
    /// <param name="guild">The guild to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guild"/> is <see langword="null"/>.</exception>
    public static GuildEntity ToGuildEntity(this RestGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        return new GuildEntity
        {
            Id = guild.Id, Name = guild.Name, OwnerId = guild.OwnerId,
        };
    }

    /// <summary>Maps a NetCord user to a <see cref="UserEntity"/>.</summary>
    /// <param name="user">The user to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
    public static UserEntity ToUserEntity(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserEntity
        {
            Id = user.Id, Username = user.Username, GlobalName = user.GlobalName,
        };
    }

    /// <summary>Maps a NetCord guild member to a <see cref="MemberEntity"/>.</summary>
    /// <remarks>
    /// Binds <c>GuildUser</c>, not its <c>PartialGuildUser</c> base: the partial type
    /// deliberately omits <c>GuildId</c>, which is half of <see cref="MemberEntity"/>'s
    /// composite key.
    /// </remarks>
    /// <param name="member">The guild member to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public static MemberEntity ToMemberEntity(this GuildUser member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberEntity
        {
            GuildId = member.GuildId, UserId = member.Id, Nickname = member.Nickname, JoinedAt = member.JoinedAt,
        };
    }

    /// <summary>Maps a NetCord role to a <see cref="RoleEntity"/>.</summary>
    /// <remarks>
    /// <c>Permissions</c> is a <c>[Flags] enum : ulong</c>, so the cast is lossless.
    /// <c>Color</c> takes the role's primary colour; NetCord's <c>Color.RawValue</c> is
    /// already <c>int</c>, so unlike the Discord.Net adapter no unchecked cast is needed.
    /// Gradient and holographic roles' secondary and tertiary colours are dropped —
    /// <see cref="RoleEntity"/> stores a single colour.
    /// </remarks>
    /// <param name="role">The role to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="role"/> is <see langword="null"/>.</exception>
    public static RoleEntity ToRoleEntity(this Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleEntity
        {
            Id = role.Id,
            GuildId = role.GuildId,
            Name = role.Name,
            Permissions = (ulong)role.Permissions,
            Color = role.Colors.PrimaryColor.RawValue,
        };
    }
```

- [ ] **Step 3: Verify the project still builds**

Run: `dotnet build Persistord.slnx`
Expected: PASS. A CS0234 here almost certainly means a qualified `NetCord.X` crept into the namespace body — see the namespace-collision section.

- [ ] **Step 4: Run the existing tests to confirm nothing regressed**

Run: `dotnet test tests/Persistord.Adapters.NetCord.Tests`
Expected: PASS, 18 tests (12 from Task 2, 6 from Task 3).

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs
git commit -m "feat(adapters): map NetCord guild, user, member, role to Persistord entities"
```

---

## Task 5: Message, emoji and history mappers

**Files:**

- Modify: `src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.NetCord.Tests/EmojiFormattingTests.cs`
- Test: `tests/Persistord.Adapters.NetCord.Tests/NullArgumentTests.cs`

**Interfaces:**

- Consumes: everything above.
- Produces:
  - `public static MessageEntity ToMessageEntity(this RestMessage message)`
  - `public static MessageHistoryEntity ToHistoryEntity(this RestMessage message, HistoryChangeType changeType)`
  - `internal static string FormatEmoji(ulong? id, string? name)`
  - `private static Embed MapEmbed(NetCordEmbed embed)`

- [ ] **Step 1: Write the failing emoji tests**

Create `tests/Persistord.Adapters.NetCord.Tests/EmojiFormattingTests.cs`:

```csharp
using Persistord.Adapters.NetCord;
using Xunit;

namespace Persistord.Adapters.NetCord.Tests;

/// <summary>
/// Covers reaction-emoji formatting. Takes the two fields rather than a
/// MessageReaction so the branch is testable — NetCord reactions cannot be built.
/// </summary>
public class EmojiFormattingTests
{
    [Fact]
    public void Custom_emoji_keeps_name_and_snowflake() =>
        Assert.Equal("blobwave:842000000000000003", NetCordMappingExtensions.FormatEmoji(842_000_000_000_000_003UL, "blobwave"));

    [Fact]
    public void Unicode_emoji_is_stored_raw() =>
        Assert.Equal("👍", NetCordMappingExtensions.FormatEmoji(null, "👍"));

    [Fact]
    public void Missing_name_and_id_becomes_empty() =>
        Assert.Equal(string.Empty, NetCordMappingExtensions.FormatEmoji(null, null));

    [Fact]
    public void Custom_emoji_with_missing_name_still_keeps_the_snowflake() =>
        Assert.Equal(":842000000000000003", NetCordMappingExtensions.FormatEmoji(842_000_000_000_000_003UL, null));
}
```

The format matches the Discord.Net adapter's `name:id` exactly, so a consumer that switches libraries reads back the same stored strings.

- [ ] **Step 2: Run to verify they fail**

Run: `dtk dotnet test tests/Persistord.Adapters.NetCord.Tests --filter EmojiFormattingTests`
Expected: FAIL to compile — `FormatEmoji` does not exist.

- [ ] **Step 3: Complete the using block**

Replace the using block at the top of `NetCordMappingExtensions.cs` with the full canonical header:

```csharp
using NetCord;
using NetCord.Rest;
using Persistord.Core.Entities;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using Embed = Persistord.Messages.Owned.Embed;
using EmbedAuthor = Persistord.Messages.Owned.EmbedAuthor;
using EmbedField = Persistord.Messages.Owned.EmbedField;
using EmbedFooter = Persistord.Messages.Owned.EmbedFooter;
using NetCordEmbed = global::NetCord.Embed;
```

- [ ] **Step 4: Add the message, history and helper methods**

Add `ToMessageEntity` and `ToHistoryEntity` after `ToRoleEntity`; add `FormatEmoji` and `MapEmbed` after `MapChannelType`:

```csharp
    /// <summary>
    /// Maps a NetCord message to a <see cref="MessageEntity"/>, including embeds,
    /// attachments, and reactions. Soft-delete state and EF-generated keys are left
    /// at their defaults; child foreign keys are filled by EF from the navigation
    /// collections on save.
    /// </summary>
    /// <remarks>
    /// Binds <c>RestMessage</c> so the gateway <c>Message</c>, which derives from it,
    /// maps through the same method.
    /// </remarks>
    /// <param name="message">The message to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageEntity ToMessageEntity(this RestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entity = new MessageEntity
        {
            Id = message.Id,
            ChannelId = message.ChannelId,
            AuthorId = message.Author.Id,
            Content = message.Content,
            EditedAt = message.EditedAt,
        };

        foreach (var attachment in message.Attachments)
        {
            entity.Attachments.Add(new AttachmentEntity
            {
                Id = attachment.Id, FileName = attachment.FileName, Url = attachment.Url,
            });
        }

        foreach (var reaction in message.Reactions)
        {
            entity.Reactions.Add(new ReactionEntity
            {
                Emoji = FormatEmoji(reaction.Emoji.Id, reaction.Emoji.Name), Count = reaction.Count,
            });
        }

        foreach (var embed in message.Embeds)
        {
            entity.Embeds.Add(MapEmbed(embed));
        }

        return entity;
    }

    /// <summary>
    /// Builds a <see cref="MessageHistoryEntity"/> snapshot of a message for the given
    /// change type. <see cref="MessageHistoryEntity.RecordedAt"/> is stamped with the
    /// current UTC time; the surrogate key is left for EF to assign.
    /// </summary>
    /// <param name="message">The message to snapshot.</param>
    /// <param name="changeType">The kind of change this snapshot records.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageHistoryEntity ToHistoryEntity(this RestMessage message, HistoryChangeType changeType)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageHistoryEntity
        {
            MessageId = message.Id,
            Content = message.Content,
            RecordedAt = DateTimeOffset.UtcNow,
            ChangeType = changeType,
        };
    }

    /// <summary>
    /// Formats a reaction emoji for storage: custom emoji become <c>name:id</c>
    /// (preserving the snowflake), while unicode emoji are stored as their raw name.
    /// Matches the Discord.Net adapter's format so stored values are portable.
    /// </summary>
    /// <param name="id">The custom emoji's snowflake, or <see langword="null"/> for a unicode emoji.</param>
    /// <param name="name">The emoji name, or the unicode character itself.</param>
    internal static string FormatEmoji(ulong? id, string? name) =>
        id is { } emojiId ? $"{name}:{emojiId}" : name ?? string.Empty;

    /// <summary>Maps a NetCord embed to a Persistord <see cref="Embed"/>.</summary>
    /// <param name="embed">The NetCord embed to map.</param>
    private static Embed MapEmbed(NetCordEmbed embed)
    {
        var mapped = new Embed
        {
            Title = embed.Title,
            Description = embed.Description,
            Color = embed.Color?.RawValue,
        };

        if (embed.Footer is { } footer)
        {
            mapped.Footer = new EmbedFooter
            {
                Text = footer.Text, IconUrl = footer.IconUrl,
            };
        }

        if (embed.Author is { } author)
        {
            mapped.Author = new EmbedAuthor
            {
                Name = author.Name, Url = author.Url,
            };
        }

        foreach (var field in embed.Fields)
        {
            mapped.Fields.Add(new EmbedField
            {
                Name = field.Name, Value = field.Value, Inline = field.Inline,
            });
        }

        return mapped;
    }
```

- [ ] **Step 5: Run the emoji tests to verify they pass**

Run: `dtk dotnet test tests/Persistord.Adapters.NetCord.Tests --filter EmojiFormattingTests`
Expected: PASS, 4 tests.

- [ ] **Step 6: Write the null-argument tests**

Create `tests/Persistord.Adapters.NetCord.Tests/NullArgumentTests.cs`:

```csharp
using NetCord;
using NetCord.Rest;
using Persistord.Adapters.NetCord;
using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.NetCord.Tests;

/// <summary>
/// Verifies every public mapper rejects a null source. These pin the
/// <c>ArgumentNullException.ThrowIfNull</c> guards against mutation, and are the only
/// automated coverage the guild, user, member, role and message mappers can have —
/// NetCord model types cannot be constructed or substituted.
/// </summary>
public class NullArgumentTests
{
    [Fact]
    public void ToChannelEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IGuildChannel)null!).ToChannelEntity());

    [Fact]
    public void ToGuildEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((RestGuild)null!).ToGuildEntity());

    [Fact]
    public void ToUserEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((User)null!).ToUserEntity());

    [Fact]
    public void ToRoleEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((Role)null!).ToRoleEntity());

    [Fact]
    public void ToMemberEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((GuildUser)null!).ToMemberEntity());

    [Fact]
    public void ToMessageEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((RestMessage)null!).ToMessageEntity());

    [Fact]
    public void ToHistoryEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((RestMessage)null!).ToHistoryEntity(HistoryChangeType.Created));

    [Fact]
    public void MapChannelType_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => NetCordMappingExtensions.MapChannelType(null!));
}
```

- [ ] **Step 7: Run the full suite**

Run: `dtk dotnet test tests/Persistord.Adapters.NetCord.Tests`
Expected: PASS, 30 tests (12 + 6 + 4 + 8).

- [ ] **Step 8: Commit**

```bash
git add src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs tests/Persistord.Adapters.NetCord.Tests/EmojiFormattingTests.cs tests/Persistord.Adapters.NetCord.Tests/NullArgumentTests.cs
git commit -m "feat(adapters): map NetCord messages, reactions and history snapshots"
```

---

## Task 6: Package README, docs wiring and pack verification

**Files:**

- Create: `src/Persistord.Adapters.NetCord/README.md`
- Modify: `README.md`
- Modify: `docs/articles/introduction.md`

**Interfaces:**

- Consumes: the finished adapter.
- Produces: a packable nupkg with a readme; docs that name the package.

- [ ] **Step 1: Write the package README**

Create `src/Persistord.Adapters.NetCord/README.md`:

````markdown
# Persistord.Adapters.NetCord

Opt-in [NetCord](https://netcord.dev) adapter for [Persistord](https://github.com/HandyS11/Persistord):
extension methods mapping NetCord model types to Persistord entities.

The core Persistord packages never reference a Discord client library. Install this
package **only** if your bot uses NetCord.

## Install

```bash
dotnet add package Persistord.Adapters.NetCord
```

## API

```csharp
using Persistord.Adapters.NetCord;
using Persistord.History.Entities;

db.Messages.Add(message.ToMessageEntity());   // embeds, attachments, reactions included
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

| Method | Binds to | Produces |
| --- | --- | --- |
| `ToGuildEntity()` | `RestGuild` | `GuildEntity` |
| `ToChannelEntity()` | `IGuildChannel` | `ChannelEntity` |
| `ToUserEntity()` | `User` | `UserEntity` |
| `ToMemberEntity()` | `GuildUser` | `MemberEntity` |
| `ToRoleEntity()` | `Role` | `RoleEntity` |
| `ToMessageEntity()` | `RestMessage` | `MessageEntity` |
| `ToHistoryEntity(changeType)` | `RestMessage` | `MessageHistoryEntity` |

Each mapper binds the base type that both the gateway and REST variants derive from,
so `Gateway.Guild` and `Gateway.Message` map through the same methods as their `Rest*`
counterparts.

## What the mappers do not touch

Mappers are pure functions over data fields. They never set `IsDeleted`/`DeletedAt`,
never set EF-generated surrogate keys (`Embed.Id`, `ReactionEntity.Id`), and never set
child foreign keys — children are added to the parent's navigation collection and EF
fills the keys on save. `GuildEntity.JoinedAt`/`LeftAt` are also left alone: they track
your bot's membership lifecycle, which your persistence logic owns.

## Channel types

NetCord expresses a channel's kind through its class rather than a property, and
Persistord's `ChannelType` has four members, so the translation collapses NetCord's
thirteen channel classes:

| NetCord class | `ChannelType` |
| --- | --- |
| `PublicGuildThread`, `PrivateGuildThread`, `AnnouncementGuildThread`, `ForumGuildThread` | `Thread` |
| `VoiceGuildChannel`, `StageGuildChannel` | `Voice` |
| `CategoryGuildChannel` | `Category` |
| `TextGuildChannel`, `AnnouncementGuildChannel`, `ForumGuildChannel`, `MediaForumGuildChannel`, `DirectoryGuildChannel` | `Text` |

Unrecognised channel classes fall back to `Text` rather than throwing, so a future
NetCord channel kind will not break a running bot.

## Versioning

This package references `NetCord` as `[1.0.0-beta.19, 2.0.0)` — a floor, not a pin. The
shipped assembly is compiled against `1.0.0-beta.19`; NuGet resolves a range to its
lowest satisfying version, and a consumer's newer direct reference wins.

**NetCord has no stable release.** Every published version is a prerelease, so this
adapter carries a prerelease dependency. That is invisible today because Persistord
itself ships as a prerelease. When Persistord publishes a stable `1.0.0`, packing this
project will raise **NU5104** (stable package with a prerelease dependency), which the
repo's `TreatWarningsAsErrors` turns into a pack failure. Resolve it then — either by
waiting for NetCord 1.0.0 stable or by shipping this adapter on its own prerelease
track — rather than by suppressing the warning.

## Test coverage

NetCord's model types have non-public constructors and non-virtual properties, so they
can be neither constructed nor mocked. Unit tests therefore cover the channel-type
table, channel field mapping through the `IGuildChannel` interface, reaction-emoji
formatting, and the null guards on every mapper. End-to-end mapping correctness is
verified by the NetCord sample bot. This asymmetry with the Discord.Net adapter — whose
interface surface admits full field-by-field mocking — is intentional.
````

- [ ] **Step 2: Add the package to the root README table**

In `README.md`, add a row directly after the `Persistord.Adapters.DiscordNet` row:

```markdown
| [`Persistord.Adapters.NetCord`](src/Persistord.Adapters.NetCord) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.Adapters.NetCord.svg)](https://www.nuget.org/packages/Persistord.Adapters.NetCord) | `.To*Entity()` mappers from [NetCord](https://netcord.dev) types | Core, Messages, History |
```

Then update the sentence below the table. Replace:

```markdown
The core packages are independent of any Discord client library. Install the DiscordNet adapter **only** if you use Discord.Net.
```

with:

```markdown
The core packages are independent of any Discord client library. Install an adapter **only** if you use that library — `Persistord.Adapters.DiscordNet` for Discord.Net, `Persistord.Adapters.NetCord` for NetCord.
```

- [ ] **Step 3: Add the install line and the per-package README link**

In the `## Install` block, after the Discord.Net adapter line:

```bash
dotnet add package Persistord.Adapters.NetCord      # optional: NetCord mappers
```

In the `## Documentation` bullet listing per-package READMEs, extend the list:

```markdown
  [History](src/Persistord.History), [Adapters.DiscordNet](src/Persistord.Adapters.DiscordNet),
  [Adapters.NetCord](src/Persistord.Adapters.NetCord).
```

- [ ] **Step 4: Update the docs article**

In `docs/articles/introduction.md`, replace:

```markdown
Mapping from Discord.Net / DSharpPlus / NetCord model types to Persistord entities
is the user's responsibility — though the optional `Persistord.Adapters.DiscordNet`
package provides ready-made mappers if you use Discord.Net.
```

with:

```markdown
Mapping from Discord.Net / DSharpPlus / NetCord model types to Persistord entities
is the user's responsibility — though the optional `Persistord.Adapters.DiscordNet`
and `Persistord.Adapters.NetCord` packages provide ready-made mappers for those two
libraries. A DSharpPlus adapter is not yet available.
```

Then update the package count in the same file — `Persistord is split into eight NuGet packages:` becomes `Persistord is split into nine NuGet packages:`.

- [ ] **Step 5: Verify the whole solution builds and tests green**

Run: `dtk dotnet build Persistord.slnx`
Expected: PASS.

Run: `dtk dotnet test Persistord.slnx`
Expected: PASS — the whole suite, including the 30 new tests.

- [ ] **Step 6: Verify the package packs with its readme**

Run: `dotnet pack src/Persistord.Adapters.NetCord/Persistord.Adapters.NetCord.csproj -o artifacts/packtest`
Expected: PASS, producing `artifacts/packtest/Persistord.Adapters.NetCord.1.0.0.nupkg`.

Confirm the readme and the NetCord dependency range are both in the package:

```bash
unzip -p artifacts/packtest/Persistord.Adapters.NetCord.1.0.0.nupkg Persistord.Adapters.NetCord.nuspec | grep -A6 '<dependencies>'
unzip -l artifacts/packtest/Persistord.Adapters.NetCord.1.0.0.nupkg | grep README
```

Expected: a `NetCord` dependency with `version="[1.0.0-beta.19, 2.0.0)"`, and `README.md` present. Then clean up: `rm -rf artifacts/packtest`.

- [ ] **Step 7: Commit**

```bash
git add src/Persistord.Adapters.NetCord/README.md README.md docs/articles/introduction.md
git commit -m "docs(adapters): document Persistord.Adapters.NetCord"
```

---

## Done when

- `dtk dotnet build Persistord.slnx` and `dtk dotnet test Persistord.slnx` both pass.
- `Persistord.Adapters.NetCord` packs with its readme and a `[1.0.0-beta.19, 2.0.0)` NetCord dependency.
- All seven `.To*Entity()` methods exist with the names the Discord.Net adapter uses.
- The channel-type table has a test per arm, including all three thread subclasses and both audio kinds.
- The root README, the package README and `docs/articles/introduction.md` all name the package.

## Follow-up, explicitly out of scope

- **The DSharpPlus adapter** — sibling plan ③, not started.
- **The NetCord flagship bot sample** — spec sub-project ③. This is the only thing that
  will end-to-end verify `ToGuildEntity`, `ToUserEntity`, `ToMemberEntity`,
  `ToRoleEntity` and `ToMessageEntity`, since they cannot be unit-tested. Until that
  sample exists, those five mappers are verified by compilation and code review only.
  Do not let the green test count imply otherwise.
