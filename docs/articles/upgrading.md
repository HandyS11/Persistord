---
description: The breaking changes between Persistord releases, and what each asks of your code and your next migration.
---

# Upgrading

This page lists the breaking changes between Persistord releases and what each one asks of your code and your next migration.

New notes are added at the top, one `##` section per release.

## Upgrading from 1.0.0-beta2

### `DiscordDbContext` no longer maps the skeleton

If you use `Guilds`/`Channels`/`Users`/`Members`/`Roles`, change your base class to
`DiscordGraphDbContext`. If you never did but your derived context's committed
model snapshot still has those five tables in it (any `DiscordDbContext`
consumer from beta2 does), your next `dotnet ef migrations add` diffs against
that snapshot and emits `DropTable` for `Guilds`, `Channels`, `Users`,
`Members` and `Roles` — usually what you want.

> [!CAUTION]
> Review the generated migration before running `database update`: this is the most likely way
> to lose data on this upgrade.

`ApplyCoreConfiguration()` is renamed `ApplyCoreGraph()`; the old name forwards for one release.

### A bare `ulong` primary key is no longer store-generated

On `1.0.0-beta2`, a consumer's own `public ulong Id { get; set; }` primary key
was `ValueGenerated.OnAdd` by EF's own convention — SQLite emitted `"Id"
INTEGER NOT NULL ... PRIMARY KEY AUTOINCREMENT` for it. `SnowflakeKeyConvention`
(see [Snowflake Conversion](snowflake-conversion.md)) now marks every `ulong`
or `ulong?` primary-key property `ValueGenerated.Never`, because a Discord
snowflake, a Steam64 id, or any other unsigned 64-bit key is a value the
caller already owns, never one the database should assign — intended
behaviour, not a regression. Two things follow for an upgrading consumer: your next
`dotnet ef migrations add` drops the identity/autoincrement from that column,
and code that relied on EF assigning the key (leaving `Id` as `0` on a new
row before `SaveChangesAsync`) now inserts a literal `0` and collides on the
second such row. If you genuinely want a store-generated `ulong` key, call
`.Property(e => e.Id).ValueGeneratedOnAdd()` explicitly on that entity —
explicit fluent configuration wins over the convention.

### `GuildEntity.Name` and `OwnerId` are optional

`Name` and `OwnerId` were required; they are now optional, and `JoinedAt`/`LeftAt` are new
columns. A consumer with an existing `Guilds` table needs a migration — on SQLite, relaxing a
column to nullable is a table rebuild, which `dotnet ef migrations add` emits for you. Code
reading `guild.Name` or `guild.OwnerId` now gets a nullable value and must handle `null`.

## See also

- [Core Graph](core-graph.md) — the two base contexts and the skeleton entities these changes touch.
- [Snowflake Conversion](snowflake-conversion.md) — `SnowflakeKeyConvention`, behind the key change.
- [Migrations](migrations.md) — generating the migration each change needs.
- [Release notes](https://github.com/HandyS11/Persistord/releases) — every release, on GitHub.
