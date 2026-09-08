# Providers

Persistord's model is provider-agnostic — see
[Swap the database provider](recipes.md#swap-the-database-provider) — but SQLite
and PostgreSQL each have real behaviour a consumer needs to know before shipping
on them. This article collects the ones that actually bite; it is not a general
provider tutorial.

## SQLite

### `ORDER BY` on a `DateTimeOffset` column fails

SQLite has no native `DateTimeOffset` comparison support, and EF Core's SQLite
provider refuses to translate an `OrderBy` over one:

```csharp
// Throws System.NotSupportedException: SQLite does not support expressions of
// type 'DateTimeOffset' in ORDER BY clauses.
var recent = await db.Set<ManagedMessage>()
    .OrderByDescending(m => m.UpdatedAt)
    .ToListAsync();
```

This is why every `ManagedResource` carries a surrogate `long Id` — insertion
order, so orderable on every provider including SQLite:

```csharp
// Right: order by the surrogate key instead.
var recent = await db.Set<ManagedMessage>()
    .OrderByDescending(m => m.Id)
    .ToListAsync();
```

`Equals`/`==` comparisons against a `DateTimeOffset` column still work; it is
specifically ordering (and a handful of other translations) that SQLite's
provider rejects. See [Managed Resources](managed-resources.md#the-four-entities)
for why `ManagedResource.Id` exists, and prefer it — or another surrogate,
auto-incrementing key of your own — over `CreatedAt`/`UpdatedAt` for ordering on
any entity that might run on SQLite.

### `journal_mode=WAL` and `busy_timeout`

A bot creates a short-lived `DbContext` per unit of work — see
[DbContext Lifetime](dbcontext-lifetime.md) — and a busy bot can have dozens of
those in flight at once, all against the same SQLite file. SQLite's default
rollback-journal mode takes a lock that blocks every reader for the duration of a
write; under that load, "database is locked" (`SQLITE_BUSY`) is routine, not
exceptional.

Two settings fix the two halves of that problem:

- **`journal_mode=WAL`** lets readers proceed concurrently with a single writer,
  instead of every write blocking every read.
- **`busy_timeout`** (Microsoft.Data.Sqlite's `Default Timeout` connection-string
  keyword, in seconds) makes a writer that arrives while another write is
  in-flight wait briefly for the lock to clear, instead of failing immediately.

`busy_timeout` is a connection-string setting:

```csharp
var connectionString = "Data Source=bot.db;Default Timeout=5"; // busy_timeout, in seconds

services.AddDbContextFactory<MyBotContext>(options => options.UseSqlite(connectionString));
```

`journal_mode=WAL` is not a connection-string keyword — it is a property of the
database file itself, set once with a `PRAGMA` and then persistent across every
future connection to that file:

```csharp
await using var db = await factory.CreateDbContextAsync();
await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
```

Run it once, e.g. right after your migrations or `EnsureCreatedAsync` have built
the schema — not on every connection open.

### There is no `EnableRetryOnFailure` for SQLite

Unlike the Npgsql and SQL Server providers, `SqliteDbContextOptionsBuilder` (the
type `options.UseSqlite(...)`'s configure delegate hands you) has no
`EnableRetryOnFailure` method — there is no transient-failure execution strategy
to opt into. `busy_timeout` above absorbs short lock contention instead. Beyond
that:

- Keep transactions short — a bare `SaveChangesAsync()` is already one
  transaction; do not hold `BeginTransactionAsync()` open across an `await` for
  Discord I/O or anything else off-database.
- Treat the file as one writer at a time. `busy_timeout` buys you queuing, not
  real concurrent writers — do not size a SQLite deployment expecting it to
  behave like a multi-writer database under sustained concurrent writes.

### Unique indexes treat `NULL` as distinct

A SQL unique index does not consider two `NULL`s a duplicate, so a nullable
column inside a natural key does not actually constrain that key: `(GuildId,
NULL, "dash")` and a second `(GuildId, NULL, "dash")` both insert cleanly. This
is why `ManagedResource.Scope` is a non-nullable `string` — `""` for "global",
never `null` — rather than a `string?`. See
[Scope](managed-resources.md#scope) for the full wrong/right query and the
sharp edge it leaves for hand-written LINQ.

### `ClearAllTablesAsync` needs no `PRAGMA defer_foreign_keys`

`ClearAllTablesAsync` computes a dependents-before-principals delete order
directly from the model's foreign-key graph (an internal `ModelDeleteOrder`
helper) and issues plain `DELETE` statements in that order — no `PRAGMA`, no
provider-specific deferral, and the same order on every relational provider.
Before deleting, it also sets every **nullable** self-referencing foreign key to
`null` across its table — this is what lets it clear `ChannelEntity.ParentId`
without a foreign-key violation, since a delete order cannot order a table
against itself.

Two shapes fall outside what it clears:

- A **non-nullable** self-referencing foreign key is left as-is and can still
  fail the delete pass with a foreign-key violation.
- A self-reference backed by a **shadow property** (no CLR member) is skipped
  rather than attempted — no entity Persistord ships has this shape today.

## PostgreSQL

### `NULLS NOT DISTINCT` is the other fix — Persistord doesn't use it

PostgreSQL 15 added `UNIQUE ... NULLS NOT DISTINCT`, which makes a unique index
treat multiple `NULL`s as a conflict — the other way to solve the nullable
natural-key problem above. Persistord doesn't use it: doing so would tie the
model to PostgreSQL 15+ and abandon the `""` convention already needed for
SQLite, so `ManagedResource.Scope` stays a non-nullable `string` and the model
stays portable across every relational provider Persistord supports, not just
recent PostgreSQL.

### `timestamptz` normalizes `DateTimeOffset` to UTC

Npgsql stores `DateTimeOffset` values in a `timestamptz` column as a UTC instant.
The offset itself is not round-tripped: write a value with a `+02:00` offset and
read it back, and you get the same instant with `Offset == TimeSpan.Zero`, not
your original offset. Store — and compare against — UTC throughout, the way
`Persistord.Core`'s `TimestampInterceptor` already does with
`TimeProvider.GetUtcNow()`, and treat any non-UTC offset a caller hands you as
informational only, never something the database will hand back unchanged.

### The `ulong ↔ long` round trip is tested against real PostgreSQL

`tests/Persistord.Provider.Tests` runs the snowflake conversion against a real
PostgreSQL instance (via a Docker-backed fixture, skipped when Docker isn't
available) rather than only against SQLite, including the edge case that a
bit-faithful cast has to get right: `ulong.MaxValue`. See
[Snowflake Conversion](snowflake-conversion.md) for how the conversion itself
works.

## See also

- [Snowflake Conversion](snowflake-conversion.md) — the `ulong ↔ long`
  conversion both providers above go through.
- [Managed Resources](managed-resources.md) — `ManagedResource.Id` and `Scope`,
  the two shapes this article's SQLite section explains.
- [DbContext Lifetime](dbcontext-lifetime.md) — why a bot has many concurrent
  contexts against one database in the first place.
- [Recipes](recipes.md#swap-the-database-provider) — the one-line provider
  swap this article's caveats sit underneath.
