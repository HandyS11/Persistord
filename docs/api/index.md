# API Reference

Generated from the XML documentation comments in the nine packages that ship code.
The tenth, `Persistord` itself, is a meta package: it carries dependencies and no
types of its own, so it contributes nothing here.

## Where to start

| Namespace | Holds |
| --- | --- |
| `Persistord.Core` | `DiscordDbContext`, `DiscordGraphDbContext`, upsert, purge, guild-root, and model-builder extensions |
| `Persistord.Core.Entities` | The five core entities — `GuildEntity`, `ChannelEntity`, `UserEntity`, `MemberEntity`, `RoleEntity` — and the `ChannelType` enum |
| `Persistord.Core.Conversions` | The `ulong` ↔ `long` snowflake converters |
| `Persistord.Core.Conventions` | `SnowflakeKeyConvention`, `GuildScopeConvention` |
| `Persistord.Core.Abstractions` | `ICreatedAt`, `IUpdatedAt`, `IGuildScoped`, `ProtectedAttribute` |
| `Persistord.Messages.Entities` | `MessageEntity` and its embed, attachment, and reaction children |
| `Persistord.History.Entities` | `MessageHistoryEntity`, `HistoryChangeType` |
| `Persistord.Managed.Entities` | `ManagedResource` and the categories, channels, messages, and webhooks a bot owns |
| `Persistord.Protection` | `ApplyProtection()`, plus the `ProtectedStringConvention`, `ProtectedStringConverter` and `ProtectionPurposes` it applies |
| `Persistord.Adapters.*` | The `.To*Entity()` mappers, one namespace per Discord library |
| `Persistord.Testing` | In-memory SQLite fixtures and EF Core model assertions |

New to the library? Read [Getting Started](../articles/getting-started.md) first — the
reference below assumes you know what a `DiscordDbContext` is.
