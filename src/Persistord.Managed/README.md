# Persistord.Managed

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Managed.svg?label=Persistord.Managed)](https://www.nuget.org/packages/Persistord.Managed)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Managed.svg)](https://www.nuget.org/packages/Persistord.Managed)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Entities, model wiring, and store helpers for
[Persistord](https://github.com/HandyS11/Persistord) that remember the Discord
resources *your bot itself created and owns* — a category, a channel, a
message it edits in place, a webhook — so it stops re-discovering them by name
on every boot and hand-rolling bespoke tables to track what it made. This
module is deliberately **not a mirror**: it does not shadow every channel or
message in the guild the way `Persistord.Core`'s skeleton or
`Persistord.Messages` do. It records only the handful of resources your bot
itself created, keyed by a name *you* chose, not by Discord's own graph.

## The shared shape

Every managed resource is a row keyed by `(GuildId, Scope, Key)` plus the
Discord snowflake the bot got back:

- **`GuildId`** — the owning guild.
- **`Scope`** — an opaque partition inside the guild, chosen by *you*: a
  game-server id, a playlist id, whatever your resources hang off. It is not a
  foreign key, so this module never couples to your own tables.
  `ManagedScope.Global` (`""`) means guild-wide. `Scope` is a non-nullable
  `string` in the database and on the CLR type — not `string?` behind a value
  converter — because a converter does not travel through null comparisons:
  `Where(m => m.Scope == null)` would translate to `IS NULL` and silently
  match nothing. Pass `null` to `ManagedScope.Normalize` (or to any helper
  built on this module) and get `ManagedScope.Global` back.
- **`Key`** — your stable identifier for the resource, unique within a guild
  and scope.
- **`DiscordId`** — the snowflake Discord handed back when the resource was
  created.

`(GuildId, Scope, Key)` is a unique index on every managed resource, so
"find-or-create" is a single indexed lookup.

## The four resources

All four are keyed by the same natural key and derive from the abstract
`ManagedResource`, which also carries the surrogate `Id`, `CreatedAt`, and
`UpdatedAt` (stamped by `Persistord.Core`'s `TimestampInterceptor` when your
context is constructed with a `TimeProvider`).

| Entity            | Natural key              | Extra columns                                   |
| ----------------- | ------------------------- | ------------------------------------------------ |
| `ManagedCategory` | `(GuildId, Scope, Key)`   | —                                                  |
| `ManagedChannel`  | `(GuildId, Scope, Key)`   | `ParentDiscordId?` — the parent it was created under, if any. |
| `ManagedMessage`  | `(GuildId, Scope, Key)`   | `ChannelDiscordId`, `ContentHash?` — see [ContentHash and render gating](#contenthash-and-render-gating). Also indexed on `DiscordId` alone, because a `MessageDeleted` gateway event carries only the message id. |
| `ManagedWebhook`  | `(GuildId, Scope, Key)`   | `ChannelDiscordId`, `Token` — see the warning below. |

`ManagedWebhook.Token` is annotated `[Protected]`
(`Persistord.Core.Abstractions.ProtectedAttribute`), which is inert on its
own — reference `Persistord.Protection` and call `ApplyProtection` to encrypt
it at rest. **Without that, the token is stored in plaintext.**

## Store helpers

`ManagedStoreExtensions` adds four `DbContext` extension methods. They are
thin wrappers over `Persistord.Core`'s `UpsertAsync`, not a repository layer:
you keep your own `DbContext` and your own reconciler, and nothing here ever
talks to Discord.

- `Task<TResource> UpsertManagedAsync<TResource>(this DbContext context, ulong guildId, string? scope, string key, ulong discordId, Action<TResource>? configure = null, CancellationToken cancellationToken = default) where TResource : ManagedResource, new()` —
  creates or updates the record for `(guildId, scope, key)`. `configure` sets
  the type-specific columns (a channel's parent, a message's channel and
  content hash, a webhook's token) and runs on created and existing rows
  alike.
- `Task<TResource?> FindManagedAsync<TResource>(this DbContext context, ulong guildId, string? scope, string key, CancellationToken cancellationToken = default) where TResource : ManagedResource` —
  reads one record by its natural key, or `null` when there is none.
- `Task<int> DeleteScopeAsync(this DbContext context, ulong guildId, string? scope, CancellationToken cancellationToken = default)` —
  deletes every managed record of one scope, across all four tables, in one
  transaction. Use it when the thing the scope stood for is gone — a game
  server was unpaired, a playlist was deleted. This deletes *records*, never
  Discord objects: tear those down in Discord first.
- `Task<IReadOnlyList<string>> ListScopesAsync(this DbContext context, ulong guildId, CancellationToken cancellationToken = default)` —
  lists the distinct scopes that still have records in the guild, sorted,
  excluding `ManagedScope.Global`. For a bot that scopes by game server, this
  answers "which servers do I still hold resources for?".

## Reconciling with Discord

The module never talks to Discord — "does the resource still exist, and does
it still look right" is your reconciler's loop. `FindManagedAsync` and
`UpsertManagedAsync` are what make the record side of that loop trivial:

```csharp
var record = await context.FindManagedAsync<ManagedMessage>(guildId, scope: "server-7", key: "dashboard");

IUserMessage? message = record is null
    ? null
    : await TryGetMessageAsync(channel, record.DiscordId); // null on a 404

message ??= await channel.SendMessageAsync(embed: BuildDashboardEmbed());

await context.UpsertManagedAsync<ManagedMessage>(
    guildId,
    scope: "server-7",
    key: "dashboard",
    discordId: message.Id,
    configure: m => m.ChannelDiscordId = channel.Id);
```

`FindManagedAsync`, the Discord round trip, and `UpsertManagedAsync` are three
separate steps on purpose: this module owns none of the middle one.

## ContentHash and render gating

`ManagedMessage.ContentHash` exists to skip needless edits. The module never
computes it — the payload is yours — but the pattern is: hash the payload you
are about to render, compare it against the stored `ContentHash`, and only
call Discord's edit endpoint (and write the new hash back via
`UpsertManagedAsync`'s `configure` callback) when the hash changed. A bot that
redraws a dashboard on every gateway event, but only actually edits the
message when its content changed, is what this column is for.

## Wiring it up

Declare the `DbSet`s you actually use and call `ApplyManagedModule()` from
`OnModelCreating`:

```csharp
public sealed class MyBotContext(DbContextOptions<MyBotContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<ManagedCategory> Categories => Set<ManagedCategory>();

    public DbSet<ManagedChannel> Channels => Set<ManagedChannel>();

    public DbSet<ManagedMessage> Messages => Set<ManagedMessage>();

    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();
        modelBuilder.ApplyGuildRoot(cascade: false); // optional: see the note below
    }
}
```

`ApplyManagedModule` maps all four resource types regardless of which `DbSet`s
you declare — an unused one just costs an empty table.

`ApplyGuildRoot()`'s default is `cascade: true`, which wires a cascading
foreign key from every managed resource's `GuildId` to `GuildEntity` and makes
the guild row a **prerequisite**: a managed resource written before its
guild's row exists fails with a foreign-key violation. Pass `cascade: true`
once your bot always creates the `GuildEntity` row on `JoinedGuild` before
reconciling anything scoped to that guild — see the
[Guild Lifecycle](https://handys11.github.io/Persistord/articles/guild-lifecycle.html)
article on the documentation site.

## Table names are pinned

Each resource is mapped to a fixed table name (`ManagedCategories`,
`ManagedChannels`, `ManagedMessages`, `ManagedWebhooks`) via `ToTable(...)` in
its configuration, rather than letting EF Core name the table after the
`DbSet` property you declare. These tables belong to the module: renaming your
`DbSet` property must not move the table.

## License

MIT
