# Troubleshooting

## Stored IDs look negative

**Cause:** Discord snowflakes are 64-bit `ulong` values. Many relational providers
lack native unsigned 64-bit support and store integers as signed `long`. Snowflakes
with the high bit set appear negative when cast to `long`.

**Fix:** This is expected. Persistord uses a bit-faithful `unchecked((long)value)`
cast so the 64 bits are reinterpreted without range checking — the value round-trips
exactly. No data is lost. See [Snowflake Conversion](snowflake-conversion.md).

## Soft-deleted messages are missing from queries

**Cause:** `ApplyMessagesModule()` installs a global EF Core query filter that hides
rows where `IsDeleted = true`. This filter is on by default.

**Fix:** Use `IgnoreQueryFilters()` for a single query, or disable the filter globally
at startup with `ApplyMessagesModule(filterDeleted: false)`. See
[Soft-delete & Query Filters](soft-delete-and-query-filters.md).

```csharp
// Per-query
var all = db.Messages.IgnoreQueryFilters().ToList();

// Global — disable at startup
modelBuilder.ApplyMessagesModule(filterDeleted: false);
```

## `dotnet ef` can't find the DbContext

**Cause:** `dotnet ef` looks for a `DbContext` in the startup project. Persistord
ships no migrations and no executable startup project.

**Fix:** Pass `--project` pointing at your bot project, which owns the derived
context:

```bash
dotnet ef migrations add Initial --project YourBot.csproj
dotnet ef database update --project YourBot.csproj
```

See [Migrations](migrations.md).

## Owned embed collections create extra tables

**Cause:** EF Core synthesizes surrogate shadow keys for owned collection types stored
relationally. `Embed`, `EmbedField`, `AttachmentEntity`, and `ReactionEntity` each
get their own table with a generated key. This is expected behaviour.

**Fix:** If you prefer document storage over relational child tables, use `e.ToJson()`
on a provider with strong JSON support (PostgreSQL, SQL Server):

```csharp
modelBuilder.Entity<MessageEntity>().OwnsMany(m => m.Embeds, e =>
{
    e.ToJson();   // power-user opt-in — not the default
});
```

`ToJson()` is not the default because owned-collection JSON support varies by
provider. See [Messages](messages.md).

## My unique index accepts duplicates

**Cause:** A nullable column is part of the natural key the index covers. SQL
unique indexes treat `NULL` as distinct from every other `NULL`, so two rows
that both have `NULL` in that column don't count as a conflict — the index
never rejects the second insert.

**Fix:** Use a non-nullable sentinel value instead of `null` for "no value here".
`ManagedResource.Scope` is a non-nullable `string`, `""` for "global", for
exactly this reason. See [Providers](providers.md#unique-indexes-treat-null-as-distinct)
and [Scope](managed-resources.md#scope).

## `ORDER BY` on a `DateTimeOffset` fails on SQLite

**Cause:** EF Core's SQLite provider cannot translate an `OrderBy`/`OrderByDescending`
over a `DateTimeOffset` column — it throws `System.NotSupportedException: SQLite
does not support expressions of type 'DateTimeOffset' in ORDER BY clauses.`

**Fix:** Order by a surrogate key instead. Every `ManagedResource` carries a
`long Id` for this reason:

```csharp
var recent = await db.Set<ManagedMessage>().OrderByDescending(m => m.Id).ToListAsync();
```

See [Providers](providers.md#order-by-on-a-datetimeoffset-column-fails).

## `CryptographicException` when reading a token

**Cause:** `Persistord.Protection` decrypts a `[Protected]` column while EF
materializes the row. A read only fails this way when the specific Data
Protection key that encrypted the value is gone from the key ring — deleted, or
explicitly revoked.

**Fix:** Restoring a deleted key from a backup of the key ring folder fixes a
read broken by deletion. A revoked key has no supported recovery through
`Persistord.Protection` — treat revocation as permanent, and back up the key
ring on the same schedule as the database itself. See
[Protection](protection.md#what-a-failure-looks-like).

## History foreign-key violation when deleting a message

**Cause:** `MessageHistoryEntity` holds a real foreign key to `MessageEntity`
configured with `DeleteBehavior.Restrict`. Hard-deleting the parent `MessageEntity`
row violates this constraint and is blocked by the database.

**Fix:** Never hard-delete messages. Set `IsDeleted = true` and `DeletedAt` instead
(soft-delete). The row survives physically, the FK stays valid, and the history —
including the row logging the deletion — remains intact. See
[Soft-delete & Query Filters](soft-delete-and-query-filters.md).
