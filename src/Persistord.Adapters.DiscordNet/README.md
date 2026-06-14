# Persistord.Adapters.DiscordNet

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Adapters.DiscordNet.svg?label=Persistord.Adapters.DiscordNet)](https://www.nuget.org/packages/Persistord.Adapters.DiscordNet)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Adapters.DiscordNet.svg)](https://www.nuget.org/packages/Persistord.Adapters.DiscordNet)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Opt-in adapter that maps [Discord.Net](https://github.com/discord-net/Discord.Net)
interface types to [Persistord](https://github.com/HandyS11/Persistord) entities.

Install only if you use Discord.Net — the core Persistord packages never reference a
Discord client library.

```bash
dotnet add package Persistord.Adapters.DiscordNet
```

## Usage

The adapter adds `.To*Entity()` extension methods on Discord.Net interfaces:

```csharp
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;

await using var db = await factory.CreateDbContextAsync();

db.Messages.Add(socketMessage.ToMessageEntity());        // embeds, attachments, reactions included
db.MessageHistory.Add(socketMessage.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

All mappers:

| Method | Source | Target |
| --- | --- | --- |
| `ToGuildEntity()` | `IGuild` | `GuildEntity` |
| `ToChannelEntity()` | `IGuildChannel` | `ChannelEntity` |
| `ToUserEntity()` | `IUser` | `UserEntity` |
| `ToMemberEntity()` | `IGuildUser` | `MemberEntity` |
| `ToRoleEntity()` | `IRole` | `RoleEntity` |
| `ToMessageEntity()` | `IMessage` | `MessageEntity` |
| `ToHistoryEntity(changeType)` | `IMessage` | `MessageHistoryEntity` |

Mappers copy data fields only. Persistence-managed fields (`MessageEntity.IsDeleted`,
`DeletedAt`) and EF-generated surrogate keys are left at their defaults; child foreign
keys are filled by EF from the navigation collections on `SaveChanges`. Mappers tolerate
partial gateway data (null optional fields) and throw only on a null source argument.

Because they bind to Discord.Net **interfaces**, the mappers work for both gateway
(`Socket*`) and REST (`Rest*`) entities.

## Versioning

This package declares a Discord.Net version range of `[3.20.0, 4.0.0)`, so you may
upgrade Discord.Net freely within its current major version while a breaking `4.0`
is held back. A new adapter release follows each Discord.Net breaking major.
