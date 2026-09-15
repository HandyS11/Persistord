---
description: How a MessageEntity is soft-deleted, the query filter that hides deleted messages, and the two ways to read them anyway.
---

# Soft-delete & Query Filters

A `MessageEntity` is soft-deleted: your code sets two fields instead of removing its row.

- `IsDeleted` (`bool`) — set it to `true` to mark the message deleted.
- `DeletedAt` (`DateTimeOffset?`) — set it to the time of the deletion.

Persistord never sets these for you: `Remove()` on a message is still a hard delete. Keeping
the row is what keeps the [History](history.md) foreign key valid:
`MessageHistoryEntity` holds a real FK to `MessageEntity` configured with
`DeleteBehavior.Restrict`, so the database refuses to hard-delete a message that has history rows,
and a soft-delete keeps every history row — including the one that logged the deletion — pointing
at an existing message.

## Default query filter

`ApplyMessagesModule()` installs a global EF Core query filter that hides
soft-deleted messages in all queries by default:

```csharp
// filter ON — soft-deleted messages are invisible (default)
modelBuilder.ApplyMessagesModule();
```

## Disabling the filter at startup

If you want all messages — including soft-deleted ones — to be returned by every
query, disable the filter when applying the module:

```csharp
// filter OFF — soft-deleted messages appear in all queries
modelBuilder.ApplyMessagesModule(filterDeleted: false);
```

## Per-query escape hatch

To read soft-deleted rows on a single query while keeping the global filter enabled,
use the standard EF Core `IgnoreQueryFilters()` extension:

```csharp
var all = db.Messages.IgnoreQueryFilters().ToList();
```

This bypasses the filter for that query only. All other queries continue to hide
soft-deleted messages.

## See also

- [History](history.md) — the append-only history module and why soft-delete is
  required to keep its FK valid.
- [Messages](messages.md) — the full `MessageEntity` shape and embed storage.
- [Troubleshooting](troubleshooting.md#soft-deleted-messages-are-missing-from-queries) — when a message you expect is missing.
