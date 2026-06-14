# Recipes

Copy-pasteable patterns for the most common Persistord operations. Each snippet
assumes you already have a short-lived `DbContext` obtained from
`IDbContextFactory` — see [DbContext Lifetime](dbcontext-lifetime.md).

## Persist a guild

Add a `GuildEntity` and save. `Id`, `Name`, and `OwnerId` are the three required
fields.

```csharp
db.Guilds.Add(new GuildEntity { Id = guildId, Name = name, OwnerId = ownerId });
await db.SaveChangesAsync();
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
providers that support `DateTimeOffset` ordering (PostgreSQL, SQL Server); order by
the surrogate `Id` on SQLite.

```csharp
var history = db.MessageHistory
    .Where(h => h.MessageId == id)
    .OrderBy(h => h.RecordedAt)
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

## Map from Discord.Net

Install `Persistord.Adapters.DiscordNet` and call `.ToMessageEntity()` directly on
the Discord.Net object. See [Discord.Net Adapter](discord-net-adapter.md) for the
full mapper table.

```csharp
db.Messages.Add(socketMessage.ToMessageEntity());
await db.SaveChangesAsync();
```
