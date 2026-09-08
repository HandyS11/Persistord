# Managed Resources

`Persistord.Managed` remembers the Discord resources *your bot itself created and
owns* — a category, a channel, a message it edits in place, a webhook — keyed by a
name you chose. It is not a mirror: it does not shadow every channel or message in a
guild the way `Persistord.Core`'s skeleton graph or `Persistord.Messages` do, and it
never calls Discord. It only makes the record side of "did I already create this?"
trivial, so your bot stops re-discovering resources by name on every boot.

## The four entities

Every managed resource derives from the abstract `ManagedResource`, which is not
mapped as an entity type itself — EF maps only the four concrete resources below,
each to its own pinned table (`ManagedCategories`, `ManagedChannels`,
`ManagedMessages`, `ManagedWebhooks`). `ManagedResource` carries the shape they all
share:

- `Id` (`long`) — surrogate primary key.
- `GuildId` (`ulong`) — the owning guild.
- `Scope` (`string`) — an opaque consumer partition. See [Scope](#scope) below.
- `Key` (`string`) — your stable identifier for the resource.
- `DiscordId` (`ulong`) — the snowflake Discord handed back.
- `CreatedAt` / `UpdatedAt` (`DateTimeOffset`) — stamped by `Persistord.Core`'s
  `TimestampInterceptor` when the context is constructed with a `TimeProvider`.

`(GuildId, Scope, Key)` is a unique index on every one of the four tables, so
"find-or-create" is a single indexed lookup.

| Entity | Extra columns | Notes |
| --- | --- | --- |
| `ManagedCategory` | — | just the shared shape |
| `ManagedChannel` | `ParentDiscordId?` | the category or parent channel it was created under, if any |
| `ManagedMessage` | `ChannelDiscordId`, `ContentHash?` | also indexed on `DiscordId` alone — a `MessageDeleted` gateway event carries only the message id |
| `ManagedWebhook` | `ChannelDiscordId`, `Token` | **`Token` is stored in plaintext unless you reference `Persistord.Protection` and call `ApplyProtection`** — see [Protection](protection.md) |

## Wiring

Declare the `DbSet`s you actually use, call `ApplyManagedModule()` from
`OnModelCreating`, and optionally `ApplyGuildRoot()` if `GuildEntity` is your tenant
root (see [Core Graph](core-graph.md)):

```csharp
public sealed class MyBotContext(DbContextOptions<MyBotContext> options)
    : DiscordDbContext(options)
{
    public DbSet<ManagedCategory> Categories => Set<ManagedCategory>();

    public DbSet<ManagedChannel> Channels => Set<ManagedChannel>();

    public DbSet<ManagedMessage> Messages => Set<ManagedMessage>();

    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();
        modelBuilder.ApplyGuildRoot(); // optional: cascade deletes from GuildEntity
    }
}
```

`ApplyManagedModule()` maps all four resource types regardless of which `DbSet`s you
declare — an unused one just costs an empty table.

## Scope

`Scope` is an opaque consumer string, chosen by *you* — a game-server id, a playlist
id, whatever your resources hang off. It is never a foreign key, so this module never
couples to your own tables.

`Scope` is **never null**. `ManagedScope.Global` is `""`, and `ManagedScope.Normalize(string?)`
turns a caller's `null` into it. This is deliberate: SQL treats NULLs as distinct in a
unique index, so `(GuildId, NULL, Key)` would happily accept duplicate guild-wide
rows on SQLite and PostgreSQL alike. Making `Scope` non-nullable with `""` for
"global" is what keeps `(GuildId, Scope, Key)` a real unique key.

The sharp edge: a consumer writing raw LINQ against a managed `DbSet` must compare
`Scope` against `""`, not `null`.

```csharp
// Wrong — Scope is never null, so this predicate matches nothing.
var globalChannels = await context.Channels
    .Where(c => c.GuildId == guildId && c.Scope == null)
    .ToListAsync();
```

```csharp
// Right.
var globalChannels = await context.Channels
    .Where(c => c.GuildId == guildId && c.Scope == ManagedScope.Global)
    .ToListAsync();
```

The store helpers (`UpsertManagedAsync`, `FindManagedAsync`, `DeleteScopeAsync`) all
accept `scope` as `string?` and normalize it for you, so this only bites code that
queries the `DbSet`s directly.

## The reconcile loop

The module never talks to Discord — "does the resource still exist, and does it
still look right" is your reconciler's loop. `FindManagedAsync` and
`UpsertManagedAsync` make the record side of that loop trivial:

```csharp
var record = await db.FindManagedAsync<ManagedMessage>(guildId, scope, "dashboard");
var message = record is null ? null : await channel.GetMessageAsync(record.DiscordId);

if (message is null)
{
    message = await channel.SendMessageAsync(embed: embed);
}
else
{
    await message.ModifyAsync(m => m.Embed = embed);
}

await db.UpsertManagedAsync<ManagedMessage>(guildId, scope, "dashboard", message.Id, m =>
{
    m.ChannelDiscordId = channel.Id;
    m.ContentHash = hash;
});
```

`FindManagedAsync`, the Discord round trip, and `UpsertManagedAsync` are three
separate steps on purpose: this module owns none of the middle one.

## Render gating with `ContentHash`

`ManagedMessage.ContentHash` exists to skip needless edits. The module never
computes it — the payload is yours — but the pattern is: hash the payload you are
about to render, compare it against the stored `ContentHash`, and only call
Discord's edit endpoint (and write the new hash back through `UpsertManagedAsync`'s
`configure` callback, as in the snippet above) when the hash actually changed.

```csharp
var hash = ComputeHash(payload);
if (record?.ContentHash == hash)
{
    return; // nothing changed since the last render
}
```

This survives a restart. A process-local cache of "what did I last render" does not
— it starts empty every time the bot restarts, and pays for one redundant edit per
message.

## Tearing down a scope

`DeleteScopeAsync` deletes every managed record of one scope, across all four
tables, in one transaction — use it once the thing the scope stood for is gone (a
game server unpaired, a playlist deleted). It deletes *records*, never Discord
objects: tear those down in Discord first.

```csharp
// Discord side first: this module never talks to Discord.
await TearDownDiscordResourcesAsync(guildId, scope);

// Then forget the records.
var deleted = await db.DeleteScopeAsync(guildId, scope);
```

`ListScopesAsync` lists the distinct scopes that still have records in a guild,
sorted and excluding `ManagedScope.Global` — useful to answer "which scopes do I
still hold resources for?" before deciding what to tear down:

```csharp
var activeScopes = await db.ListScopesAsync(guildId); // e.g. ["server-1", "server-2"]
```

## What this module deliberately does not do

- It never talks to Discord. Fetching a resource, deciding it is stale, and
  recreating it is entirely your reconciler's job.
- It has no notion of channel type, permissions, or name. Those are spec-driven in
  your bot and re-derived every reconcile pass, not persisted here.

## See also

- [Core Graph](core-graph.md) — `DiscordDbContext`, `GuildEntity`, and
  `ApplyGuildRoot()`.
- [Protection](protection.md) — encrypting `ManagedWebhook.Token` at rest.
- [Testing](testing.md) — in-memory SQLite fixtures for testing a context that
  applies this module.
