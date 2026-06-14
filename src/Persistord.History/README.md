# Persistord.History

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.History.svg?label=Persistord.History)](https://www.nuget.org/packages/Persistord.History)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.History.svg)](https://www.nuget.org/packages/Persistord.History)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Append-only message-history module for
[Persistord](https://github.com/HandyS11/Persistord). Depends on
`Persistord.Messages` (and therefore `Persistord.Core`).

Adds `MessageHistoryEntity` and an `ApplyHistoryModule()` model extension:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyMessagesModule();   // required: history has a real FK to messages
    modelBuilder.ApplyHistoryModule();
}
```

## What it records

Every change to a message is appended as a new row — a **full content snapshot**
per change, not a diff — tagged with a `HistoryChangeType` (`Created`, `Edited`,
`Deleted`) and a `RecordedAt` timestamp. Rows are indexed on
`(MessageId, RecordedAt)` for chronological lookups.

## Relationship to messages

`MessageHistoryEntity` holds a **real foreign key** to `MessageEntity` with
`DeleteBehavior.Restrict`. Because messages are soft-deleted (see
`Persistord.Messages`), the parent row is never physically removed, so history
rows — including the row that logs the deletion — always keep a valid reference.

This means **History requires the Messages table to be persisted**; it is not a
standalone audit log.

## License

MIT
