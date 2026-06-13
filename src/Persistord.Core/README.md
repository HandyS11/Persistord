# Persistord.Core

Foundation package for [Persistord](https://github.com/HandyS11/Persistord), a
provider-agnostic, Discord-library-agnostic persistence layer for Discord bots
built on EF Core 10.

`Persistord.Core` ships:

- **Snowflake conversion** — Discord ids are `ulong`; relational providers store
  signed `long`. `UlongToLongConverter` / `NullableUlongToLongConverter` perform a
  bit-faithful `unchecked` round-trip, so every value (including ids with the high
  bit set) survives storage exactly. The conversion is registered globally in
  `DiscordDbContext.ConfigureConventions`, so you never annotate individual ids.
- **`DiscordDbContext`** — an abstract base context that maps the core skeleton
  entities (`GuildEntity`, `ChannelEntity`, `UserEntity`, `MemberEntity`,
  `RoleEntity`) and applies the snowflake convention. Inherit it, add the module
  `DbSet`s you want, and apply module configurations in `OnModelCreating`.
- **`ApplyCoreConfiguration()`** — a `ModelBuilder` extension that wires the core
  entity configurations. `DiscordDbContext` calls it for you.

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
