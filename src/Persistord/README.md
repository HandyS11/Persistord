# Persistord

The convenience meta package for [Persistord](https://github.com/HandyS11/Persistord).

Installing `Persistord` pulls in the full library-neutral stack in one reference:

| Package | Adds |
| --- | --- |
| `Persistord.Core` | snowflake conversion, base `DiscordDbContext`, core skeleton entities |
| `Persistord.Messages` | `MessageEntity` (soft-delete), embeds, attachments, reactions |
| `Persistord.History` | append-only `MessageHistoryEntity` with a real FK to messages |

```bash
dotnet add package Persistord
```

This package selects **no** database provider and **no** Discord client library —
you stay in control of both. If you use [Discord.Net](https://github.com/discord-net/Discord.Net),
add the adapter separately:

```bash
dotnet add package Persistord.Adapters.DiscordNet
```
