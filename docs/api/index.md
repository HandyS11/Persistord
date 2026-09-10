# API Reference

Generated from the XML documentation comments in each shipped package.

## Where to start

| Namespace | Holds |
| --- | --- |
| `Persistord.Core` | `DiscordDbContext`, `DiscordGraphDbContext`, upsert, purge, guild-root, and model-builder extensions |
| `Persistord.Core.Entities` | The six core entities and `ChannelType` |
| `Persistord.Core.Conversions` | The `ulong` ↔ `long` snowflake converters |
| `Persistord.Core.Conventions` | `SnowflakeKeyConvention`, `GuildScopeConvention` |
| `Persistord.Core.Abstractions` | `ICreatedAt`, `IUpdatedAt`, `IGuildScoped`, `ProtectedAttribute` |
| `Persistord.Messages.Entities` | `MessageEntity` and its embed, attachment, and reaction children |
| `Persistord.History.Entities` | `MessageHistoryEntity`, `HistoryChangeType` |
| `Persistord.Managed.Entities` | `ManagedResource` and the categories, channels, messages, and webhooks a bot owns |
| `Persistord.Adapters.*` | The `.To*Entity()` mappers, one namespace per Discord library |
| `Persistord.Testing` | In-memory SQLite fixtures and EF Core model assertions |

New to the library? Read [Getting Started](../articles/getting-started.md) first — the
reference below assumes you know what a `DiscordDbContext` is.
