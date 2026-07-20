# Design — Persistord Adapter Packages

**Date:** 2026-06-14
**Status:** Approved for planning
**Sub-project:** ② of 3 (Root README, **Adapter packages**, Samples expansion)

## Context

Persistord is deliberately Discord-library-agnostic: the published packages
(`Persistord.Core`, `Persistord.Messages`, `Persistord.History`) never reference a
Discord client library. The PRD lists "adapters to specific Discord libraries" as an
explicit v1 Non-Goal, flagged as *possible future packages*.

This sub-project delivers those packages. The key realization that unblocks them: the
agnostic constraint only binds the **core packages**. A separate, **opt-in** adapter
package may depend on a specific Discord library without polluting the core — a user who
never installs the adapter never sees the dependency.

This design covers the adapter packages only. It is the foundation for the flagship
"one real bot per library" samples in sub-project ③, which depend on these packages.

## Goals

- Ship three opt-in adapter packages mapping each library's model types to Persistord
  entities: `Persistord.Adapters.DiscordNet`, `Persistord.Adapters.NetCord`,
  `Persistord.Adapters.DSharpPlus`.
- Provide a discoverable, idiomatic API: extension methods (`.ToMessageEntity()` etc.).
- Cover the full model: core skeleton + messages (with embeds/attachments/reactions) +
  a history helper.
- Keep the Discord-library dependency isolated to the adapter package the user
  explicitly installs.

## Non-Goals

- No changes to `Persistord.Core` / `.Messages` / `.History` — they stay agnostic.
- No reverse mapping (Persistord entity → Discord lib type). One direction only.
- No DI registration / `services.Add…()` — the mappers are pure functions.
- No gateway wiring, persistence, or `DbContext` usage in the adapters — that belongs to
  the consumer and is demonstrated in the sample bots (sub-project ③).

## 1. Package structure

Three new packable projects under `src/`, following the existing packable-csproj pattern
(`PackageId`, `Description`, `Authors`, `PackageLicenseExpression=MIT`, packed
`README.md`, `PackageTags`), `net10.0`, `IsPackable=true`, inheriting
`Directory.Build.props` (nullable, analyzers, `TreatWarningsAsErrors`, XML docs).

| Project | References |
| --- | --- |
| `src/Persistord.Adapters.DiscordNet` | Core + Messages + History + `Discord.Net` |
| `src/Persistord.Adapters.NetCord` | Core + Messages + History + `NetCord` |
| `src/Persistord.Adapters.DSharpPlus` | Core + Messages + History + `DSharpPlus` |

Each references all three Persistord packages because coverage includes the history
helper (which needs `Persistord.History`).

Discord-library package versions are added to `Directory.Packages.props` (central
package management is enabled solution-wide). Each Discord-lib reference is a **normal**
`PackageReference` (not `PrivateAssets=all`) so the dependency flows transitively to the
consumer.

### Versioning policy

- **Minimum-version floor**, set to the oldest release the adapter is built and tested
  against.
- **No upper ceiling.** Consumers float up freely within the library's major version;
  NuGet's "direct reference wins" rule means a consumer's newer direct reference is used.
- Re-release the adapter when the underlying library ships a breaking major.
- This policy is stated in each adapter's README so the version contract is explicit.

Rationale: a hard ceiling (`[3.0.0,4.0.0)`) would turn a breaking major into a
restore-time error instead of a runtime error, but these libraries iterate quickly and
do not all follow SemVer strictly, so a ceiling generates false-positive friction.
Interface-based binding (§2) already minimizes cross-version breakage risk.

## 2. Public API

One `static` class per package, named `<Lib>MappingExtensions`, in namespace
`Persistord.Adapters.<Lib>`. Method names are identical across all three packages for a
consistent feel:

```csharp
namespace Persistord.Adapters.DiscordNet;

public static class DiscordNetMappingExtensions
{
    public static GuildEntity   ToGuildEntity(this IGuild guild);
    public static ChannelEntity ToChannelEntity(this IChannel channel);
    public static UserEntity    ToUserEntity(this IUser user);
    public static MemberEntity  ToMemberEntity(this IGuildUser member);
    public static RoleEntity    ToRoleEntity(this IRole role);
    public static MessageEntity ToMessageEntity(this IMessage message);
    public static MessageHistoryEntity ToHistoryEntity(
        this IMessage message, HistoryChangeType changeType);
}
```

### Binding surface per library

- **Discord.Net** binds to **interfaces** (`IGuild`, `IChannel`, `IUser`, `IGuildUser`,
  `IRole`, `IMessage`). This works for both gateway (`Socket*`) and REST (`Rest*`)
  entities and is mockable for tests.
- **NetCord** and **DSharpPlus** bind to their **concrete model types** (e.g.
  `DiscordMessage` for DSharpPlus) because they do not expose comparable mapping
  interfaces. The `this` parameter type differs per library; the method names do not.

The exact source parameter types for NetCord and DSharpPlus are resolved during
planning/implementation against each library's current API (use the context7 MCP for
up-to-date library docs).

