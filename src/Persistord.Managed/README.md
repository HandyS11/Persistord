# Persistord.Managed

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Managed.svg?label=Persistord.Managed)](https://www.nuget.org/packages/Persistord.Managed)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Managed.svg)](https://www.nuget.org/packages/Persistord.Managed)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Entities and model wiring for [Persistord](https://github.com/HandyS11/Persistord)
that remember the Discord resources *your bot itself created and owns* — a
category, a channel, a message it edits in place, a webhook — so it stops
re-discovering them by name on every boot and hand-rolling bespoke tables to
track what it made.

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

- **`ManagedCategory`** — a category the bot created.
- **`ManagedChannel`** — a channel the bot created, with an optional
  `ParentDiscordId` for the category or parent channel it was created under.
- **`ManagedMessage`** — a message the bot posted and edits in place: a
  dashboard, a per-item embed, a prompt whose buttons must survive a restart.
  Carries `ChannelDiscordId` and an optional `ContentHash` for render gating —
  hash the next payload, compare, and skip the edit when it matches. Also
  indexed by `DiscordId` alone, because a `MessageDeleted` gateway event
  carries only the message id.
- **`ManagedWebhook`** — a webhook the bot created, with `ChannelDiscordId`
  and a `Token`. `Token` is annotated `[Protected]`
  (`Persistord.Core.Abstractions.ProtectedAttribute`), which is inert on its
  own — reference `Persistord.Protection` and call `ApplyProtection` to
  encrypt it at rest. **Without that, the token is stored in plaintext.**

All four derive from the abstract `ManagedResource`, which also carries the
surrogate `Id`, `CreatedAt`, and `UpdatedAt` (stamped by
`Persistord.Core`'s `TimestampInterceptor` when your context is constructed
with a `TimeProvider`).

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
        modelBuilder.ApplyGuildRoot(); // optional: cascade deletes from GuildEntity
    }
}
```

`ApplyManagedModule` maps all four resource types regardless of which `DbSet`s
you declare — an unused one just costs an empty table.

## Table names are pinned

Each resource is mapped to a fixed table name (`ManagedCategories`,
`ManagedChannels`, `ManagedMessages`, `ManagedWebhooks`) via `ToTable(...)` in
its configuration, rather than letting EF Core name the table after the
`DbSet` property you declare. These tables belong to the module: renaming your
`DbSet` property must not move the table.

## License

MIT
