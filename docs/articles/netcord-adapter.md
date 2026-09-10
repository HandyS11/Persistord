# NetCord Adapter

`Persistord.Adapters.NetCord` is an opt-in adapter that maps
[NetCord](https://netcord.dev) model types to Persistord entities. The core
Persistord packages never reference a Discord client library — install this package
only if you use NetCord.

```bash
dotnet add package Persistord.Adapters.NetCord
```

## Usage

The adapter adds `.To*Entity()` extension methods on NetCord model types. Import the
namespace and call the relevant mapper on the NetCord object:

```csharp
using Persistord.Adapters.NetCord;
using Persistord.History.Entities;

await using var db = await factory.CreateDbContextAsync();

db.Messages.Add(message.ToMessageEntity());        // embeds, attachments, reactions included
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

## Mapper table

| Method | Source | Target |
| --- | --- | --- |
| `ToGuildEntity()` | `RestGuild` | `GuildEntity` |
| `ToChannelEntity()` | `IGuildChannel` | `ChannelEntity` |
| `ToUserEntity()` | `User` | `UserEntity` |
| `ToMemberEntity()` | `GuildUser` | `MemberEntity` |
| `ToRoleEntity()` | `Role` | `RoleEntity` |
| `ToMessageEntity()` | `RestMessage` | `MessageEntity` |
| `ToHistoryEntity(changeType)` | `RestMessage` | `MessageHistoryEntity` |

NetCord splits gateway and REST variants for only two of these types: `Gateway.Guild`
derives from `RestGuild`, and `Gateway.Message` from `RestMessage`. So `ToGuildEntity()`,
`ToMessageEntity()` and `ToHistoryEntity()` take either variant through the one method —
pass a gateway object straight from an event handler.

The other four bind types that have no such split. `User`, `GuildUser` and `Role` are
single unified classes, and `ToChannelEntity()` binds the `IGuildChannel` interface that
every guild channel class implements.

## What mappers copy (and what they leave alone)

Mappers are pure functions over data fields. They never set `IsDeleted`/`DeletedAt`,
never set EF-generated surrogate keys (`Embed.Id`, `EmbedField.Id`, `ReactionEntity.Id`),
and never set child foreign keys — children are added to the parent's navigation
collection and EF fills the keys on save. `GuildEntity.JoinedAt`/`LeftAt` are also
left alone: they track your bot's membership lifecycle, which your persistence
logic owns.

## Channel types

NetCord expresses a channel's kind through its class rather than a property, and
Persistord's `ChannelType` has four members, so the translation collapses NetCord's
channel classes. The mapper switches on base type and interface, not on an
enumerated list of concrete classes, so every class that derives from a given base
lands in the same bucket:

| NetCord class | `ChannelType` |
| --- | --- |
| `PublicGuildThread`, `PrivateGuildThread`, `AnnouncementGuildThread`, `ForumGuildThread` (all derive from `GuildThread`) | `Thread` |
| `VoiceGuildChannel`, `StageGuildChannel` (both implement `IVoiceGuildChannel`) | `Voice` |
| `CategoryGuildChannel` | `Category` |
| `TextGuildChannel`, `AnnouncementGuildChannel`, `ForumGuildChannel`, `MediaForumGuildChannel`, `DirectoryGuildChannel` | `Text` |

Because the switch matches on `GuildThread`/`IVoiceGuildChannel` rather than by
name, a channel class NetCord adds later is classified by what it derives from, not
by whether the mapper explicitly knows about it: a future thread subtype maps to
`Thread` and a future voice-capable channel maps to `Voice` automatically, with no
adapter change required. Only a class matching none of the three typed arms reaches
the `Text` fallback — this never throws. NetCord's own `UnknownGuildThread`, the
placeholder for thread kinds NetCord hasn't modelled explicitly yet, already
demonstrates this: it derives from `GuildThread`, so it maps to `Thread` without
appearing anywhere in the mapper.

## Versioning

This package references `NetCord` as `[1.0.0-beta.19, 2.0.0)` — a floor, not a pin.
The shipped assembly is compiled against `1.0.0-beta.19`; NuGet resolves a range to
its lowest satisfying version, and a consumer's newer direct reference wins.

**NetCord has no stable release.** Every published version is a prerelease, so this
adapter carries a prerelease dependency. A local `dotnet pack` without a version
override fails with **NU5104** (stable package with a prerelease dependency):
`Directory.Build.props` sets a local-build placeholder `<Version>1.0.0</Version>`,
which NuGet reads as stable, and the repo's `TreatWarningsAsErrors` turns that
mismatch into a hard pack failure. To pack locally, pass a prerelease version
explicitly: `dotnet pack -p:Version=1.0.0-beta.1`. CD is unaffected — it packs with
`-p:Version=$VERSION`, and release tags are themselves prerelease (`1.0.0-beta4` and
the like), so the stable/prerelease mismatch never arises there.

## See also

- [Messages](messages.md) — `MessageEntity` shape and embed storage decisions.
- [History](history.md) — `MessageHistoryEntity` and `HistoryChangeType`.
- [Choosing an Adapter](adapters.md) — compares all three adapters side by side.
