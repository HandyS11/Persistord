# Upsert

`UpsertAsync` is `Persistord.Core`'s natural-key upsert: read by natural key,
create or mutate, save, and recover exactly once from a lost insert race. It
collapses the find-then-add-or-update-with-a-recovery-block every consumer
eventually hand-writes into one call — see [Before and after](#before-and-after)
below for the two side by side.

## Signature

```csharp
public static Task<TEntity> UpsertAsync<TEntity>(
    this DbSet<TEntity> set,
    Expression<Func<TEntity, bool>> naturalKey,
    Func<TEntity> create,
    Action<TEntity> update,
    CancellationToken cancellationToken = default)
    where TEntity : class
```

- `naturalKey` — a predicate matching at most one row.
- `create` — builds the row when none matches. The natural-key values belong
  here.
- `update` — applies the mutation. Called for a freshly created row too — see
  [Create and update run the same mutation](#create-and-update-run-the-same-mutation).

It returns the tracked row.

## The unique index is what makes race recovery work

`UpsertAsync` reads with `set.AsTracking().SingleOrDefaultAsync(naturalKey, ...)`.
When nothing matches, it builds the row, adds it, and saves inside a `try`. If
two callers race — both read "no row", both build one — exactly one insert can
win *if a unique index backs the natural key*: the loser's `SaveChangesAsync`
throws `DbUpdateException`, which is what the `catch` block recovers from by
detaching its own copy, re-reading the natural key once, and applying `update`
to the winner instead.

Without that index, both inserts succeed as ordinary duplicate rows: no
exception, no recovery path taken, and the natural key that was supposed to be
unique now matches two rows. A later
`SingleOrDefaultAsync(naturalKey)` — including the next `UpsertAsync` call
against the same key — throws `InvalidOperationException: Sequence contains
more than one matching element`. Every `ManagedResource` type ships a real
unique index over `(GuildId, Scope, Key)` for exactly this reason:

```csharp
modelBuilder.Entity<ManagedChannel>()
    .HasIndex(c => new { c.GuildId, c.Scope, c.Key })
    .IsUnique();
```

## Create and update run the same mutation

`update` runs against both a freshly created row and an existing one, so the
caller writes the mutation once instead of twice:

```csharp
var channel = await db.Set<ManagedChannel>().UpsertAsync(
    c => c.GuildId == guildId && c.Scope == scope && c.Key == "announcements",
    () => new ManagedChannel { GuildId = guildId, Scope = scope, Key = "announcements" },
    c => c.DiscordId = discordChannelId);
```

Whether `"announcements"` already had a row or not, `c.DiscordId =
discordChannelId` is the only place that assignment is written.

## `UpsertIfChangedAsync` and the dirty-check short-circuit

`UpsertAsync` is `UpsertIfChangedAsync` with the outcome discarded. The full
signature reports whether the call actually wrote anything:

```csharp
public static Task<UpsertResult<TEntity>> UpsertIfChangedAsync<TEntity>(
    this DbSet<TEntity> set,
    Expression<Func<TEntity, bool>> naturalKey,
    Func<TEntity> create,
    Action<TEntity> update,
    CancellationToken cancellationToken = default)
    where TEntity : class
```

`UpsertResult<TEntity>` is a `readonly record struct` of `(TEntity Entity, bool
Changed)`. For an *existing* row, after `update` runs, `UpsertIfChangedAsync`
checks `context.ChangeTracker.HasChanges()` — the whole change tracker, not
just the upserted row's own entry: `context.Entry(row).State` only reflects
that entry's own scalars, so a mutation that lands on an owned type, an EF
complex type, or a collection navigation would leave the principal entry
`Unchanged` and silently drop the write if the check stopped there. If change
detection finds nothing pending anywhere in the context, `SaveChangesAsync` is
never called and `Changed` comes back `false`. A freshly created row skips
this check entirely — `create` + `update` always inserts and always reports
`Changed: true`.

`Changed` therefore reports whether the call issued a write, not whether the
upserted row specifically changed: an unrelated pending change already staged
on the same context makes it `true` too, because the `SaveChangesAsync` call
below flushes those alongside the upserted row (see
[`SaveChangesAsync` flushes the whole context](#savechangesasync-flushes-the-whole-context)).
That is the honest reading, and it never loses a write.

This interacts with `UpdatedAt`: `Persistord.Core`'s `TimestampInterceptor`
only stamps `IUpdatedAt.UpdatedAt` on rows in the `Added` or `Modified` state
when `SaveChangesAsync` runs. A no-op `update` — new values equal to the old
ones — means no save, which means no `UpdatedAt` stamp either. Use the
`Changed` flag to skip work that should only happen when something really did:

```csharp
var (record, changed) = await db.Set<ManagedMessage>().UpsertIfChangedAsync(
    m => m.GuildId == guildId && m.Scope == scope && m.Key == "dashboard",
    () => new ManagedMessage { GuildId = guildId, Scope = scope, Key = "dashboard" },
    m => m.ContentHash = hash);

if (!changed)
{
    return; // nothing changed — the Discord edit this would gate is skipped too
}
```

## It forces tracking, even under `QueryTrackingBehavior.NoTracking`

The natural-key read calls `.AsTracking()` explicitly, so the row `UpsertAsync`
hands back is tracked even in a context configured with
`UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)`. This is
deliberate: the dirty check reads `context.Entry(row).State`, and a detached
entry always reports `Detached`, never `Modified` — under a context-wide
`NoTracking` default, an untracked read would make every mutation look like a
no-op and silently drop it instead of saving it.
`tests/Persistord.Core.Tests/UpsertTests.cs`'s
`UpsertIfChanged_persists_the_change_under_context_wide_NoTracking` pins this
behaviour. The side effect worth knowing about: an entity returned from
`UpsertAsync`/`UpsertIfChangedAsync` stays in the change tracker afterwards,
even under a `NoTracking` context and even if nothing else in that unit of work
is tracked.

## `SaveChangesAsync` flushes the whole context

Both methods call `context.SaveChangesAsync()` — the context's, covering every
pending change it tracks, not a per-entity save scoped to just the upserted
row. Stage several unrelated changes on the same context before calling
`UpsertAsync`, and its `SaveChangesAsync` call flushes all of them, not only
the natural-key row it was asked to upsert.

## What it deliberately does not do

- **No bulk upsert.** One natural key, one row, one round trip. Looping
  `UpsertAsync` over a batch is one read/write pair per iteration — fine at
  gateway-event volume, not built for a bulk import.
- **No conflict resolution beyond "the loser re-reads and mutates the
  winner."** There is no merge policy and no field-by-field comparison; the
  same `update` callback the caller already wrote runs again, once, against
  whichever row is actually there after the race.
- **No `ON CONFLICT` / `MERGE` generation.** Race recovery is a `catch` block
  around a second read, not a provider-specific upsert statement, which is why
  it behaves identically on SQLite, PostgreSQL, and SQL Server.
- **No multi-row merge.** `naturalKey` must match at most one row; a predicate
  that matches more throws from `SingleOrDefaultAsync` rather than picking one.

## Before and after

Hand-written, this is the pattern `UpsertAsync` exists to replace:

```csharp
var channel = await db.Set<ManagedChannel>()
    .SingleOrDefaultAsync(c => c.GuildId == guildId && c.Scope == scope && c.Key == "announcements");

if (channel is null)
{
    channel = new ManagedChannel { GuildId = guildId, Scope = scope, Key = "announcements" };
    channel.DiscordId = discordChannelId;
    db.Set<ManagedChannel>().Add(channel);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        // Lost the race: someone else inserted the same natural key first.
        db.Entry(channel).State = EntityState.Detached;
        channel = await db.Set<ManagedChannel>()
            .SingleOrDefaultAsync(c => c.GuildId == guildId && c.Scope == scope && c.Key == "announcements");
        if (channel is null)
        {
            throw;
        }

        channel.DiscordId = discordChannelId;
        await db.SaveChangesAsync();
    }
}
else
{
    channel.DiscordId = discordChannelId;
    await db.SaveChangesAsync();
}
```

Collapses to one call:

```csharp
var channel = await db.Set<ManagedChannel>().UpsertAsync(
    c => c.GuildId == guildId && c.Scope == scope && c.Key == "announcements",
    () => new ManagedChannel { GuildId = guildId, Scope = scope, Key = "announcements" },
    c => c.DiscordId = discordChannelId);
```

`Persistord.Managed`'s own `UpsertManagedAsync` is this same call, generalized
across the four managed resource types — see
[Managed Resources](managed-resources.md#the-reconcile-loop).

## See also

- [Guild Lifecycle](guild-lifecycle.md) — `UpsertAsync` against `GuildEntity`
  on `JoinedGuild`/`LeftGuild`.
- [Managed Resources](managed-resources.md) — `UpsertManagedAsync`, built on
  `UpsertAsync`.
- [DbContext Lifetime](dbcontext-lifetime.md) — the short-lived context every
  snippet above assumes.
