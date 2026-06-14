# Persistord.Messages

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Messages.svg?label=Persistord.Messages)](https://www.nuget.org/packages/Persistord.Messages)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Messages.svg)](https://www.nuget.org/packages/Persistord.Messages)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Message-persistence module for [Persistord](https://github.com/HandyS11/Persistord).
Depends on `Persistord.Core`.

Adds `MessageEntity` plus its related data and an `ApplyMessagesModule()` model
extension:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);   // core skeleton + snowflake convention
    modelBuilder.ApplyMessagesModule();   // messages, embeds, attachments, reactions
}
```

## Soft-delete

`MessageEntity` carries `IsDeleted` / `DeletedAt`. A delete is recorded, not
removed, so dependent rows (e.g. history) keep a valid foreign key.

By default a global query filter hides soft-deleted messages:

```csharp
modelBuilder.ApplyMessagesModule();             // filter ON (default)
modelBuilder.ApplyMessagesModule(filterDeleted: false);  // filter OFF
```

To read soft-deleted rows on a per-query basis while keeping the filter on, use the
standard EF Core escape hatch:

```csharp
var all = db.Messages.IgnoreQueryFilters().ToList();
```

## Storage shape

- **Embeds** are stored relationally as child rows (`Embed`, with owned
  `EmbedFooter` / `EmbedAuthor` and a relational `EmbedField` collection). Each
  embed and field has a surrogate key, which keeps the model portable across
  providers — including SQLite, where a JSON-document mapping is not the default.
  If you target a provider with strong JSON support and prefer document storage,
  `e.ToJson()` is available as a power-user opt-in, but it is **not** the default
  because owned-collection JSON support varies by provider.
- **Attachments** and **reactions** are relational children with their own keys.
  `AttachmentEntity.Id` is the caller-supplied Discord snowflake (never
  store-generated); `ReactionEntity.Id` is a surrogate.

## License

MIT
