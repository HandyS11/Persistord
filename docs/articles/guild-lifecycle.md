# Guild Lifecycle

The recipe for keeping `GuildEntity` in step with the two gateway events that
bracket a bot's presence in a guild: `JoinedGuild` and `LeftGuild`. It builds on
`UpsertAsync`, `ApplyGuildRoot`, and `PurgeGuildAsync` — see
[Upsert](upsert.md) and [Core Graph](core-graph.md) for those on their own.
Every snippet assumes a short-lived `DbContext` from `IDbContextFactory` (see
[DbContext Lifetime](dbcontext-lifetime.md)) built on `DiscordGraphDbContext`, so
`db.Guilds` is already exposed, and a constructor-injected `TimeProvider clock`
for the timestamps — the same clock `Persistord.Core`'s own
`TimestampInterceptor` takes.

## On `JoinedGuild`: upsert the root row

```csharp
await db.Guilds.UpsertAsync(
    g => g.Id == guild.Id,
    () => new GuildEntity { Id = guild.Id },
    g =>
    {
        g.Name = guild.Name;
        g.JoinedAt ??= clock.GetUtcNow();
        g.LeftAt = null;
    });
```

This is safe to call whether or not a row already exists: a first join inserts
it, a re-invite after a soft-marked leave finds the existing row, clears
`LeftAt`, and — because `JoinedAt` is set with `??=`, not `=` — keeps the
*original* join timestamp rather than overwriting it with the re-invite time.
Assign it unconditionally instead if you want `JoinedAt` to track the most
recent join.

## On `LeftGuild`: soft mark or hard purge

Handling `LeftGuild` is a choice between two policies, and Persistord makes
neither for you:

**Soft mark** — stamp `LeftAt` and keep every row, so a re-invite finds its
history intact:

```csharp
await db.Guilds.UpsertAsync(
    g => g.Id == guild.Id,
    () => new GuildEntity { Id = guild.Id },
    g => g.LeftAt = clock.GetUtcNow());
```

A non-null `LeftAt` is just data until you act on it. To also hide left guilds
from ordinary queries, call `ApplyGuildRoot(filterLeftGuilds: true)` once in
`OnModelCreating` — it is a model-wide switch, not something you toggle per
event — and reach for `IgnoreQueryFilters()` on the rare query that needs to see
them anyway.

**Hard purge** — erase the guild's rows outright with `PurgeGuildAsync` (see
[Purge behind a lock](#purge-behind-a-per-guild-lock) below). `PurgeGuildAsync`
ignores query filters itself, so a guild already hidden by
`filterLeftGuilds: true` is still reachable and still gets purged.

**Pick one.** With neither policy wired to `LeftGuild`, a left guild's rows
outlive the guild forever — nothing in Persistord removes them on its own.

`PurgeGuildAsync` only ranks `IGuildScoped` types in its delete order. The five
skeleton entities from `DiscordGraphDbContext` are deliberately not
`IGuildScoped` — see [Core Graph](core-graph.md#guildentity) for why — so a
consumer who also mirrors `ChannelEntity`, `RoleEntity`, `MemberEntity`, and the
rest must delete those itself, before or after calling `PurgeGuildAsync`.

## Purge behind a per-guild lock

Discord can and does deliver `LeftGuild` more than once for the same guild —
most commonly around a gateway resume — and a reconciler pass can be mid-write
for that guild when the second one arrives. Serialize purge and reconcile work
per guild so the two can never interleave:

```csharp
using (await _guildLocks.AcquireAsync(guildId, cancellationToken))
{
    await db.PurgeGuildAsync(guildId, cancellationToken);
}
```

`_guildLocks` here is your own per-guild async lock (an async-friendly mutex
keyed by guild id) — Persistord ships no locking primitive; `PurgeGuildAsync`
only guarantees that its own deletes run in one transaction, not that nothing
else touches the guild while it runs.

## Order of operations: Discord first, then the database

`PurgeGuildAsync` deletes *records*, never Discord objects — same rule as
`DeleteScopeAsync` in [Managed Resources](managed-resources.md#tearing-down-a-scope).
Tear down whatever Discord resources the guild's rows point at before deleting
the rows that remember them, not after: once the record is gone, so is your
only lead back to the Discord object.

```csharp
using (await _guildLocks.AcquireAsync(guildId, cancellationToken))
{
    await TearDownDiscordResourcesAsync(guildId, cancellationToken); // Discord side first
    await db.PurgeGuildAsync(guildId, cancellationToken);            // then forget the records
}
```

## What `cascade: true` buys — and costs

`ApplyGuildRoot`'s default, `cascade: true`, wires a cascading foreign key from
every `IGuildScoped` entity's `GuildId` to `GuildEntity`. That buys you a
database-level `PurgeGuildAsync` alternative for free: deleting the `Guilds` row
directly deletes every scoped row with it, no application code involved.

It also costs you an insert order: the guild row becomes a prerequisite for
every scoped insert. A scoped row written before its guild's `JoinedGuild`
upsert has run fails with a foreign-key violation, not a queued write. If scoped
rows legitimately need to arrive before, or outlive, their guild row, pass
`cascade: false` — the same choice `PurgeGuildAsync`'s own tests make, because a
guild root with no cascading foreign key is the harder case that helper exists
for: nothing in the database deletes those rows without it.

## See also

- [Core Graph](core-graph.md) — `GuildEntity`, `ApplyGuildRoot`, and why the
  five skeleton entities aren't `IGuildScoped`.
- [Upsert](upsert.md) — the `UpsertAsync` signature and dirty-check both
  snippets above rely on.
- [Managed Resources](managed-resources.md) — `DeleteScopeAsync`, the
  scope-level equivalent of a guild purge.
- [DbContext Lifetime](dbcontext-lifetime.md) — the short-lived `DbContext`
  every snippet here assumes.
