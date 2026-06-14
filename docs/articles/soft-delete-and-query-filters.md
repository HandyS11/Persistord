# Soft-delete & Query Filters

`MessageEntity` carries two soft-delete fields:

- `IsDeleted` (`bool`) — set to `true` when the message is deleted.
- `DeletedAt` (`DateTimeOffset?`) — the timestamp of the deletion.

Deleting a message sets these fields rather than removing the row from the database.
The row survives physically, which is required to keep the [History](history.md)
foreign key valid: `MessageHistoryEntity` holds a real FK to `MessageEntity`, and
hard-deleting the message row would cascade-delete its history — including the row
that logged the deletion.

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
