# Core Graph

`Persistord.Core` ships an abstract `DiscordDbContext` and five skeleton entity
types that mirror the core Discord object graph. Every derived context inherits
these automatically — you only declare the module `DbSet`s your bot needs.

## DiscordDbContext

`DiscordDbContext` is the base class you inherit:

```csharp
public sealed class MyBotContext : DiscordDbContext
{
    public MyBotContext(DbContextOptions<MyBotContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // core skeleton + snowflake convention
        // apply optional modules here
    }
}
```

The base implementation calls `ApplyCoreConfiguration()` (which wires the core
entity type configurations) and `ConfigureConventions()` (which registers the
snowflake converters globally). See [Snowflake Conversion](snowflake-conversion.md).

## Skeleton DbSets

The base context exposes these `DbSet`s directly — no declaration needed in your
derived class:

```csharp
public DbSet<GuildEntity>   Guilds   => Set<GuildEntity>();
public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();
public DbSet<UserEntity>    Users    => Set<UserEntity>();
public DbSet<MemberEntity>  Members  => Set<MemberEntity>();
public DbSet<RoleEntity>    Roles    => Set<RoleEntity>();
```

## Entity shapes

All entities are plain POCOs. They carry no Discord client library types.

### GuildEntity

| Property | Type | Notes |
| --- | --- | --- |
| `Id` | `ulong` | Primary key, snowflake |
| `Name` | `string` | Guild name |
| `OwnerId` | `ulong` | Snowflake of the guild owner |

### ChannelEntity

| Property | Type | Notes |
| --- | --- | --- |
| `Id` | `ulong` | Primary key, snowflake |
| `GuildId` | `ulong` | Foreign key to `GuildEntity` |
| `ParentId` | `ulong?` | Nullable self-referencing FK — categories own channels, channels own threads |
| `Type` | enum | Channel type discriminator (text, voice, category, thread, …) |
| `Name` | `string` | Channel name |

Channel polymorphism uses **table-per-hierarchy** — a single table with a `Type`
discriminator column. The self-referencing `ParentId` models the category →
channel → thread hierarchy.

### UserEntity

| Property | Type | Notes |
| --- | --- | --- |
| `Id` | `ulong` | Primary key, snowflake |
| `Username` | `string` | Discord username |
| `GlobalName` | `string?` | Display name (distinct from per-guild nickname) |

### MemberEntity

| Property | Type | Notes |
| --- | --- | --- |
| `GuildId` | `ulong` | Part of composite primary key |
| `UserId` | `ulong` | Part of composite primary key |
| `Nickname` | `string?` | Guild-specific nickname |
| `JoinedAt` | `DateTimeOffset?` | When the member joined the guild |

`MemberEntity` uses a composite primary key `(GuildId, UserId)`.

### RoleEntity

| Property | Type | Notes |
| --- | --- | --- |
| `Id` | `ulong` | Primary key, snowflake |
| `GuildId` | `ulong` | Foreign key to `GuildEntity` |
| `Name` | `string` | Role name |
| `Permissions` | `ulong` | Discord permission bitfield |
| `Color` | `int` | Role color as an integer |

## See also

- [Snowflake Conversion](snowflake-conversion.md) — how `ulong` IDs are stored as `long`.
- [Messages](messages.md) — the `MessageEntity` module that builds on the core graph.
