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

This package references `NetCord` as `[1.0.0-beta.19, 2.0.0)` — a floor, not a pin. The
shipped assembly is compiled against `1.0.0-beta.19`; NuGet resolves a range to its
lowest satisfying version, and a consumer's newer direct reference wins.

**NetCord has no stable release.** Every published version is a prerelease, so this
adapter carries a prerelease dependency. A local `dotnet pack` with no version
override fails **today** with **NU5104** (stable package with a prerelease
dependency): `Directory.Build.props` sets a local-build placeholder
`<Version>1.0.0</Version>`, which NuGet reads as stable, and the repo's
`TreatWarningsAsErrors` turns that mismatch into a hard pack failure. Real releases
are unaffected — `CD.yml` packs with `-p:Version=$VERSION`, and release tags are
themselves prerelease (`1.0.0-beta4` and the like), so the stable/prerelease
mismatch never arises there. To pack locally, pass a prerelease version explicitly:
`dotnet pack -p:Version=1.0.0-beta.1`. When Persistord genuinely publishes a stable
`1.0.0`, this becomes a real release blocker to resolve then — either by waiting for
NetCord 1.0.0 stable or by shipping this adapter on its own prerelease track —
rather than by suppressing the warning.
