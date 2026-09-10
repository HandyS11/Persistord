# Persistord.Adapters.DSharpPlus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Persistord.Adapters.DSharpPlus`, an opt-in package of extension methods that map DSharpPlus model types to Persistord entities.

**Architecture:** A single static `DSharpPlusMappingExtensions` class exposes `.To*Entity()` extension methods. DSharpPlus exposes no mapping interfaces and no public constructors — every entity is a concrete class with an `internal` constructor and `internal` setters — so each mapper binds the concrete class (`DiscordGuild`, `DiscordChannel`, `DiscordUser`, `DiscordMember`, `DiscordRole`, `DiscordMessage`). Mappers are pure functions: they copy data fields only, never touch persistence-managed fields (`IsDeleted`, surrogate keys), and leave EF to fill foreign keys from navigation collections.

**Tech Stack:** .NET 10, DSharpPlus 4.5.3, xunit, Newtonsoft.Json (test-only, for constructing DSharpPlus models), EF Core entities from `Persistord.Core`/`.Messages`/`.History`.

**Scope note:** This is sibling plan ③ of the adapter sub-project, and the last one. `Persistord.Adapters.DiscordNet` (plan `2026-06-14-persistord-adapter-discordnet.md`) is the reference adapter that fixes the API shape; `Persistord.Adapters.NetCord` (plan `2026-09-09-persistord-adapter-netcord.md`, shipped in #52) is the second. Completing this plan closes `2026-06-14-persistord-adapter-packages-design.md` entirely.

**Spec:** `docs/superpowers/specs/2026-06-14-persistord-adapter-packages-design.md` (the DSharpPlus half of §1–§5)

---

## Global Constraints

- **Target framework:** `net10.0`. Inherited from `Directory.Build.props`; do not restate in the csproj beyond the single `<TargetFramework>` line.
- **Nullable:** `enable` (inherited). **`TreatWarningsAsErrors` is `true`** — a warning fails the build. XML doc comments are required on every public member.
- **Analyzers are strict.** `AnalysisLevel=latest-all` with NetAnalyzers, Roslynator and SonarAnalyzer, all as errors. `.editorconfig` turns off 55 specific rules (including `CA1707` so underscored test names are fine, `CA1062` so nullable annotations carry argument validation, and `IDE0005` so an unused `using` is not fatal). **Anything not on that list is a build error.** Two rules bit the spike for this plan and are called out in "Analyzer traps" below: `S3358`/`RCS1238` (nested ternaries) and `S6580` (date parsing without a format provider).
- **Central package management** is on. Versions live in `Directory.Packages.props`; `PackageReference` in a csproj carries **no** `Version` attribute.
- **DSharpPlus version range:** `[4.5.3, 5.0.0)` — floor plus major ceiling, matching the existing `Discord.Net` entry `[3.20.1, 4.0.0)`. **4.5.3 is the correct floor, not 5.0.0.** See "Spec drift" §1 — this is the single most important fact in this plan.
- **Method names are identical across all three adapters:** `ToGuildEntity`, `ToChannelEntity`, `ToUserEntity`, `ToMemberEntity`, `ToRoleEntity`, `ToMessageEntity`, `ToHistoryEntity`. The `this` parameter type differs, and **two methods take an extra `ulong guildId` parameter** (see "Spec drift" §2).
- **No changes to `Persistord.Core` / `.Messages` / `.History`.** They stay Discord-library-agnostic. This plan adds files under `src/Persistord.Adapters.DSharpPlus`, `tests/Persistord.Adapters.DSharpPlus.Tests`, and edits only `Directory.Packages.props`, `Persistord.slnx`, `README.md`, `docs/articles/introduction.md`, `.github/workflows/CD.yml`, `docs/docfx.json`, `.github/workflows/Mutation.yml`, and `.github/dependabot.yml`.
- **Mapping contract (spec §3):** map only data fields the source provides; never set `IsDeleted`/`DeletedAt`; leave DB-generated surrogate keys (`Embed.Id`, `EmbedField.Id`, `ReactionEntity.Id`) at `0`; add children to navigation collections and let EF fill their FKs; unknown channel types map to a documented default rather than throwing; the one throwing case is a null source (`ArgumentNullException`).
- **Author/licence metadata:** `Authors` = `HandyS11`, `PackageLicenseExpression` = `MIT`, packed `README.md`.

---

## Target entity fields (authoritative — read before writing any mapper)

These are the **current** shapes in the repo, after the bot-fit release (#50). They differ from the spec's §3 table, which was written 2026-06-14.

| Entity | Fields this adapter sets |
| --- | --- |
| `GuildEntity` | `Id` (`ulong`), `Name` (`string?`), `OwnerId` (`ulong?`) |
| `ChannelEntity` | `Id`, `GuildId` (`ulong`, non-nullable), `ParentId` (`ulong?`), `Type` (`ChannelType`), `Name` (`string`) |
| `UserEntity` | `Id`, `Username` (`string`) — **`GlobalName` is NOT set**, see "Spec drift" §3 |
| `MemberEntity` | `GuildId`, `UserId`, `Nickname` (`string?`), `JoinedAt` (`DateTimeOffset?`) |
| `RoleEntity` | `Id`, `GuildId`, `Name` (`string`), `Permissions` (`ulong`), `Color` (`int`) |
| `MessageEntity` | `Id`, `ChannelId`, `AuthorId`, `Content` (`string?`), `EditedAt` (`DateTimeOffset?`) + `Embeds`/`Attachments`/`Reactions` |
| `AttachmentEntity` | `Id` (snowflake), `FileName`, `Url` |
| `ReactionEntity` | `Emoji` (`string`), `Count` (`int`) — `Id` (`long`) left `0` |
| `Embed` (owned) | `Title?`, `Description?`, `Color` (`int?`), `Footer?`, `Author?`, `Fields` — `Id` (`long`) left `0` |
| `MessageHistoryEntity` | `MessageId`, `Content`, `RecordedAt`, `ChangeType` |

`ChannelType` (Persistord's) has exactly four members: `Text = 0`, `Voice = 2`, `Category = 4`, `Thread = 11`.

**`GuildEntity.JoinedAt` and `GuildEntity.LeftAt` are deliberately NOT mapped.** They record when *the bot* joined and left the guild — bot-lifecycle state owned by the consumer's persistence logic, not data carried on a Discord guild object. Setting them from a mapper would silently overwrite the consumer's records. This is the same reasoning that keeps `IsDeleted`/`DeletedAt` unmapped, and it matches both sibling adapters.

---

## DSharpPlus type facts (verified by reflection + a running spike against DSharpPlus 4.5.3, 2026-09-10)

Do not re-derive these, and do not trust the DSharpPlus docs site over them — the published 4.5.x API differs from the in-progress rewrite documented there. Every line below was checked by reflecting over `DSharpPlus.dll` 4.5.3 and by deserializing real payloads.

**Everything is a concrete class. There are no mapping interfaces.** Every entity type derives from `SnowflakeObject` (except `DiscordReaction`, `DiscordEmbed*`, which derive from `object`) and implements nothing but `IEquatable<T>`. There is no `IGuild`/`IChannel`/`IMessage` to bind to, so each mapper binds the concrete class. Unlike NetCord there is no gateway/REST split to straddle: one class serves both.

| DSharpPlus type | Namespace | Members this adapter reads |
| --- | --- | --- |
| `DiscordGuild` | `DSharpPlus.Entities` | `Id` (`ulong`), `Name` (`string`), `OwnerId` (`ulong`) |
| `DiscordChannel` | `DSharpPlus.Entities` | `Id` (`ulong`), `GuildId` (**`ulong?`**), `ParentId` (`ulong?`), `Type` (`ChannelType`), `Name` (`string`) |
| `DiscordThreadChannel` | `DSharpPlus.Entities` | derives from `DiscordChannel`; adds nothing this adapter needs — threads are identified by `Type`, not by class |
| `DiscordUser` | `DSharpPlus.Entities` | `Id`, `Username` (`string`). **No `GlobalName` member exists in 4.5.3** |
| `DiscordMember` | `DSharpPlus.Entities` | `DiscordMember : DiscordUser`. `Id`, `Nickname` (`string?`), `JoinedAt` (`DateTimeOffset`). **No public `GuildId`** |
| `DiscordRole` | `DSharpPlus.Entities` | `Id`, `Name` (`string`), `Permissions` (`Permissions` enum), `Color` (`DiscordColor`). **No public `GuildId`** |
| `DiscordMessage` | `DSharpPlus.Entities` | `Id`, `ChannelId`, `Author` (`DiscordUser`), `Content` (`string`), `EditedTimestamp` (`DateTimeOffset?`), `Embeds`/`Attachments`/`Reactions` (`IReadOnlyList<T>`, get-only) |
| `DiscordAttachment` | `DSharpPlus.Entities` | `Id` (`ulong`), `FileName` (`string`), `Url` (`string`) |
| `DiscordReaction` | `DSharpPlus.Entities` | `Count` (`int`), `Emoji` (`DiscordEmoji`) |
| `DiscordEmoji` | `DSharpPlus.Entities` | `Id` (**`ulong`, `0` for unicode — not nullable**), `Name` (`string`) |
| `DiscordEmbed` | `DSharpPlus.Entities` | `Title?`, `Description?`, `Color` (**`Optional<DiscordColor>`**), `Footer` (`DiscordEmbedFooter?`), `Author` (`DiscordEmbedAuthor?`), `Fields` (`IReadOnlyList<DiscordEmbedField>?`) |
| `DiscordEmbedFooter` | `DSharpPlus.Entities` | `Text` (`string`), `IconUrl` (**`DiscordUri`**, not `string`) |
| `DiscordEmbedAuthor` | `DSharpPlus.Entities` | `Name` (`string`), `Url` (**`Uri`**, not `string`) |
| `DiscordEmbedField` | `DSharpPlus.Entities` | `Name` (`string`), `Value` (`string`), `Inline` (`bool`) |
| `ChannelType` | **`DSharpPlus`** (not `.Entities`) | enum, 14 members — see the translation table in Task 2 |
| `Permissions` | **`DSharpPlus`** (not `.Entities`) | `[Flags] enum : long` — **underlying type is `Int64`, not `UInt64`** |
| `DiscordColor` | `DSharpPlus.Entities` | struct; `Value` is `int` (also `R`/`G`/`B` bytes) |
| `DiscordUri` | `DSharpPlus.Entities` | `ToString()` returns the raw URL string |

**Conversions these shapes force, each verified to compile warning-free:**

- `ChannelEntity.GuildId` is `ulong` but `DiscordChannel.GuildId` is `ulong?` (DMs and group DMs have none) → `channel.GuildId ?? 0UL`.
- `RoleEntity.Permissions` is `ulong` but the source enum is `Int64`-backed → `unchecked((ulong)role.Permissions)`. The `unchecked` is required because the permission bitfield's high bit is in range for `long` but the conversion is not provably non-negative.
- `RoleEntity.Color` ← `role.Color.Value` (`DiscordColor.Value` is already `int`; no cast).
- `Embed.Color` ← `embed.Color.HasValue ? embed.Color.Value.Value : null`. The doubled `.Value` is not a typo: the outer unwraps `Optional<DiscordColor>`, the inner reads `DiscordColor.Value`.
- `EmbedFooter.IconUrl` ← `footer.IconUrl?.ToString()` (`DiscordUri` → `string`).
- `EmbedAuthor.Url` ← `author.Url?.ToString()` (`Uri` → `string`).
- `MessageEntity.AuthorId` ← `message.Author?.Id ?? 0UL`. `DiscordMessage` has no `AuthorId`, and `Author` is null on a payload that omits it.

**Nullability gotchas found by running the spike, not by reading signatures.** DSharpPlus declares `Name`, `Username`, `Content`, `FileName`, `Url` as non-nullable `string`, but the library is `netstandard2.0` and compiled **without** nullable reference types, so those annotations are absent rather than guaranteed — the compiler treats them as oblivious and they really are null on partial payloads. Verified: a message with no `content` key yields `Content == null`. **Keep the `?? string.Empty` coalesces** on every non-nullable Persistord target. This is the opposite of the NetCord adapter's rule, where the source genuinely is non-nullable and the coalesce was dead code.

**Collection nullability is inconsistent, and the difference matters:**

- `DiscordMessage.Embeds` / `.Attachments` / `.Reactions` are **empty lists, never null**, on a payload that omits them (they are backed by eagerly-initialised internal `List<T>` fields).
- `DiscordEmbed.Fields` **is null** when the embed carries no `fields` key.

Write `?? []` on all four anyway. It is required for `Fields` and harmless on the other three, and a reader should not have to remember which is which.

---

## ⚠️ Namespace collision — read before writing the first `using`

The package namespace is `Persistord.Adapters.DSharpPlus` and the library's root namespace is `DSharpPlus`. Inside the package namespace, the identifier `DSharpPlus` binds to **`Persistord.Adapters.DSharpPlus` itself**, so a qualified reference like `DSharpPlus.ChannelType` inside the namespace body fails to compile. This is the same trap the NetCord adapter hit, and the same two rules defuse it:

1. `using` directives sit **outside** the namespace declaration, at the top of the file, where they resolve against the global namespace. These are fine.
2. Never write a qualified `DSharpPlus.Something` inside the namespace body. Use the unqualified name, or an alias declared at the top of the file.

This adapter collides on **one** type name only: `ChannelType` exists as both `DSharpPlus.ChannelType` and `Persistord.Core.Entities.ChannelType`. DSharpPlus prefixes its embed types (`DiscordEmbed`, `DiscordEmbedFooter`, `DiscordEmbedAuthor`, `DiscordEmbedField`), so unlike NetCord there is **no** collision with `Persistord.Messages.Owned.Embed` and friends, and no `global::` alias is needed anywhere.

The canonical header block, used verbatim in Tasks 2–4 — **verified to compile under the real namespace**, which is a thing the spike checked explicitly:

```csharp
using DSharpPlus.Entities;
using Persistord.Core.Entities;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using DSharpPlusChannelType = DSharpPlus.ChannelType;

namespace Persistord.Adapters.DSharpPlus;
```

`Persistord.Messages.Owned` is imported as needed per task; `Embed`, `EmbedFooter`, `EmbedAuthor` and `EmbedField` need no alias.

---

## Spec drift — four places this plan overrides the 2026-06-14 spec

1. **§1 "`src/Persistord.Adapters.DSharpPlus` references … `DSharpPlus`" — with no version stated. Pin the floor at `4.5.3`, and do NOT use `5.0.0`.** A `DSharpPlus 5.0.0` exists on NuGet and sorts highest by SemVer, but it is **unlisted** (`"listed": false`, `"published": "1900-01-01T00:00:00+00:00"` — NuGet's withdrawn-package marker) and targets `net7.0` only. `dotnet add package DSharpPlus` with no version resolves to **4.5.3**, the latest listed stable (published 2026-08-24). The alternative is the `5.0.0-nightly-*` line (latest `5.0.0-nightly-02603`, 2026-09-01, `net9.0`), which is the in-progress rewrite and is prerelease-only. Choosing stable 4.5.3 follows the Discord.Net precedent and, unlike NetCord, avoids the NU5104 stable-package-with-prerelease-dependency problem entirely. **Consequence the spec did not foresee:** 4.5.3 is `netstandard2.0` and drags in `Newtonsoft.Json 13.0.2` transitively. Verified acceptable — a probe project built against the repo's full analyzer set and `TreatWarningsAsErrors` produced **zero warnings**, no `NU1701` (framework mismatch) and no `NU1903` (audit).

2. **§2 "Method names are identical across all three packages … only the `this` parameter type differs." Two signatures must also take a `ulong guildId`.** `DiscordRole` and `DiscordMember` have **no publicly reachable guild id** in 4.5.3: the value lives in an `internal` field `_guild_id`, which is not even populated by deserialization. `DiscordMember.Guild` exists but resolves through the client's guild cache and throws `NullReferenceException` on any member the cache does not own — unusable from a pure mapper. `RoleEntity.GuildId` and `MemberEntity.GuildId` (half of `MemberEntity`'s composite key) are not optional fields that can be left null. So:

   ```csharp
   public static MemberEntity ToMemberEntity(this DiscordMember member, ulong guildId);
   public static RoleEntity   ToRoleEntity(this DiscordRole role, ulong guildId);
   ```

   The method *names* still match the sibling adapters, which is what §2's "consistent feel" goal was actually about. The caller always has the guild id in hand at the point of mapping (it is on the event args, the guild object, or the command context). The alternative — reflecting into `_guild_id` — would bind the adapter to a private field and silently return `0` for the deserialized case, which is worse than an explicit parameter. Document this asymmetry in the package README's API table.

3. **§3 mapping table: `UserEntity.GlobalName` cannot be mapped; leave it unset.** `DiscordUser` in 4.5.3 exposes `Username` and `Discriminator` but has **no `GlobalName` member at all** — reflection over every property in `DSharpPlus.Entities` finds only `DiscordMember.DisplayName`, which is a computed nickname-or-username fallback resolved through the client cache, not Discord's `global_name` field. Mapping `DisplayName` into `GlobalName` would put a nickname in a column that means "account-level display name" and would throw on a cache-less member. Leave `GlobalName` at its default `null` and say so in the README. This is a genuine capability gap against the Discord.Net adapter, not an oversight.

4. **§4 "these libraries' concrete model types are hard or impossible to construct in isolation, so full field-by-field unit coverage is not feasible."** Half right this time — and the half that is right is not the half the spec meant. The types genuinely cannot be constructed the normal way (every constructor and setter is `internal`, and the classes are not mockable: the properties are non-virtual, so NSubstitute cannot override them, and Castle cannot proxy a class whose only constructor is `internal`). **But there is a clean construction route, and it gives full coverage** — see the next section. The spec's "documented asymmetry" between Discord.Net and the other two adapters therefore applies to neither NetCord nor DSharpPlus.

---

## Testability — Newtonsoft deserialization is the construction route

DSharpPlus 4.5.3 serialises its entities with **Newtonsoft.Json**, and every field this adapter reads carries a `[JsonProperty]` attribute on its `internal` setter (or, for the message child collections, on the internal backing field directly: `_attachments` → `"attachments"`, `_embeds` → `"embeds"`, `_reactions` → `"reactions"`). Newtonsoft honours `[JsonProperty]` on non-public members, and `ConstructorHandling.AllowNonPublicDefaultConstructor` lets it use the `internal` parameterless constructor.

So tests construct real DSharpPlus objects from real Discord gateway JSON:

```csharp
private static readonly JsonSerializerSettings Settings =
    new() { ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };

private static T Make<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;
```

**This was verified by building it and running 19 assertions, not by reading documentation.** A single `DiscordMessage` payload round-trips fully: id, channel id, content, edited timestamp, nested author, nested attachments, nested reactions with both unicode and custom emoji, and a nested embed complete with colour, footer (`DiscordUri`), author (`Uri`) and fields. `DiscordGuild`, `DiscordChannel`, `DiscordUser`, `DiscordRole` and `DiscordMember` all populate the same way.

This approach has a second virtue worth stating: the test inputs are **Discord's own wire format**, so a test reads as "given this gateway payload, persist this row" — closer to what the adapter actually faces than a hand-built mock would be.

Three constraints the spike uncovered, all of which the test builders must respect:

1. **`DiscordMember` needs a flat `"id"`, not a nested `"user"` object.** The real gateway sends `{"user": {"id": …}}`, and DSharpPlus reassembles that through an internal `TransportMember` path that standalone deserialization does not exercise — a nested payload leaves `Id == 0`. Use `{"id": "789", "nick": …, "joined_at": …, "roles": []}`.
2. **`DiscordMember.Username` and `.Guild` throw `NullReferenceException`** on a deserialized member, because both resolve through the client cache. `ToMemberEntity` must not read either (it does not need to — `MemberEntity` has no username), and tests must not assert on them.
3. **`"roles": []` is required** in member payloads; the roles collection is lazily projected and the lazy initialiser dereferences it.

Consequences for the implementation: `MapChannelType`, `FormatEmoji` and `MapEmbed` are all `private` and exercised through the public mappers — no `InternalsVisibleTo`, no widened API surface.

`Newtonsoft.Json` becomes a **test-only** dependency, referenced explicitly by the test project (it is already available transitively via DSharpPlus, but an explicit reference is honest about the dependency and lets central package management pin it). It is **not** referenced by the adapter project. **NSubstitute is not used by this test project at all** — there is nothing mockable here — so do not add it.

---

## Analyzer traps this plan's spike hit

Both of these failed the build under `TreatWarningsAsErrors` and cost a rebuild. Write the code right the first time:

1. **`S3358` + `RCS1238` — no nested ternaries.** The obvious `FormatEmoji` one-liner (`emoji is null ? … : emoji.Id == 0 ? … : …`) is a hard error. Use a switch expression with a `null` arm and a `{ Id: 0UL }` property pattern, exactly as written in Task 4.
2. **`S6580` — `DateTimeOffset.Parse` needs a real format provider.** Passing `null` as the provider is an error; pass `CultureInfo.InvariantCulture` and `using System.Globalization;`. This bites the `JoinedAt` assertion in Task 3.

---

## File Structure

**Created:**

- `src/Persistord.Adapters.DSharpPlus/Persistord.Adapters.DSharpPlus.csproj` — packable project, refs Core+Messages+History+DSharpPlus
- `src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs` — all seven `.To*Entity()` methods plus the private helpers
- `src/Persistord.Adapters.DSharpPlus/README.md` — packed readme (API, versioning, coverage gaps)
- `tests/Persistord.Adapters.DSharpPlus.Tests/Persistord.Adapters.DSharpPlus.Tests.csproj` — xunit + Newtonsoft.Json
- `tests/Persistord.Adapters.DSharpPlus.Tests/stryker-config.json` — mutation-testing config, mirrors the NetCord one
- `tests/Persistord.Adapters.DSharpPlus.Tests/DSharpPlusFakes.cs` — JSON-payload builders for real DSharpPlus instances
- `tests/Persistord.Adapters.DSharpPlus.Tests/ChannelMappingTests.cs` — the translation table and channel fields
- `tests/Persistord.Adapters.DSharpPlus.Tests/CoreEntityMappingTests.cs` — guild, user, member, role
- `tests/Persistord.Adapters.DSharpPlus.Tests/MessageMappingTests.cs` — message, children, history
- `tests/Persistord.Adapters.DSharpPlus.Tests/NullArgumentTests.cs` — all seven guards

**Modified:**

- `Directory.Packages.props` — add the `DSharpPlus` range and the test-only `Newtonsoft.Json` pin
- `Persistord.slnx` — add the two new projects
- `README.md` — package table row, install line, adapter prose, per-package README link
- `docs/articles/introduction.md` — replace "A DSharpPlus adapter is not yet available", add the package entry, bump the package count to ten, extend the dependency-graph sentence
- `.github/workflows/CD.yml` — add the Pack step
- `docs/docfx.json` — add the csproj glob to API-reference generation
- `.github/workflows/Mutation.yml` — add the matrix entry
- `.github/dependabot.yml` — ignore `DSharpPlus` bumps (published floor, not a tested version)

One source file holds all seven mappers, matching `DiscordNetMappingExtensions.cs` and `NetCordMappingExtensions.cs`. The mappers share the alias header and the private helpers; splitting them across files would duplicate the header block and scatter one cohesive responsibility.

---

## Task 1: Project scaffolding & package wiring

**Files:**

- Modify: `Directory.Packages.props`
- Create: `src/Persistord.Adapters.DSharpPlus/Persistord.Adapters.DSharpPlus.csproj`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/Persistord.Adapters.DSharpPlus.Tests.csproj`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/stryker-config.json`
- Modify: `Persistord.slnx`

**Interfaces:**

- Consumes: nothing.
- Produces: two buildable projects. The test project references the adapter project and `Newtonsoft.Json`.

- [ ] **Step 1: Add the package versions**

In `Directory.Packages.props`, in the first `ItemGroup` (the shipped-floors one), add the `DSharpPlus` entry immediately after `Discord.Net` with its explanatory comment:

```xml
    <PackageVersion Include="Discord.Net" Version="[3.20.1, 4.0.0)" />
    <!-- DSharpPlus 4.5.3 is the latest LISTED stable. A 5.0.0 exists on NuGet and sorts
         higher, but it is unlisted (published 1900-01-01, NuGet's withdrawn marker) and
         targets net7.0; the 5.0.0-nightly-* line is the in-progress rewrite and is
         prerelease-only. 4.5.3 is netstandard2.0 and brings Newtonsoft.Json 13.0.2 in
         transitively — verified warning-free under this repo's analyzer set. -->
    <PackageVersion Include="DSharpPlus" Version="[4.5.3, 5.0.0)" />
```

In the **second** `ItemGroup` (`<!-- Test + sample -->`), add the test-only pin after `Xunit.SkippableFact`:

```xml
    <!-- Test-only: the DSharpPlus adapter tests construct DSharpPlus entities by
         deserializing Discord gateway payloads, because every DSharpPlus constructor
         and setter is internal. Not referenced by any shipped package. -->
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
```

- [ ] **Step 2: Create the adapter project**

Create `src/Persistord.Adapters.DSharpPlus/Persistord.Adapters.DSharpPlus.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.Adapters.DSharpPlus</PackageId>
    <Description>DSharpPlus adapter for Persistord: extension methods mapping DSharpPlus model types to Persistord entities.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>discord;dsharpplus;efcore;persistence;adapter</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="DSharpPlus" />
  </ItemGroup>
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

- [ ] **Step 3: Create the test project**

Create `tests/Persistord.Adapters.DSharpPlus.Tests/Persistord.Adapters.DSharpPlus.Tests.csproj`. Note there is **no** `NSubstitute` reference — unlike the other two adapter test projects, this suite mocks nothing:

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
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Adapters.DSharpPlus/Persistord.Adapters.DSharpPlus.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create the Stryker config**

Create `tests/Persistord.Adapters.DSharpPlus.Tests/stryker-config.json`:

```json
{
  "stryker-config": {
    "reporters": [
      "html",
      "cleartext"
    ],
    "project-info": {
      "name": "github.com/HandyS11/Persistord",
      "module": "Persistord.Adapters.DSharpPlus"
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

In `Persistord.slnx`, add the source project to the `/src/` folder after the NetCord line:

```xml
    <Project Path="src/Persistord.Adapters.DSharpPlus/Persistord.Adapters.DSharpPlus.csproj" />
```

and the test project to the `/tests/` folder **before** the NetCord line (entries are alphabetical: `DiscordNet`, `DSharpPlus`, `NetCord`):

```xml
    <Project Path="tests/Persistord.Adapters.DSharpPlus.Tests/Persistord.Adapters.DSharpPlus.Tests.csproj" />
```

Put the `src` entry in alphabetical position too, i.e. after `Persistord.Adapters.DiscordNet` and before `Persistord.Adapters.NetCord`.

- [ ] **Step 6: Verify the solution restores and builds**

Run: `dotnet build Persistord.slnx`
Expected: success, **0 warnings**. A warning is a failure here — in particular there must be no `NU1701` (DSharpPlus is `netstandard2.0`, which is compatible with `net10.0`, so this should not appear) and no `NU1903` audit warning from the transitive `Newtonsoft.Json 13.0.2`. If either appears, stop and report rather than suppressing it; the spike for this plan saw neither.

Both new projects contain no code yet, so they build as empty assemblies.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props Persistord.slnx src/Persistord.Adapters.DSharpPlus tests/Persistord.Adapters.DSharpPlus.Tests
git commit -m "chore: scaffold Persistord.Adapters.DSharpPlus project and test project"
```

---

## Task 2: Channel mapper and the channel-type table

**Files:**

- Create: `src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/DSharpPlusFakes.cs`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/ChannelMappingTests.cs`

**Interfaces:**

- Consumes: the two projects from Task 1.
- Produces:
  - `public static ChannelEntity ToChannelEntity(this DiscordChannel channel)`
  - `private static ChannelType MapChannelType(DSharpPlusChannelType type)`
  - `internal static T Make<T>(string json)` in `DSharpPlusFakes` — the JSON deserialization helper every later test task uses.
  - `internal static DiscordChannel MakeChannel(int type, ulong id, ulong? guildId, string name, ulong? parentId)` in `DSharpPlusFakes`.

DSharpPlus's `ChannelType` has 14 members and Persistord's has four, so the translation collapses them:

| `DSharpPlus.ChannelType` | Raw | Persistord `ChannelType` |
| --- | --- | --- |
| `Voice`, `Stage` | 2, 13 | `Voice` |
| `Category` | 4 | `Category` |
| `NewsThread`, `PublicThread`, `PrivateThread` | 10, 11, 12 | `Thread` |
| `Text`, `News`, `Store`, `GuildForum`, `Private`, `Group`, `Directory`, `Unknown` | 0, 5, 6, 15, 1, 3, 14, 2147483647 | `Text` (fallback) |

The fallback arm is deliberate, per spec §3 rule 3: an unrecognised kind maps to `Text` rather than throwing, so a channel type Discord adds later will not break a running bot.

- [ ] **Step 1: Write the shared test builders**

Create `tests/Persistord.Adapters.DSharpPlus.Tests/DSharpPlusFakes.cs`. This file is extended in Tasks 3 and 4; create it now with the channel pieces and the `Make<T>` core.

```csharp
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace Persistord.Adapters.DSharpPlus.Tests;

/// <summary>
/// Builders for real DSharpPlus model instances.
/// <para>
/// Every DSharpPlus constructor and property setter is <c>internal</c>, and the
/// properties are non-virtual, so the entities can be neither constructed nor mocked
/// directly. They can, however, be deserialized: DSharpPlus annotates its internal
/// setters (and the internal backing fields behind its read-only collections) with
/// Newtonsoft <c>[JsonProperty]</c>, so Newtonsoft populates them when allowed to use
/// the internal parameterless constructor. The inputs below are therefore real Discord
/// gateway payloads.
/// </para>
/// </summary>
internal static class DSharpPlusFakes
{
    private static readonly JsonSerializerSettings Settings =
        new() { ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };

    internal static T Make<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;

    internal static DiscordChannel MakeChannel(
        int type = 0,
        ulong id = 111UL,
        ulong? guildId = 222UL,
        string name = "general",
        ulong? parentId = null)
    {
        var guildPart = guildId is { } gid ? $"\"guild_id\":\"{gid}\"," : string.Empty;
        var parentPart = parentId is { } pid ? $",\"parent_id\":\"{pid}\"" : string.Empty;

        return Make<DiscordChannel>(
            $"{{\"id\":\"{id}\",{guildPart}\"name\":\"{name}\",\"type\":{type}{parentPart}}}");
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Persistord.Adapters.DSharpPlus.Tests/ChannelMappingTests.cs`:

```csharp
using DSharpPlus.Entities;
using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;
using ChannelType = Persistord.Core.Entities.ChannelType;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class ChannelMappingTests
{
    [Theory]
    [InlineData(0, ChannelType.Text)]          // Text
    [InlineData(1, ChannelType.Text)]          // Private (DM) — fallback
    [InlineData(2, ChannelType.Voice)]         // Voice
    [InlineData(3, ChannelType.Text)]          // Group (group DM) — fallback
    [InlineData(4, ChannelType.Category)]      // Category
    [InlineData(5, ChannelType.Text)]          // News (announcement)
    [InlineData(6, ChannelType.Text)]          // Store
    [InlineData(10, ChannelType.Thread)]       // NewsThread
    [InlineData(11, ChannelType.Thread)]       // PublicThread
    [InlineData(12, ChannelType.Thread)]       // PrivateThread
    [InlineData(13, ChannelType.Voice)]        // Stage
    [InlineData(14, ChannelType.Text)]         // Directory — fallback
    [InlineData(15, ChannelType.Text)]         // GuildForum — fallback
    [InlineData(2147483647, ChannelType.Text)] // Unknown — fallback
    public void ToChannelEntity_translates_every_channel_type(int raw, ChannelType expected)
    {
        var entity = MakeChannel(type: raw).ToChannelEntity();

        Assert.Equal(expected, entity.Type);
    }

    [Fact]
    public void ToChannelEntity_maps_all_fields()
    {
        var entity = MakeChannel(id: 111UL, guildId: 222UL, name: "general", parentId: 444UL).ToChannelEntity();

        Assert.Equal(111UL, entity.Id);
        Assert.Equal(222UL, entity.GuildId);
        Assert.Equal(444UL, entity.ParentId);
        Assert.Equal("general", entity.Name);
    }

    [Fact]
    public void ToChannelEntity_leaves_parent_null_when_the_channel_has_no_parent()
    {
        Assert.Null(MakeChannel(type: 4, parentId: null).ToChannelEntity().ParentId);
    }

    [Fact]
    public void ToChannelEntity_maps_a_guildless_channel_to_guild_zero()
    {
        // DM and group-DM channels carry no guild_id. ChannelEntity.GuildId is a
        // non-nullable ulong, so the mapper collapses the absent id to 0 rather than
        // throwing — per spec §3 rule 5, missing optional data must not throw.
        Assert.Equal(0UL, MakeChannel(type: 1, guildId: null).ToChannelEntity().GuildId);
    }

    [Fact]
    public void ToChannelEntity_maps_a_nameless_channel_to_an_empty_name()
    {
        var channel = Make<DiscordChannel>("""{"id":"111","guild_id":"222","type":0}""");

        Assert.Equal(string.Empty, channel.ToChannelEntity().Name);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.DSharpPlus.Tests`
Expected: build failure — `ToChannelEntity` does not exist (CS1061 / CS0117). This is the expected red state.

- [ ] **Step 4: Create the extensions file**

Create `src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs`. The using block is the canonical one from the namespace-collision section; it is completed in later tasks as more types are needed.

```csharp
using DSharpPlus.Entities;
using Persistord.Core.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using DSharpPlusChannelType = DSharpPlus.ChannelType;

namespace Persistord.Adapters.DSharpPlus;

/// <summary>
/// Extension methods mapping DSharpPlus model types to Persistord entities.
/// Mappers copy data fields only; persistence-managed fields and EF-generated keys
/// are left at their defaults.
/// </summary>
public static class DSharpPlusMappingExtensions
{
    /// <summary>Maps a DSharpPlus channel to a <see cref="ChannelEntity"/>.</summary>
    /// <remarks>
    /// Binds <c>DiscordChannel</c> rather than a thread-specific subclass: DSharpPlus
    /// carries the channel kind in the <c>Type</c> property, so <c>DiscordThreadChannel</c>
    /// maps through this same method. <c>DiscordChannel.GuildId</c> is nullable because DM
    /// and group-DM channels have none; <see cref="ChannelEntity.GuildId"/> is not, so an
    /// absent guild id becomes <c>0</c> rather than an exception.
    /// </remarks>
    /// <param name="channel">The channel to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public static ChannelEntity ToChannelEntity(this DiscordChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ChannelEntity
        {
            Id = channel.Id,
            GuildId = channel.GuildId ?? 0UL,
            ParentId = channel.ParentId,
            Type = MapChannelType(channel.Type),
            Name = channel.Name ?? string.Empty,
        };
    }

    /// <summary>
    /// Maps a DSharpPlus channel kind to a Persistord <see cref="ChannelType"/>.
    /// <para>
    /// DSharpPlus has fourteen channel kinds and Persistord has four, so the
    /// translation collapses them. Kinds with no Persistord equivalent — DMs, group
    /// DMs, directories, forums, and DSharpPlus's own <c>Unknown</c> sentinel — fall
    /// back to <see cref="ChannelType.Text"/> rather than throwing, so a channel kind
    /// Discord adds later will not break a running bot.
    /// </para>
    /// </summary>
    /// <param name="type">The DSharpPlus channel kind.</param>
    private static ChannelType MapChannelType(DSharpPlusChannelType type) => type switch
    {
        DSharpPlusChannelType.Voice or DSharpPlusChannelType.Stage => ChannelType.Voice,
        DSharpPlusChannelType.Category => ChannelType.Category,
        DSharpPlusChannelType.NewsThread
            or DSharpPlusChannelType.PublicThread
            or DSharpPlusChannelType.PrivateThread => ChannelType.Thread,
        _ => ChannelType.Text,
    };
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DSharpPlus.Tests`
Expected: PASS — 18 tests (14 theory cases + 4 facts), 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs tests/Persistord.Adapters.DSharpPlus.Tests
git commit -m "feat: map DSharpPlus channels to ChannelEntity"
```

---

## Task 3: Guild, user, member and role mappers

**Files:**

- Modify: `src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs`
- Modify: `tests/Persistord.Adapters.DSharpPlus.Tests/DSharpPlusFakes.cs`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/CoreEntityMappingTests.cs`

**Interfaces:**

- Consumes: `DSharpPlusMappingExtensions` and `DSharpPlusFakes.Make<T>` from Task 2.
- Produces:
  - `public static GuildEntity ToGuildEntity(this DiscordGuild guild)`
  - `public static UserEntity ToUserEntity(this DiscordUser user)`
  - `public static MemberEntity ToMemberEntity(this DiscordMember member, ulong guildId)`
  - `public static RoleEntity ToRoleEntity(this DiscordRole role, ulong guildId)`
  - `MakeGuild`, `MakeUser`, `MakeMember`, `MakeRole` in `DSharpPlusFakes`.

- [ ] **Step 1: Extend the test builders**

Append these four builders to `DSharpPlusFakes`, inside the class, after `MakeChannel`:

```csharp
    internal static DiscordGuild MakeGuild(
        ulong id = 100UL,
        string name = "a-guild",
        ulong ownerId = 101UL) =>
        Make<DiscordGuild>($"{{\"id\":\"{id}\",\"name\":\"{name}\",\"owner_id\":\"{ownerId}\"}}");

    internal static DiscordUser MakeUser(
        ulong id = 789UL,
        string username = "someone") =>
        Make<DiscordUser>($"{{\"id\":\"{id}\",\"username\":\"{username}\"}}");

    // The real gateway nests the account under "user", but DSharpPlus reassembles that
    // through an internal TransportMember path that standalone deserialization does not
    // reach — a nested payload leaves Id at 0. A flat "id" populates it. "roles" must be
    // present: the roles collection is lazily projected and the initialiser dereferences it.
    internal static DiscordMember MakeMember(
        ulong userId = 789UL,
        string? nickname = "nick",
        string joinedAt = "2026-02-03T04:05:06+00:00")
    {
        var nickPart = nickname is null ? "null" : $"\"{nickname}\"";

        return Make<DiscordMember>(
            $"{{\"id\":\"{userId}\",\"nick\":{nickPart},\"joined_at\":\"{joinedAt}\",\"roles\":[]}}");
    }

    internal static DiscordRole MakeRole(
        ulong id = 333UL,
        string name = "admin",
        long permissions = 8L,
        int color = 0xFF00FF) =>
        Make<DiscordRole>(
            $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"permissions\":{permissions},\"color\":{color}}}");
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Persistord.Adapters.DSharpPlus.Tests/CoreEntityMappingTests.cs`. Note `using System.Globalization;` and the explicit `CultureInfo.InvariantCulture` — `DateTimeOffset.Parse` without a real format provider is an `S6580` build error.

```csharp
using System.Globalization;
using DSharpPlus.Entities;
using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class CoreEntityMappingTests
{
    [Fact]
    public void ToGuildEntity_maps_id_name_and_owner()
    {
        var entity = MakeGuild(id: 100UL, name: "a-guild", ownerId: 101UL).ToGuildEntity();

        Assert.Equal(100UL, entity.Id);
        Assert.Equal("a-guild", entity.Name);
        Assert.Equal(101UL, entity.OwnerId);
    }

    [Fact]
    public void ToGuildEntity_does_not_touch_the_bot_membership_lifecycle()
    {
        // JoinedAt/LeftAt record when the BOT joined and left. That is the consumer's
        // state, not data on a Discord guild, so the mapper must leave both alone.
        var entity = MakeGuild().ToGuildEntity();

        Assert.Null(entity.JoinedAt);
        Assert.Null(entity.LeftAt);
    }

    [Fact]
    public void ToUserEntity_maps_id_and_username()
    {
        var entity = MakeUser(id: 789UL, username: "someone").ToUserEntity();

        Assert.Equal(789UL, entity.Id);
        Assert.Equal("someone", entity.Username);
    }

    [Fact]
    public void ToUserEntity_leaves_global_name_null_because_DSharpPlus_does_not_expose_it()
    {
        // DiscordUser in DSharpPlus 4.5.3 has no GlobalName member at all. The nearest
        // thing, DiscordMember.DisplayName, is a cache-resolved nickname fallback — a
        // different concept, and it throws on a cache-less member. So this stays null.
        Assert.Null(MakeUser().ToUserEntity().GlobalName);
    }

    [Fact]
    public void ToUserEntity_maps_a_nameless_user_to_an_empty_username()
    {
        var user = Make<DiscordUser>("""{"id":"789"}""");

        Assert.Equal(string.Empty, user.ToUserEntity().Username);
    }

    [Fact]
    public void ToMemberEntity_maps_the_composite_key_from_the_member_and_the_argument()
    {
        var entity = MakeMember(userId: 789UL).ToMemberEntity(222UL);

        Assert.Equal(222UL, entity.GuildId);
        Assert.Equal(789UL, entity.UserId);
    }

    [Fact]
    public void ToMemberEntity_maps_nickname_and_join_date()
    {
        var entity = MakeMember(nickname: "nick", joinedAt: "2026-02-03T04:05:06+00:00").ToMemberEntity(222UL);

        Assert.Equal("nick", entity.Nickname);
        Assert.Equal(
            DateTimeOffset.Parse("2026-02-03T04:05:06+00:00", CultureInfo.InvariantCulture),
            entity.JoinedAt);
    }

    [Fact]
    public void ToMemberEntity_tolerates_a_member_with_no_nickname()
    {
        Assert.Null(MakeMember(nickname: null).ToMemberEntity(222UL).Nickname);
    }

    [Fact]
    public void ToRoleEntity_maps_the_guild_id_from_the_argument()
    {
        // DiscordRole has no publicly reachable guild id — the value lives in an internal
        // _guild_id field that deserialization does not even populate — so the caller
        // supplies it.
        Assert.Equal(222UL, MakeRole().ToRoleEntity(222UL).GuildId);
    }

    [Fact]
    public void ToRoleEntity_maps_id_name_permissions_and_color()
    {
        var entity = MakeRole(id: 333UL, name: "admin", permissions: 8L, color: 0xFF00FF).ToRoleEntity(222UL);

        Assert.Equal(333UL, entity.Id);
        Assert.Equal("admin", entity.Name);
        Assert.Equal(8UL, entity.Permissions);
        Assert.Equal(0xFF00FF, entity.Color);
    }

    [Fact]
    public void ToRoleEntity_preserves_a_permission_bitfield_with_the_high_bit_set()
    {
        // DSharpPlus's Permissions enum is Int64-backed while RoleEntity.Permissions is
        // ulong, so the conversion must be unchecked and must not lose or sign-extend bits.
        var entity = MakeRole(permissions: long.MinValue).ToRoleEntity(222UL);

        Assert.Equal(9223372036854775808UL, entity.Permissions);
    }

    [Fact]
    public void ToRoleEntity_maps_a_nameless_role_to_an_empty_name()
    {
        var role = Make<DiscordRole>("""{"id":"333","permissions":0,"color":0}""");

        Assert.Equal(string.Empty, role.ToRoleEntity(222UL).Name);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.DSharpPlus.Tests`
Expected: build failure — the four new methods do not exist.

- [ ] **Step 4: Extend the using block**

In `DSharpPlusMappingExtensions.cs`, the using block stays as it is — `DiscordGuild`, `DiscordUser`, `DiscordMember` and `DiscordRole` all live in `DSharpPlus.Entities`, already imported, and `Permissions`/`DiscordColor` are reached through property types rather than named directly. No new directives are needed for this task.

- [ ] **Step 5: Add the four mappers**

Insert these after `ToChannelEntity` and before `MapChannelType` in `DSharpPlusMappingExtensions.cs`:

```csharp
    /// <summary>Maps a DSharpPlus guild to a <see cref="GuildEntity"/>.</summary>
    /// <remarks>
    /// <c>JoinedAt</c> and <c>LeftAt</c> are intentionally not set: they track the bot's
    /// own membership lifecycle, which the consumer owns, not data carried on a Discord
    /// guild.
    /// </remarks>
    /// <param name="guild">The guild to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guild"/> is <see langword="null"/>.</exception>
    public static GuildEntity ToGuildEntity(this DiscordGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        return new GuildEntity
        {
            Id = guild.Id, Name = guild.Name, OwnerId = guild.OwnerId,
        };
    }

    /// <summary>Maps a DSharpPlus user to a <see cref="UserEntity"/>.</summary>
    /// <remarks>
    /// <see cref="UserEntity.GlobalName"/> is left <see langword="null"/>: DSharpPlus
    /// 4.5.3 exposes no equivalent of Discord's <c>global_name</c> field.
    /// <c>DiscordMember.DisplayName</c> is not one — it is a cache-resolved
    /// nickname-or-username fallback, and it throws on a member the client has not
    /// cached.
    /// </remarks>
    /// <param name="user">The user to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
    public static UserEntity ToUserEntity(this DiscordUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserEntity
        {
            Id = user.Id, Username = user.Username ?? string.Empty,
        };
    }

    /// <summary>Maps a DSharpPlus guild member to a <see cref="MemberEntity"/>.</summary>
    /// <remarks>
    /// The guild id is a parameter rather than a field read: <c>DiscordMember</c> keeps
    /// its guild id in an <c>internal</c> field, and its public <c>Guild</c> property
    /// resolves through the client's guild cache and throws for any member the cache
    /// does not hold. Since the id is half of <see cref="MemberEntity"/>'s composite key
    /// it cannot be omitted, so the caller — which always has it in hand from the event
    /// or command context — supplies it.
    /// </remarks>
    /// <param name="member">The guild member to map.</param>
    /// <param name="guildId">The snowflake id of the guild this membership belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public static MemberEntity ToMemberEntity(this DiscordMember member, ulong guildId)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberEntity
        {
            GuildId = guildId, UserId = member.Id, Nickname = member.Nickname, JoinedAt = member.JoinedAt,
        };
    }

    /// <summary>Maps a DSharpPlus role to a <see cref="RoleEntity"/>.</summary>
    /// <remarks>
    /// The guild id is a parameter for the same reason as on
    /// <see cref="ToMemberEntity"/>: <c>DiscordRole</c> exposes no guild id at all.
    /// <c>Permissions</c> is an <c>Int64</c>-backed <c>[Flags]</c> enum while
    /// <see cref="RoleEntity.Permissions"/> is <c>ulong</c>, so the conversion is
    /// unchecked — it reinterprets the bitfield rather than sign-extending it.
    /// </remarks>
    /// <param name="role">The role to map.</param>
    /// <param name="guildId">The snowflake id of the guild this role belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="role"/> is <see langword="null"/>.</exception>
    public static RoleEntity ToRoleEntity(this DiscordRole role, ulong guildId)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleEntity
        {
            Id = role.Id,
            GuildId = guildId,
            Name = role.Name ?? string.Empty,
            Permissions = unchecked((ulong)role.Permissions),
            Color = role.Color.Value,
        };
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DSharpPlus.Tests`
Expected: PASS — 30 tests, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs tests/Persistord.Adapters.DSharpPlus.Tests
git commit -m "feat: map DSharpPlus guilds, users, members and roles"
```

---

## Task 4: Message, reactions, embeds and history

**Files:**

- Modify: `src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs`
- Modify: `tests/Persistord.Adapters.DSharpPlus.Tests/DSharpPlusFakes.cs`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/MessageMappingTests.cs`
- Create: `tests/Persistord.Adapters.DSharpPlus.Tests/NullArgumentTests.cs`

**Interfaces:**

- Consumes: everything from Tasks 2 and 3.
- Produces:
  - `public static MessageEntity ToMessageEntity(this DiscordMessage message)`
  - `public static MessageHistoryEntity ToHistoryEntity(this DiscordMessage message, HistoryChangeType changeType)`
  - `private static string FormatEmoji(DiscordEmoji? emoji)`
  - `private static Embed MapEmbed(DiscordEmbed embed)`
  - `MakeMessage` in `DSharpPlusFakes`.

- [ ] **Step 1: Extend the test builders**

Append to `DSharpPlusFakes`, after `MakeRole`. `MakeMessage` takes raw JSON fragments for the child collections so a test can express exactly the payload shape it is probing, including omitting a key entirely:

```csharp
    /// <summary>
    /// Builds a message. <paramref name="attachments"/>, <paramref name="reactions"/> and
    /// <paramref name="embeds"/> take raw JSON array bodies so a test can omit a key
    /// entirely (pass <see langword="null"/>) and exercise the absent-collection path.
    /// </summary>
    internal static DiscordMessage MakeMessage(
        ulong id = 555UL,
        ulong channelId = 111UL,
        ulong authorId = 789UL,
        string? content = "hello",
        string? editedAt = null,
        string? attachments = null,
        string? reactions = null,
        string? embeds = null)
    {
        var parts = new List<string>
        {
            $"\"id\":\"{id}\"",
            $"\"channel_id\":\"{channelId}\"",
            $"\"author\":{{\"id\":\"{authorId}\",\"username\":\"author\"}}",
        };

        if (content is not null) { parts.Add($"\"content\":\"{content}\""); }
        if (editedAt is not null) { parts.Add($"\"edited_timestamp\":\"{editedAt}\""); }
        if (attachments is not null) { parts.Add($"\"attachments\":{attachments}"); }
        if (reactions is not null) { parts.Add($"\"reactions\":{reactions}"); }
        if (embeds is not null) { parts.Add($"\"embeds\":{embeds}"); }

        return Make<DiscordMessage>($"{{{string.Join(",", parts)}}}");
    }

    internal const string OneAttachment =
        """[{"id":"900","filename":"shot.png","url":"https://cdn.example/shot.png"}]""";

    internal const string UnicodeAndCustomReactions =
        """[{"count":3,"emoji":{"id":null,"name":"thumbsup"}},{"count":1,"emoji":{"id":"12345","name":"blob"}}]""";

    internal const string FullEmbed =
        """
        [{"title":"a title","description":"a description","color":1122867,
          "footer":{"text":"a footer","icon_url":"https://cdn.example/i.png"},
          "author":{"name":"an author","url":"https://example/a"},
          "fields":[{"name":"fname","value":"fvalue","inline":true}]}]
        """;

    internal const string BareEmbed = """[{"title":"a title"}]""";
```

- [ ] **Step 2: Write the failing tests**

Create `tests/Persistord.Adapters.DSharpPlus.Tests/MessageMappingTests.cs`:

```csharp
using System.Globalization;
using Persistord.History.Entities;
using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class MessageMappingTests
{
    [Fact]
    public void ToMessageEntity_maps_the_scalar_fields()
    {
        var entity = MakeMessage(
            id: 555UL,
            channelId: 111UL,
            authorId: 789UL,
            content: "hello",
            editedAt: "2026-01-02T03:04:05+00:00").ToMessageEntity();

        Assert.Equal(555UL, entity.Id);
        Assert.Equal(111UL, entity.ChannelId);
        Assert.Equal(789UL, entity.AuthorId);
        Assert.Equal("hello", entity.Content);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-02T03:04:05+00:00", CultureInfo.InvariantCulture),
            entity.EditedAt);
    }

    [Fact]
    public void ToMessageEntity_leaves_soft_delete_state_alone()
    {
        var entity = MakeMessage().ToMessageEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAt);
    }

    [Fact]
    public void ToMessageEntity_maps_attachments()
    {
        var entity = MakeMessage(attachments: OneAttachment).ToMessageEntity();

        var attachment = Assert.Single(entity.Attachments);
        Assert.Equal(900UL, attachment.Id);
        Assert.Equal("shot.png", attachment.FileName);
        Assert.Equal("https://cdn.example/shot.png", attachment.Url);
        Assert.Equal(0UL, attachment.MessageId); // EF fills this from the navigation on save
    }

    [Fact]
    public void ToMessageEntity_formats_unicode_and_custom_reaction_emoji_differently()
    {
        var entity = MakeMessage(reactions: UnicodeAndCustomReactions).ToMessageEntity();

        Assert.Collection(
            entity.Reactions,
            r =>
            {
                Assert.Equal("thumbsup", r.Emoji);
                Assert.Equal(3, r.Count);
            },
            r =>
            {
                Assert.Equal("blob:12345", r.Emoji);
                Assert.Equal(1, r.Count);
            });
    }

    [Fact]
    public void ToMessageEntity_leaves_reaction_surrogate_keys_for_EF()
    {
        var entity = MakeMessage(reactions: UnicodeAndCustomReactions).ToMessageEntity();

        Assert.All(entity.Reactions, r => Assert.Equal(0L, r.Id));
    }

    [Fact]
    public void ToMessageEntity_maps_a_full_embed()
    {
        var entity = MakeMessage(embeds: FullEmbed).ToMessageEntity();

        var embed = Assert.Single(entity.Embeds);
        Assert.Equal("a title", embed.Title);
        Assert.Equal("a description", embed.Description);
        Assert.Equal(1122867, embed.Color);
        Assert.Equal(0L, embed.Id); // EF-generated surrogate key
        Assert.Equal("a footer", embed.Footer!.Text);
        Assert.Equal("https://cdn.example/i.png", embed.Footer.IconUrl);
        Assert.Equal("an author", embed.Author!.Name);
        Assert.Equal("https://example/a", embed.Author.Url);

        var field = Assert.Single(embed.Fields);
        Assert.Equal("fname", field.Name);
        Assert.Equal("fvalue", field.Value);
        Assert.True(field.Inline);
        Assert.Equal(0L, field.Id);
    }

    [Fact]
    public void ToMessageEntity_tolerates_an_embed_with_no_color_footer_author_or_fields()
    {
        // DiscordEmbed.Color is Optional<DiscordColor>, and Fields is genuinely null
        // — not an empty list — when the payload omits it.
        var entity = MakeMessage(embeds: BareEmbed).ToMessageEntity();

        var embed = Assert.Single(entity.Embeds);
        Assert.Equal("a title", embed.Title);
        Assert.Null(embed.Color);
        Assert.Null(embed.Footer);
        Assert.Null(embed.Author);
        Assert.Empty(embed.Fields);
    }

    [Fact]
    public void ToMessageEntity_tolerates_a_bare_message()
    {
        // Gateway payloads are routinely partial — a message-delete event carries little
        // more than ids. Mapping must not throw, and absent optional data stays null.
        var entity = MakeMessage(content: null).ToMessageEntity();

        Assert.Null(entity.Content);
        Assert.Null(entity.EditedAt);
        Assert.Empty(entity.Attachments);
        Assert.Empty(entity.Reactions);
        Assert.Empty(entity.Embeds);
    }

    [Fact]
    public void ToHistoryEntity_snapshots_content_and_records_the_change_type()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = MakeMessage(id: 555UL, content: "hello").ToHistoryEntity(HistoryChangeType.Edited);

        Assert.Equal(555UL, entity.MessageId);
        Assert.Equal("hello", entity.Content);
        Assert.Equal(HistoryChangeType.Edited, entity.ChangeType);
        Assert.InRange(entity.RecordedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(0L, entity.Id); // EF-generated surrogate key
    }

    [Theory]
    [InlineData(HistoryChangeType.Created)]
    [InlineData(HistoryChangeType.Edited)]
    [InlineData(HistoryChangeType.Deleted)]
    public void ToHistoryEntity_passes_every_change_type_through(HistoryChangeType changeType)
    {
        Assert.Equal(changeType, MakeMessage().ToHistoryEntity(changeType).ChangeType);
    }

    [Fact]
    public void ToHistoryEntity_snapshots_a_null_content_as_null()
    {
        Assert.Null(MakeMessage(content: null).ToHistoryEntity(HistoryChangeType.Deleted).Content);
    }
}
```

Create `tests/Persistord.Adapters.DSharpPlus.Tests/NullArgumentTests.cs`:

```csharp
using DSharpPlus.Entities;
using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class NullArgumentTests
{
    [Fact]
    public void ToChannelEntity_throws_on_a_null_channel() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordChannel)null!).ToChannelEntity());

    [Fact]
    public void ToGuildEntity_throws_on_a_null_guild() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordGuild)null!).ToGuildEntity());

    [Fact]
    public void ToUserEntity_throws_on_a_null_user() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordUser)null!).ToUserEntity());

    [Fact]
    public void ToMemberEntity_throws_on_a_null_member() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordMember)null!).ToMemberEntity(222UL));

    [Fact]
    public void ToRoleEntity_throws_on_a_null_role() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordRole)null!).ToRoleEntity(222UL));

    [Fact]
    public void ToMessageEntity_throws_on_a_null_message() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordMessage)null!).ToMessageEntity());

    [Fact]
    public void ToHistoryEntity_throws_on_a_null_message() =>
        Assert.Throws<ArgumentNullException>(
            () => ((DiscordMessage)null!).ToHistoryEntity(HistoryChangeType.Created));
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.DSharpPlus.Tests`
Expected: build failure — `ToMessageEntity` and `ToHistoryEntity` do not exist.

- [ ] **Step 4: Complete the using block**

Replace the using block at the top of `DSharpPlusMappingExtensions.cs` with the full set:

```csharp
using DSharpPlus.Entities;
using Persistord.Core.Entities;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using ChannelType = Persistord.Core.Entities.ChannelType;
using DSharpPlusChannelType = DSharpPlus.ChannelType;
```

`Persistord.Messages.Owned` needs no aliases: DSharpPlus prefixes its embed types with `Discord`, so `Embed`/`EmbedFooter`/`EmbedAuthor`/`EmbedField` are unambiguous.

- [ ] **Step 5: Add the message, history and helper methods**

Insert `ToMessageEntity` and `ToHistoryEntity` after `ToRoleEntity`, and the two private helpers after `MapChannelType`:

```csharp
    /// <summary>
    /// Maps a DSharpPlus message to a <see cref="MessageEntity"/>, including embeds,
    /// attachments, and reactions. Soft-delete state and EF-generated keys are left
    /// at their defaults; child foreign keys are filled by EF from the navigation
    /// collections on save.
    /// </summary>
    /// <remarks>
    /// <c>DiscordMessage</c> has no author id of its own, so the id comes from
    /// <c>Author</c>, which is <see langword="null"/> on a payload that omits it.
    /// </remarks>
    /// <param name="message">The message to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageEntity ToMessageEntity(this DiscordMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entity = new MessageEntity
        {
            Id = message.Id,
            ChannelId = message.ChannelId,
            AuthorId = message.Author?.Id ?? 0UL,
            Content = message.Content,
            EditedAt = message.EditedTimestamp,
        };

        foreach (var attachment in message.Attachments ?? [])
        {
            entity.Attachments.Add(new AttachmentEntity
            {
                Id = attachment.Id,
                FileName = attachment.FileName ?? string.Empty,
                Url = attachment.Url ?? string.Empty,
            });
        }

        foreach (var reaction in message.Reactions ?? [])
        {
            entity.Reactions.Add(new ReactionEntity
            {
                Emoji = FormatEmoji(reaction.Emoji), Count = reaction.Count,
            });
        }

        foreach (var embed in message.Embeds ?? [])
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
    public static MessageHistoryEntity ToHistoryEntity(this DiscordMessage message, HistoryChangeType changeType)
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
```

and, after `MapChannelType`:

```csharp
    /// <summary>
    /// Formats a reaction emoji for storage: custom emoji become <c>name:id</c>
    /// (preserving the snowflake), while unicode emoji are stored as their raw name.
    /// Matches the other adapters' format so stored values are portable between them.
    /// </summary>
    /// <remarks>
    /// DSharpPlus distinguishes the two by a zero id, not by a null one —
    /// <c>DiscordEmoji.Id</c> is a non-nullable <c>ulong</c> and is <c>0</c> for a
    /// unicode emoji. Written as a switch expression rather than nested conditionals,
    /// which the repo's analyzers reject (S3358 / RCS1238).
    /// </remarks>
    /// <param name="emoji">The reaction's emoji, if the payload carried one.</param>
    private static string FormatEmoji(DiscordEmoji? emoji) => emoji switch
    {
        null => string.Empty,
        { Id: 0UL } => emoji.Name ?? string.Empty,
        _ => $"{emoji.Name}:{emoji.Id}",
    };

    /// <summary>Maps a DSharpPlus embed to a Persistord <see cref="Embed"/>.</summary>
    /// <remarks>
    /// Three shapes differ from Persistord's: <c>Color</c> is an
    /// <c>Optional&lt;DiscordColor&gt;</c> (so the doubled <c>.Value</c> below unwraps
    /// the optional, then reads the colour's integer), the footer's icon is a
    /// <c>DiscordUri</c> and the author's link a <c>Uri</c> rather than strings, and
    /// <c>Fields</c> is <see langword="null"/> — not empty — when the embed carries none.
    /// </remarks>
    /// <param name="embed">The DSharpPlus embed to map.</param>
    private static Embed MapEmbed(DiscordEmbed embed)
    {
        var mapped = new Embed
        {
            Title = embed.Title,
            Description = embed.Description,
            Color = embed.Color.HasValue ? embed.Color.Value.Value : null,
        };

        if (embed.Footer is { } footer)
        {
            mapped.Footer = new EmbedFooter
            {
                Text = footer.Text, IconUrl = footer.IconUrl?.ToString(),
            };
        }

        if (embed.Author is { } author)
        {
            mapped.Author = new EmbedAuthor
            {
                Name = author.Name, Url = author.Url?.ToString(),
            };
        }

        foreach (var field in embed.Fields ?? [])
        {
            mapped.Fields.Add(new EmbedField
            {
                Name = field.Name ?? string.Empty,
                Value = field.Value ?? string.Empty,
                Inline = field.Inline,
            });
        }

        return mapped;
    }
```

- [ ] **Step 6: Run the full suite**

Run: `dotnet test tests/Persistord.Adapters.DSharpPlus.Tests`
Expected: PASS — 50 tests, 0 warnings (30 from Tasks 2–3, 13 new message/history tests, 7 null guards).

Then run the whole solution to confirm nothing else regressed:

Run: `dotnet test Persistord.slnx`
Expected: PASS, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs tests/Persistord.Adapters.DSharpPlus.Tests
git commit -m "feat: map DSharpPlus messages, embeds, reactions and history"
```

---

## Task 5: Package README, docs wiring and pack verification

**Files:**

- Create: `src/Persistord.Adapters.DSharpPlus/README.md`
- Modify: `README.md`
- Modify: `docs/articles/introduction.md`
- Modify: `docs/docfx.json`
- Modify: `.github/workflows/CD.yml`
- Modify: `.github/workflows/Mutation.yml`
- Modify: `.github/dependabot.yml`

**Interfaces:**

- Consumes: the finished adapter from Tasks 2–4.
- Produces: a packable package with a packed README, wired into CD, mutation testing, docs generation and Dependabot's ignore list.

- [ ] **Step 1: Write the package README**

Create `src/Persistord.Adapters.DSharpPlus/README.md`:

````markdown
# Persistord.Adapters.DSharpPlus

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Adapters.DSharpPlus.svg?label=Persistord.Adapters.DSharpPlus)](https://www.nuget.org/packages/Persistord.Adapters.DSharpPlus)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Adapters.DSharpPlus.svg)](https://www.nuget.org/packages/Persistord.Adapters.DSharpPlus)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Opt-in [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) adapter for
[Persistord](https://github.com/HandyS11/Persistord): extension methods mapping
DSharpPlus model types to Persistord entities.

The core Persistord packages never reference a Discord client library. Install this
package **only** if your bot uses DSharpPlus.

## Install

```bash
dotnet add package Persistord.Adapters.DSharpPlus
```

## API

```csharp
using Persistord.Adapters.DSharpPlus;
using Persistord.History.Entities;

db.Messages.Add(message.ToMessageEntity());   // embeds, attachments, reactions included
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

| Method | Binds to | Produces |
| --- | --- | --- |
| `ToGuildEntity()` | `DiscordGuild` | `GuildEntity` |
| `ToChannelEntity()` | `DiscordChannel` | `ChannelEntity` |
| `ToUserEntity()` | `DiscordUser` | `UserEntity` |
| `ToMemberEntity(guildId)` | `DiscordMember` | `MemberEntity` |
| `ToRoleEntity(guildId)` | `DiscordRole` | `RoleEntity` |
| `ToMessageEntity()` | `DiscordMessage` | `MessageEntity` |
| `ToHistoryEntity(changeType)` | `DiscordMessage` | `MessageHistoryEntity` |

DSharpPlus models every entity as one concrete class rather than splitting gateway and
REST variants, so there is a single binding per entity. Threads map through
`ToChannelEntity()` too: DSharpPlus carries the channel kind in a property, so
`DiscordThreadChannel` needs no separate method.

### Why two methods take a guild id

`DiscordRole` and `DiscordMember` do not expose their guild id — it lives in an internal
field, and `DiscordMember.Guild` resolves through the client's guild cache and throws for
an uncached member. `RoleEntity.GuildId` and `MemberEntity.GuildId` (half of its composite
primary key) are not optional, so the caller passes the id, which it always has at the
point of mapping:

```csharp
db.Members.Add(member.ToMemberEntity(eventArgs.Guild.Id));
db.Roles.Add(role.ToRoleEntity(eventArgs.Guild.Id));
```

## What the mappers do not touch

Mappers are pure functions over data fields. They never set `IsDeleted`/`DeletedAt`,
never set EF-generated surrogate keys (`Embed.Id`, `EmbedField.Id`, `ReactionEntity.Id`),
and never set child foreign keys — children are added to the parent's navigation
collection and EF fills the keys on save. `GuildEntity.JoinedAt`/`LeftAt` are also left
alone: they track your bot's membership lifecycle, which your persistence logic owns.

## Coverage gaps

Two fields cannot be filled from DSharpPlus 4.5.x, and the mappers leave them at their
defaults rather than guessing:

- **`UserEntity.GlobalName` stays `null`.** DSharpPlus 4.5.x has no equivalent of
  Discord's `global_name`. `DiscordMember.DisplayName` is not one — it is a
  cache-resolved nickname-or-username fallback, a different concept, and it throws on an
  uncached member. If you need global names, set the property yourself.
- **`ChannelEntity.GuildId` is `0` for DM and group-DM channels,** which carry no guild
  id. Guild channels are unaffected.

## Channel types

DSharpPlus has fourteen channel kinds and Persistord's `ChannelType` has four, so the
translation collapses them:

| `DSharpPlus.ChannelType` | `ChannelType` |
| --- | --- |
| `NewsThread`, `PublicThread`, `PrivateThread` | `Thread` |
| `Voice`, `Stage` | `Voice` |
| `Category` | `Category` |
| `Text`, `News`, `Store`, `GuildForum`, `Private`, `Group`, `Directory`, `Unknown` | `Text` |

Unrecognised kinds fall back to `Text` rather than throwing, so a channel kind Discord
adds later will not break a running bot.

## Versioning

This package references `DSharpPlus` as `[4.5.3, 5.0.0)` — a floor, not a pin. The
shipped assembly is compiled against `4.5.3`; NuGet resolves a range to its lowest
satisfying version, and a consumer's newer direct reference wins.

**4.5.3 is the latest *listed* stable release.** A `DSharpPlus 5.0.0` exists on NuGet and
sorts higher, but it is unlisted — a withdrawn package — and the `5.0.0-nightly-*` line is
the in-progress v5 rewrite, published as prereleases only. This adapter therefore tracks
the 4.5.x line. DSharpPlus v5 reorganises the entity model substantially; when it reaches
a listed stable release, this adapter needs a new major of its own rather than a floor
bump.

Because 4.5.x targets `netstandard2.0`, installing this adapter also brings in
`Newtonsoft.Json` transitively. That is DSharpPlus's dependency, not Persistord's: no
other Persistord package references it.
````

- [ ] **Step 2: Update the root README**

Three edits in `README.md`:

In the **Packages** table, add a row immediately after the `Persistord.Adapters.DiscordNet` row (alphabetical: `DiscordNet`, `DSharpPlus`, `NetCord`):

```markdown
| [`Persistord.Adapters.DSharpPlus`](src/Persistord.Adapters.DSharpPlus) | [![Downloads](https://img.shields.io/nuget/dt/Persistord.Adapters.DSharpPlus.svg)](https://www.nuget.org/packages/Persistord.Adapters.DSharpPlus) | `.To*Entity()` mappers from [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) types | Core, Messages, History |
```

Replace the sentence below the table:

```markdown
The core packages are independent of any Discord client library. Install an adapter **only** if you use that library — `Persistord.Adapters.DiscordNet` for Discord.Net, `Persistord.Adapters.DSharpPlus` for DSharpPlus, `Persistord.Adapters.NetCord` for NetCord.
```

In the **Install** block, add the line after the DiscordNet one:

```bash
dotnet add package Persistord.Adapters.DSharpPlus   # optional: DSharpPlus mappers
```

In the **Documentation** section's per-package README bullet, extend the list:

```markdown
- Per-package READMEs: [Core](src/Persistord.Core), [Messages](src/Persistord.Messages),
  [History](src/Persistord.History), [Adapters.DiscordNet](src/Persistord.Adapters.DiscordNet),
  [Adapters.DSharpPlus](src/Persistord.Adapters.DSharpPlus),
  [Adapters.NetCord](src/Persistord.Adapters.NetCord).
```

- [ ] **Step 3: Update the introduction article**

Four edits in `docs/articles/introduction.md`:

Replace the non-goals paragraph (currently ending "A DSharpPlus adapter is not yet available.") with:

```markdown
Mapping from Discord.Net / DSharpPlus / NetCord model types to Persistord entities
is the user's responsibility — though the optional `Persistord.Adapters.DiscordNet`,
`Persistord.Adapters.DSharpPlus` and `Persistord.Adapters.NetCord` packages provide
ready-made mappers for all three libraries.
```

Change the package count:

```markdown
Persistord is split into ten NuGet packages:
```

Add an entry between the `Persistord.Adapters.DiscordNet` and `Persistord.Adapters.NetCord` paragraphs:

```markdown
**`Persistord.Adapters.DSharpPlus`** — an optional adapter that maps DSharpPlus model
types (`DiscordGuild`, `DiscordMessage`, etc.) to Persistord entities via `.To*Entity()`
extension methods. Install only if you use DSharpPlus; the core packages never
reference a Discord client library. `ToMemberEntity` and `ToRoleEntity` take the guild
id as an argument, because DSharpPlus does not expose it on those two types.
```

Extend the dependency-graph sentence at the end:

```markdown
The dependency graph is not linear: `Messages` depends on `Core`, `History` depends
on `Messages`, and all three of `Adapters.DiscordNet`, `Adapters.DSharpPlus` and
`Adapters.NetCord` depend on all three — that chain is the one the meta package
bundles. `Managed`, `Protection`, and `Testing` each depend on `Core` alone,
independently of that chain and of each other.
```

- [ ] **Step 4: Wire the CI and docs configuration**

In `docs/docfx.json`, add the glob after the DiscordNet entry:

```json
            "Persistord.Adapters.DSharpPlus/**.csproj",
```

In `.github/workflows/CD.yml`, add a Pack step after the `Persistord.Adapters.NetCord` one:

```yaml
      - name: Pack NuGet Package Persistord.Adapters.DSharpPlus
        run: cd ./src/Persistord.Adapters.DSharpPlus/ && dotnet pack --configuration Release -p:Version=$VERSION
```

In `.github/workflows/Mutation.yml`, add a matrix entry after the NetCord one:

```yaml
          - source: Persistord.Adapters.DSharpPlus.csproj
            testdir: tests/Persistord.Adapters.DSharpPlus.Tests
```

In `.github/dependabot.yml`, add to the `ignore:` list after the `NetCord` entry:

```yaml
      # Published floor for Persistord.Adapters.DSharpPlus, not a tested version — see
      # the shipped-floors comment in Directory.Packages.props. DSharpPlus's highest
      # SemVer version on NuGet (5.0.0) is unlisted and its v5 line is prerelease-only,
      # so an automated bump would either fail to restore or silently move the published
      # floor onto the in-progress rewrite.
      - dependency-name: "DSharpPlus"
```

- [ ] **Step 5: Verify the package packs with the README embedded**

Run:

```bash
cd src/Persistord.Adapters.DSharpPlus && dotnet pack --configuration Release
```

Expected: success, 0 warnings. Unlike the NetCord adapter there is **no NU5104 risk** — `4.5.3` is a stable release, so the local-build placeholder `<Version>1.0.0</Version>` pairs with it fine.

Then confirm the README and the dependency landed in the package:

```bash
cd src/Persistord.Adapters.DSharpPlus && unzip -l bin/Release/Persistord.Adapters.DSharpPlus.1.0.0.nupkg | grep -E "README|nuspec"
unzip -p bin/Release/Persistord.Adapters.DSharpPlus.1.0.0.nupkg Persistord.Adapters.DSharpPlus.nuspec | grep -A8 "<dependencies>"
```

Expected: `README.md` is present at the package root, and the `net10.0` dependency group lists `DSharpPlus` with version `[4.5.3, 5.0.0)` alongside the three `Persistord.*` project references.

- [ ] **Step 6: Verify the whole solution one more time**

```bash
dotnet build Persistord.slnx
dotnet test Persistord.slnx
```

Expected: build and all tests pass, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Persistord.Adapters.DSharpPlus/README.md README.md docs/articles/introduction.md docs/docfx.json .github/workflows/CD.yml .github/workflows/Mutation.yml .github/dependabot.yml
git commit -m "docs: document Persistord.Adapters.DSharpPlus and wire it into CI"
```

---

## Done when

- `dotnet build Persistord.slnx` and `dotnet test Persistord.slnx` both pass with **0 warnings**.
- All seven `.To*Entity()` methods exist on `DSharpPlusMappingExtensions`, with names matching the other two adapters.
- All fourteen `DSharpPlus.ChannelType` members have an asserted translation.
- `dotnet pack` produces a nupkg with the README embedded and `DSharpPlus [4.5.3, 5.0.0)` in its dependency group.
- The root README, the introduction article, docfx, CD, Mutation and Dependabot all name the new package.
- `docs/articles/introduction.md` no longer claims a DSharpPlus adapter is unavailable.

## Follow-up, explicitly out of scope

- **A DSharpPlus flagship sample.** `samples/` has one adapter sample (Discord.Net) and the samples spec (`2026-06-14-showcase-samples-design.md`) scoped it to that one deliberately. A DSharpPlus sample would need its own spec entry, partly because DSharpPlus entities cannot be faked the way the Discord.Net sample fakes interfaces with NSubstitute — a sample would have to deserialize payloads as these tests do, which reads oddly in a showcase.
- **Tracking DSharpPlus v5.** When the v5 rewrite reaches a listed stable release it will need a new major of this adapter, not a floor bump: the entity model, the JSON stack (v5 nightlies add ten dependencies) and the namespaces all move.
- **Filling `UserEntity.GlobalName`.** Blocked upstream; revisit only if DSharpPlus 4.5.x adds the field.
- **Adding `Persistord.Adapters.DSharpPlus` to the `Persistord` meta package.** Adapters are deliberately excluded — the meta package stays library-neutral.

---

## Self-Review

**Spec coverage (`2026-06-14-persistord-adapter-packages-design.md`, DSharpPlus half):**

- §1 packable project, `net10.0`, standard packable-csproj pattern, README packed → Task 1 Step 2, Task 5 Step 1. ✓
- §1 references Core + Messages + History + DSharpPlus → Task 1 Step 2. ✓
- §1 DSharpPlus version added to `Directory.Packages.props`, normal `PackageReference` (not `PrivateAssets`) so it flows transitively → Task 1 Steps 1–2; verified in Task 5 Step 5. ✓
- §1 versioning policy stated in the adapter README → Task 5 Step 1 "Versioning". Deviates from the spec's "no upper ceiling" to match the repo's actual floor-plus-ceiling convention; recorded in Spec drift §1. ✓
- §2 one `static` class named `<Lib>MappingExtensions` in `Persistord.Adapters.<Lib>` → Task 2 Step 4. ✓
- §2 seven methods with names identical across adapters → Tasks 2–4. Two carry an extra `guildId` parameter; recorded in Spec drift §2 and documented in the README. ✓
- §2 "exact source parameter types resolved during planning against the library's current API" → the "DSharpPlus type facts" table, established by reflection and a running spike rather than from docs. ✓
- §3 mapping table, field by field → Tasks 2–4, with the current entity shapes in "Target entity fields" superseding the 2026-06-14 table (Spec drift §3 for the one field that cannot be mapped). ✓
- §3 rule 1, never set persistence-managed fields or surrogate keys → asserted in `ToMessageEntity_leaves_soft_delete_state_alone`, `ToMessageEntity_leaves_reaction_surrogate_keys_for_EF`, and the `Id` assertions in `ToMessageEntity_maps_a_full_embed`. ✓
- §3 rule 2, children added to navigation collections with FKs left to EF → asserted in `ToMessageEntity_maps_attachments` (`Assert.Equal(0UL, attachment.MessageId)`). ✓
- §3 rule 3, per-library channel-type table with a documented default → Task 2, all 14 members asserted. ✓
- §3 rule 4, `RecordedAt` stamped at mapping time → `ToHistoryEntity_snapshots_content_and_records_the_change_type` asserts it falls in the window around the call. ✓
- §3 rule 5, partial-data tolerance, throw only on null identity → `ToMessageEntity_tolerates_a_bare_message`, the nameless/guildless channel tests, `ToMessageEntity_tolerates_an_embed_with_no_color_footer_author_or_fields`, and `NullArgumentTests`. ✓
- §4 test project following repo conventions → Task 1 Step 3. The spec's prediction of partial coverage is rebutted in the Testability section: full field-by-field coverage, verified by running it. ✓

**Placeholder scan:** No TBD/TODO. Every code step carries complete, compilable content. Every version number, type name, property name, enum member and JSON key was taken from a reflection dump or a passing assertion, not from memory. The one judgement call left to the implementer — what to do if `NU1701`/`NU1903` appears in Task 1 Step 6 — is an explicit instruction to stop and report, not a gap.

**Type consistency:** `DSharpPlusMappingExtensions` is the class name throughout. `DSharpPlusFakes` is the test helper throughout, with `Make<T>` introduced in Task 2 Step 1 and used in Tasks 2–4. Builder names (`MakeChannel`, `MakeGuild`, `MakeUser`, `MakeMember`, `MakeRole`, `MakeMessage`) and constants (`OneAttachment`, `UnicodeAndCustomReactions`, `FullEmbed`, `BareEmbed`) match between the task that defines them and every task that calls them. Parameter names match between the signatures in Tasks 2–4, the "Interfaces" blocks, and the README API table. The aliases `ChannelType` and `DSharpPlusChannelType` are used consistently and never shadowed.

**Verification provenance:** everything asserted about DSharpPlus in this plan was checked against DSharpPlus 4.5.3 on 2026-09-10 by (a) reflecting over the assembly for constructor/setter accessibility, property types and enum members, (b) deserializing real gateway payloads to confirm which fields populate and which members throw, (c) compiling the full mapper surface inside this repo against its real analyzer set and `TreatWarningsAsErrors`, and (d) running 19 assertions covering the channel table, conversions, child collections and null guards. The scaffolding was deleted afterwards; the working tree was clean when this plan was written.