## 3. Mapping contract

Target entity fields (authoritative, from the current model):

| Entity | Mapped fields |
| --- | --- |
| `GuildEntity` | `Id`, `Name`, `OwnerId` |
| `ChannelEntity` | `Id`, `GuildId`, `ParentId?`, `Type` (enum), `Name` |
| `UserEntity` | `Id`, `Username`, `GlobalName?` |
| `MemberEntity` | `GuildId`, `UserId`, `Nickname?`, `JoinedAt?` |
| `RoleEntity` | `Id`, `GuildId`, `Name`, `Permissions`, `Color` |
| `MessageEntity` | `Id`, `ChannelId`, `AuthorId`, `Content?`, `EditedAt?` + child collections |
| `AttachmentEntity` | `Id` (Discord snowflake), `FileName`, `Url` |
| `ReactionEntity` | `Emoji`, `Count` (surrogate `Id` left default) |
| `Embed` (owned) | `Title?`, `Description?`, `Color?`, `Footer?`, `Author?`, `Fields` |
| `MessageHistoryEntity` | `MessageId`, `Content`, `RecordedAt`, `ChangeType` |

Rules:

1. **Map only data fields the source provides.** Never set persistence-managed fields:
   - `MessageEntity.IsDeleted` / `DeletedAt` stay default (the consumer's persistence
     logic owns soft-delete state).
   - DB-generated surrogate keys (`Embed.Id`, `ReactionEntity.Id`) stay `0`; EF assigns
     them on save.
2. **Child collections** (`Embeds`, `Attachments`, `Reactions`) are populated and added
   to the message's navigation collections. The `MessageId` foreign key is left for EF
   to fill from the relationship on `SaveChanges` — the mapper does not set it manually.
3. **`ChannelEntity.Type`** requires a per-library translation table from the library's
   channel-type representation to Persistord's `ChannelType` enum. Unknown or
   unsupported source types map to a documented default rather than throwing.
4. **`MessageHistoryEntity.RecordedAt`** is set to `DateTimeOffset.UtcNow` at mapping
   time unless the source provides an authoritative timestamp for the change.
5. **Partial-data tolerance.** Gateway entities are frequently partial (null content on
   edit events, uncached author/guild). Mappers map what is present and leave nullable
   targets `null`. They must not throw on missing *optional* data. Missing *required*
   identity (e.g. a null `Id`) is the one case that may throw `ArgumentNullException`.

## 4. Testing

- **`Persistord.Adapters.DiscordNet.Tests`** — the reference suite. Mock Discord.Net
  interfaces with **NSubstitute** (added to `Directory.Packages.props`) and assert
  field-by-field mapping for every method, including:
  - embeds / attachments / reactions populated correctly on `ToMessageEntity`,
  - the channel-type translation table,
  - partial-data tolerance (null optional fields do not throw),
  - the history helper sets `ChangeType` and a `RecordedAt`.
- **`Persistord.Adapters.NetCord.Tests` / `…DSharpPlus.Tests`** — these libraries'
  concrete model types are hard or impossible to construct in isolation, so full
  field-by-field unit coverage is not feasible. These suites cover what *is*
  constructible — primarily the channel-type translation table and any pure helper
  logic. End-to-end mapping correctness for these two libraries is verified through
  their flagship bot samples in sub-project ③.

This asymmetry is intentional and documented: Discord.Net carries the rigorous unit
suite by virtue of its interface surface; NetCord and DSharpPlus rely on
sample-driven verification plus partial unit coverage.

Test projects follow the existing test-project conventions (xunit, `Microsoft.NET.Test.Sdk`,
`xunit.runner.visualstudio`) and are added to the solution under `/tests/`.

## 5. Implementation sequencing

Within the implementation plan:

1. **Discord.Net adapter, fully** — package, interface-based mappers, channel-type
   table, NSubstitute test suite. This proves the API shape and mapping contract.
2. **NetCord adapter** — replicate the established shape against NetCord's concrete
   types; partial tests.
3. **DSharpPlus adapter** — same, against DSharpPlus's concrete types; partial tests.
4. **Solution + packaging wiring** — add all projects to `Persistord.slnx`, package
   metadata, per-package READMEs documenting the API and versioning policy.

All three adapters ship in this sub-project. Discord.Net goes first so the pattern is
proven before it is duplicated.

## Open items deferred to ③ (samples)

- Flagship bot samples (one per library) that wire a gateway event → `.ToEntity()` →
  persist. These are the primary end-to-end verification for NetCord and DSharpPlus.

## Decisions locked

- Mapping home: opt-in adapter packages (not samples-only).
- Target libraries: Discord.Net, NetCord, DSharpPlus.
- API shape: extension methods.
- Coverage: full model.
- Binding: interfaces where available (Discord.Net), concrete types otherwise.
- Versioning: minimum floor, no ceiling.
- Mock library: NSubstitute.
- Testing asymmetry: accepted and documented.
