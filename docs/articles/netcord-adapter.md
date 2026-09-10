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

Each mapper binds the base type that both the gateway and REST variants derive from,
so `Gateway.Guild` and `Gateway.Message` map through the same methods as their
`Rest*` counterparts.

## What the mappers do not touch

Mappers are pure functions over data fields. They never set `IsDeleted`/`DeletedAt`,
never set EF-generated surrogate keys (`Embed.Id`, `ReactionEntity.Id`), and never set
child foreign keys — children are added to the parent's navigation collection and EF
fills the keys on save. `GuildEntity.JoinedAt`/`LeftAt` are also left alone: they
track your bot's membership lifecycle, which your persistence logic owns.

## Channel types

NetCord expresses a channel's kind through its class rather than a property, and
Persistord's `ChannelType` has four members, so the translation collapses NetCord's
channel classes:

| NetCord class | `ChannelType` |
| --- | --- |
| `PublicGuildThread`, `PrivateGuildThread`, `AnnouncementGuildThread`, `ForumGuildThread` | `Thread` |
| `VoiceGuildChannel`, `StageGuildChannel` | `Voice` |
| `CategoryGuildChannel` | `Category` |
| `TextGuildChannel`, `AnnouncementGuildChannel`, `ForumGuildChannel`, `MediaForumGuildChannel`, `DirectoryGuildChannel` | `Text` |

Unrecognised channel classes fall back to `Text` rather than throwing, so a future
NetCord channel kind will not break a running bot.

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
