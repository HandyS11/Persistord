# Persistord.Core

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Core.svg?label=Persistord.Core)](https://www.nuget.org/packages/Persistord.Core)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Core.svg)](https://www.nuget.org/packages/Persistord.Core)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Foundation package for [Persistord](https://github.com/HandyS11/Persistord), a
provider-agnostic, Discord-library-agnostic persistence layer for Discord bots
built on EF Core 10.

`Persistord.Core` ships:

- **Snowflake conversion** — Discord ids are `ulong`; relational providers store
  signed `long`. `UlongToLongConverter` / `NullableUlongToLongConverter` perform a
  bit-faithful `unchecked` round-trip, so every value (including ids with the high
  bit set) survives storage exactly. The conversion is registered globally in
  `DiscordDbContext.ConfigureConventions`, so you never annotate individual ids.
- **`DiscordDbContext`** — an abstract base context that applies the snowflake
  convention and maps no entity types. Inherit it, add the module `DbSet`s you
  want, and apply module configurations in `OnModelCreating`.
- **`DiscordGraphDbContext`** — derives from `DiscordDbContext` and adds the
  opt-in core skeleton entities (`GuildEntity`, `ChannelEntity`, `UserEntity`,
  `MemberEntity`, `RoleEntity`). Inherit it instead when your context mirrors
  Discord's guild/channel/user/member/role graph.
- **`ApplyCoreGraph()`** — a `ModelBuilder` extension that wires the core
  entity configurations. `DiscordGraphDbContext` calls it for you.

## What's in the box

- **`DiscordDbContext`** (conventions) —
  `abstract class DiscordDbContext : DbContext`. Applies the snowflake and
  guild-scope conventions and maps no entity types.
- **`DiscordGraphDbContext`** (skeleton) —
  `abstract class DiscordGraphDbContext : DiscordDbContext`. Adds the opt-in
  guild/channel/user/member/role skeleton entities.
- **`ApplyCoreGraph`** —
  `ModelBuilder ApplyCoreGraph(this ModelBuilder modelBuilder)`. Wires the
  skeleton entity configurations.
- **`ApplyGuildRoot`** —
  `ModelBuilder ApplyGuildRoot(this ModelBuilder modelBuilder, bool cascade = true, bool filterLeftGuilds = false)`.
  Registers `GuildEntity` as the tenant root, optionally cascading deletes to
  every `IGuildScoped` entity and filtering guilds that have left.
- **`IGuildScoped`** —
  `interface IGuildScoped { ulong GuildId { get; } }`. Marks a row as
  belonging to exactly one guild.
- **`ICreatedAt` / `IUpdatedAt` + `TimestampInterceptor`** —
  `DateTimeOffset CreatedAt { get; set; }` / `DateTimeOffset UpdatedAt { get; set; }`,
  stamped by `TimestampInterceptor : SaveChangesInterceptor` from a
  `TimeProvider` on every save.
- **`UpsertAsync` / `UpsertIfChangedAsync`** —
  `Task<TEntity> UpsertAsync<TEntity>(this DbSet<TEntity> set, Expression<Func<TEntity, bool>> naturalKey, Func<TEntity> create, Action<TEntity> update, CancellationToken cancellationToken = default)`.
  Natural-key create-or-update with lost-insert-race recovery.
- **`PurgeGuildAsync`** —
  `Task<int> PurgeGuildAsync(this DbContext context, ulong guildId, CancellationToken cancellationToken = default)`.
  Deletes every `IGuildScoped` row of one guild, plus its `GuildEntity` row,
  dependents before principals, in one transaction.
- **`ClearAllTablesAsync`** —
  `Task<int> ClearAllTablesAsync(this DbContext context, CancellationToken cancellationToken = default)`.
  Deletes every row of every mapped table, dependents before principals, for
  test teardown and local database resets.

## Provider-agnostic by design

The library **never** selects a database provider. It defines the model only. You
choose the provider in your own composition root:

```csharp
services.AddDbContextFactory<MyBotContext>(o => o.UseNpgsql(connectionString));
// or .UseSqlite(...), .UseSqlServer(...), etc.
```

## Context lifetime

A Discord bot is long-lived and highly concurrent. **Do not** hold a single
`DbContext` for the bot's lifetime — its change tracker grows unbounded and it is
not thread-safe. Use `IDbContextFactory<T>` and create a short-lived context per
unit of work (per gateway event, per command):

```csharp
await using var db = await factory.CreateDbContextAsync();
db.Guilds.Add(new GuildEntity { Id = guildId, Name = name, OwnerId = ownerId });
await db.SaveChangesAsync();
```

## Snowflake storage note

Snowflakes are stored as `long`. Discord snowflakes stay below `long.MaxValue`
until roughly the year 2084, so signed storage is safe; the converter is
nevertheless bit-faithful and would round-trip even past that point.

## License

MIT
