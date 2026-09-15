---
description: Build a context that persists Discord messages and their edit history, register a database provider, and create its schema with a migration.
---

# Getting Started

This tutorial takes a bot project from no Persistord reference to a database schema ready for its first messages and history rows.

## What you'll build

A `MyBotContext` that stores Discord messages and an append-only history of their changes,
registered against the database provider you choose, with its schema created by an EF Core
migration. Along the way you pick a base context, write your first records through a short-lived
context, and, if your bot uses one, map messages straight from your Discord library.

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and a bot project to add
  Persistord to.
- The EF Core command-line tools, for the last step: `dotnet tool install --global dotnet-ef`,
  plus a reference to `Microsoft.EntityFrameworkCore.Design` in the bot project.
- A database: a PostgreSQL or SQL Server instance, or nothing at all for SQLite.

## 1. Install the packages

The easiest way is the meta package, which pulls in `Core`, `Messages`, and
`History` in one reference:

```bash
# Recommended: the full library-neutral stack in one package
dotnet add package Persistord
```

Or install the same modules individually:

```bash
dotnet add package Persistord.Core
dotnet add package Persistord.Messages      # optional: message persistence
dotnet add package Persistord.History       # optional: requires Messages
```

That is the library-neutral mirror stack the meta package bundles. Step 5 adds a Discord-library
adapter if you want one. Three more packages exist outside the stack, opt in and installed
separately when you need them: `Persistord.Managed` (records of resources your bot creates and
owns), `Persistord.Protection` (encrypts `[Protected]` columns at rest), and `Persistord.Testing`
(in-memory SQLite fixtures for tests). See [Packages](packages.md) for the full list.

## 2. Derive a context

`Persistord.Core` splits the base context in two: `DiscordDbContext` applies
only the snowflake conventions and maps nothing, while `DiscordGraphDbContext`
adds the guild/channel/user/member/role skeleton on top.

If your bot doesn't mirror Discord's own objects — it just persists messages,
say — derive `DiscordDbContext` and apply the module configurations you want in
`OnModelCreating`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

public sealed class MyBotContext : DiscordDbContext
{
    public MyBotContext(DbContextOptions<MyBotContext> options) : base(options) { }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // snowflake convention only
        modelBuilder.ApplyMessagesModule();   // omit if you don't persist messages
        modelBuilder.ApplyHistoryModule();    // requires ApplyMessagesModule()
    }
}
```

If your bot also mirrors guilds, channels, users, members or roles, derive
`DiscordGraphDbContext` instead — it exposes those `DbSet`s automatically:

```csharp
public sealed class MyBotContext : DiscordGraphDbContext
{
    public MyBotContext(DbContextOptions<MyBotContext> options) : base(options) { }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // core skeleton + snowflake convention
        modelBuilder.ApplyMessagesModule();   // omit if you don't persist messages
        modelBuilder.ApplyHistoryModule();    // requires ApplyMessagesModule()
    }
}
```

Core entities (`Guilds`, `Channels`, `Users`, `Members`, `Roles`) are already
exposed by `DiscordGraphDbContext` — you only declare the module `DbSet`s.

## 3. Register a provider

The consumer owns the provider choice. Install the provider package and register a context
factory:

# [PostgreSQL](#tab/postgresql)

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

```csharp
services.AddDbContextFactory<MyBotContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Bot")));
```

# [SQL Server](#tab/sqlserver)

```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

```csharp
services.AddDbContextFactory<MyBotContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Bot")));
```

# [SQLite](#tab/sqlite)

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

```csharp
services.AddDbContextFactory<MyBotContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Bot")));
```

---

Any EF Core 10 relational provider works. The snowflake `ulong → long` conversion is applied
automatically. [Providers](providers.md) covers the SQLite and PostgreSQL behaviour worth knowing
before you ship.

## 4. Write your first records

A bot is long-lived and concurrent; a `DbContext` is neither thread-safe nor meant
to live forever. Create one per unit of work via the factory and dispose it:

```csharp
await using var db = await factory.CreateDbContextAsync();

db.Messages.Add(new MessageEntity
{
    Id = message.Id,
    ChannelId = message.ChannelId,
    AuthorId = message.Author.Id,
    Content = message.Content,
});
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = message.Id,
    Content = message.Content,
    RecordedAt = DateTimeOffset.UtcNow,
    ChangeType = HistoryChangeType.Created,
});

await db.SaveChangesAsync();
```

## 5. Map from your Discord library

This step is optional. An adapter replaces the hand-written mapping above with one call per
entity. Install the one for the library your bot uses:

# [Discord.Net](#tab/discordnet)

```bash
dotnet add package Persistord.Adapters.DiscordNet
```

`message` is any Discord.Net `IMessage`, gateway or REST:

```csharp
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;

db.Messages.Add(message.ToMessageEntity());
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

# [DSharpPlus](#tab/dsharpplus)

```bash
dotnet add package Persistord.Adapters.DSharpPlus
```

`message` is a DSharpPlus `DiscordMessage`:

```csharp
using Persistord.Adapters.DSharpPlus;
using Persistord.History.Entities;

db.Messages.Add(message.ToMessageEntity());
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

# [NetCord](#tab/netcord)

```bash
dotnet add package Persistord.Adapters.NetCord
```

`message` is a NetCord `RestMessage`, or the gateway `Message` that derives from it:

```csharp
using Persistord.Adapters.NetCord;
using Persistord.History.Entities;

db.Messages.Add(message.ToMessageEntity());
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

---

[Choosing an Adapter](adapters.md) compares the three.

## 6. Run it

Create the schema from your context with a migration:

```bash
dotnet ef migrations add Initial
dotnet ef database update
```

`database update` ends with `Done.`. The database now holds a `Messages` and a `MessageHistory`
table, plus `Embed`, `EmbedField`, `AttachmentEntity` and `ReactionEntity` for the message's
children and EF Core's own `__EFMigrationsHistory`. With `DiscordGraphDbContext` it also holds
`Guilds`, `Channels`, `Users`, `Members` and `Roles`. Run the bot, and every message that reaches
the code from step 4 or step 5 adds one `Messages` row and one `Created` row in `MessageHistory`.

> [!TIP]
> If `dotnet ef` cannot find or create your context, see
> [Troubleshooting](troubleshooting.md#dotnet-ef-cant-find-the-dbcontext).

## See also

- [Migrations](migrations.md) — generate and apply EF Core migrations against your
  own context and provider.
- [Snowflake Conversion](snowflake-conversion.md) — how `ulong ↔ long` storage
  works and when to care.
- [Messages](messages.md) — embeds, attachments, reactions, and how they are stored.
- [DbContext Lifetime](dbcontext-lifetime.md) — patterns for `IDbContextFactory`
  in a concurrent bot.
- [Guild Lifecycle](guild-lifecycle.md) — upserting the guild root on
  `JoinedGuild` and soft-marking or purging it on `LeftGuild`.
- [Upsert](upsert.md) — the natural-key upsert behind that recipe and its own
  gotchas.
- [Providers](providers.md) — the SQLite and PostgreSQL caveats worth knowing
  before you pick one.
