---
description: Copy-paste patterns for common Persistord operations, from persisting a guild to mapping a message from your Discord library.
---

# Recipes

Each recipe is a copy-pasteable pattern for one common Persistord operation.

Each snippet assumes you already have a short-lived `DbContext` obtained from
`IDbContextFactory` — see [DbContext Lifetime](dbcontext-lifetime.md).

## Persist a guild

Add a `GuildEntity` and save. `Id` is the only required field — `Name` and
`OwnerId` are optional, for a bot that owns resources rather than mirroring
Discord.

```csharp
db.Guilds.Add(new GuildEntity { Id = guildId, Name = name, OwnerId = ownerId });
await db.SaveChangesAsync();
```

## Remember a channel the bot created

`Persistord.Managed`'s `UpsertManagedAsync` finds-or-creates the record in one
call, keyed by `(guildId, scope, key)`. See [Upsert](upsert.md) for the
`UpsertAsync` it wraps, and [Managed Resources](managed-resources.md) for the
reconcile loop this fits into.

```csharp
var record = await db.UpsertManagedAsync<ManagedChannel>(
    guildId, scope: null, key: "announcements", discordId: channel.Id);
```

## Purge a guild

Deletes every `IGuildScoped` row for the guild, and the guild row itself, in
one transaction. See [Guild Lifecycle](guild-lifecycle.md) for the per-guild
lock this needs and why Discord resources must be torn down first.

```csharp
await db.PurgeGuildAsync(guildId);
```

## Log a message create, edit, and delete

Add the `MessageEntity` row once, then append a `MessageHistoryEntity` for each
change. Each history row is a full content snapshot tagged with a `HistoryChangeType`.

```csharp
// On message create: add the message and log a Created snapshot.
db.Messages.Add(new MessageEntity
{
    Id = messageId,
    ChannelId = channelId,
    AuthorId = authorId,
    Content = content,
});
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId,
    Content = content,
    RecordedAt = DateTimeOffset.UtcNow,
    ChangeType = HistoryChangeType.Created,
});
await db.SaveChangesAsync();

// On message edit: update the message and append an Edited snapshot.
// message.Content = newContent; message.EditedAt = DateTimeOffset.UtcNow;
// db.MessageHistory.Add(new MessageHistoryEntity { ..., ChangeType = HistoryChangeType.Edited });

// On message delete: soft-delete the message (set IsDeleted/DeletedAt) and append a Deleted snapshot.
// message.IsDeleted = true; message.DeletedAt = DateTimeOffset.UtcNow;
// db.MessageHistory.Add(new MessageHistoryEntity { ..., ChangeType = HistoryChangeType.Deleted });
```

## Read soft-deleted messages

The default query filter hides soft-deleted messages. Call `IgnoreQueryFilters()`
to bypass it for a single query.

```csharp
var all = db.Messages.IgnoreQueryFilters().ToList();
```

## Query a message's history chronologically

History rows are indexed on `(MessageId, RecordedAt)`. Order by `RecordedAt` on
providers that support `DateTimeOffset` ordering (PostgreSQL, SQL Server):

```csharp
var history = db.MessageHistory
    .Where(h => h.MessageId == id)
    .OrderBy(h => h.RecordedAt)
    .ToList();
```

SQLite cannot order by a `DateTimeOffset` column, so order by the surrogate `Id` there
(see [Providers](providers.md#order-by-on-a-datetimeoffset-column-fails)):

```csharp
var history = db.MessageHistory
    .Where(h => h.MessageId == id)
    .OrderBy(h => h.Id)
    .ToList();
```

## Swap the database provider

The model is provider-agnostic. Change only the provider call in your DI
registration; the snowflake `ulong ↔ long` conversion applies automatically
regardless of provider.

```csharp
// PostgreSQL
options.UseNpgsql(connectionString);

// SQLite
options.UseSqlite(connectionString);

// SQL Server
options.UseSqlServer(connectionString);
```

## Map from your Discord library

Install the adapter for your library and call `.To*Entity()` directly on its objects. Each
adapter's guide has the full mapper table.

# [Discord.Net](#tab/discordnet)

```csharp
using Persistord.Adapters.DiscordNet;

db.Messages.Add(message.ToMessageEntity());
await db.SaveChangesAsync();
```

See [Discord.Net Adapter](discord-net-adapter.md).

# [DSharpPlus](#tab/dsharpplus)

```csharp
using Persistord.Adapters.DSharpPlus;

db.Messages.Add(message.ToMessageEntity());
db.Members.Add(member.ToMemberEntity(guildId)); // DSharpPlus members and roles need the guild id
await db.SaveChangesAsync();
```

See [DSharpPlus Adapter](dsharpplus-adapter.md).

# [NetCord](#tab/netcord)

```csharp
using Persistord.Adapters.NetCord;

db.Messages.Add(message.ToMessageEntity());
await db.SaveChangesAsync();
```

See [NetCord Adapter](netcord-adapter.md).

---

## See also

- [Guild Lifecycle](guild-lifecycle.md) — the guild recipes above, wired to gateway events.
- [Managed Resources](managed-resources.md) — the reconcile loop around `UpsertManagedAsync`.
- [Choosing an Adapter](adapters.md) — comparing the three adapters.
