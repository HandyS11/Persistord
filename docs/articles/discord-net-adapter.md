# Discord.Net Adapter

`Persistord.Adapters.DiscordNet` is an opt-in adapter that maps
[Discord.Net](https://github.com/discord-net/Discord.Net) interface types to
Persistord entities. The core Persistord packages never reference a Discord client
library — install this package only if you use Discord.Net.

```bash
dotnet add package Persistord.Adapters.DiscordNet
```

## Usage

The adapter adds `.To*Entity()` extension methods on Discord.Net interfaces. Import
the namespace and call the relevant mapper on the Discord.Net object:

```csharp
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;

await using var db = await factory.CreateDbContextAsync();

db.Messages.Add(socketMessage.ToMessageEntity());        // embeds, attachments, reactions included
db.MessageHistory.Add(socketMessage.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

## Mapper table

| Method | Source | Target |
| --- | --- | --- |
| `ToGuildEntity()` | `IGuild` | `GuildEntity` |
| `ToChannelEntity()` | `IGuildChannel` | `ChannelEntity` |
| `ToUserEntity()` | `IUser` | `UserEntity` |
| `ToMemberEntity()` | `IGuildUser` | `MemberEntity` |
| `ToRoleEntity()` | `IRole` | `RoleEntity` |
| `ToMessageEntity()` | `IMessage` | `MessageEntity` |
| `ToHistoryEntity(changeType)` | `IMessage` | `MessageHistoryEntity` |

Because mappers bind to Discord.Net **interfaces**, they work for both gateway
(`Socket*`) and REST (`Rest*`) objects — no separate overloads needed.

## What mappers copy (and what they leave alone)

Mappers copy data fields from the Discord.Net object. They leave the following at
their defaults, letting EF Core manage them:

- Persistence-managed fields on `MessageEntity` (`IsDeleted`, `DeletedAt`) — stay
  `false` / `null` on creation.
- EF-generated surrogate keys on embed, attachment, and reaction child entities.
- Child foreign keys — filled by EF Core from the navigation collections on
  `SaveChanges`.

Mappers tolerate partial gateway data (null optional fields) and throw only on a
null source argument.

## Versioning

This package declares a Discord.Net version range of `[3.20.0, 4.0.0)`. You may
upgrade Discord.Net freely within its current major version; a breaking `4.0` is
held back. A new adapter release follows each Discord.Net breaking major.

## See also

- [Messages](messages.md) — `MessageEntity` shape and embed storage decisions.
- [History](history.md) — `MessageHistoryEntity` and `HistoryChangeType`.
