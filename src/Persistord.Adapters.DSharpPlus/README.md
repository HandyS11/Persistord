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
defaults rather than guessing. A third gap is not a field but an input that throws:

- **`UserEntity.GlobalName` stays `null`.** DSharpPlus 4.5.x has no equivalent of
  Discord's `global_name`. `DiscordMember.DisplayName` is not one — it is a
  cache-resolved nickname-or-username fallback, a different concept, and it throws on an
  uncached member. If you need global names, set the property yourself.
- **`ChannelEntity.GuildId` is `0` for DM and group-DM channels,** which carry no guild
  id. Guild channels are unaffected.
- **`ToUserEntity()` throws on an uncached `DiscordMember`.** `DiscordMember` derives
  from `DiscordUser` and overrides `Username` to resolve through the client's user
  cache, so `member.ToUserEntity()` compiles and works against a live, cached client but
  throws `NullReferenceException` for a member the client has not cached. This is a
  DSharpPlus characteristic, not something the mapper works around.

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
