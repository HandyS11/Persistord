# Getting Started

This guide walks through the minimum setup to start persisting Discord data with
Persistord: install the packages, derive a context, choose a provider, and write
your first records.

## Install

The easiest way is the meta package, which pulls in `Core`, `Messages`, and
`History` in one reference:

```bash
# Recommended: the full library-neutral stack in one package
dotnet add package Persistord
```

Or install modules individually:

```bash
dotnet add package Persistord.Core
dotnet add package Persistord.Messages      # optional: message persistence
dotnet add package Persistord.History       # optional: requires Messages
dotnet add package Persistord.Adapters.DiscordNet   # optional: Discord.Net mappers
```

## 1. Derive a context

Inherit `DiscordDbContext`, expose the module `DbSet`s you want, and apply the
module configurations in `OnModelCreating`:

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
        base.OnModelCreating(modelBuilder);   // core skeleton + snowflake convention
        modelBuilder.ApplyMessagesModule();   // omit if you don't persist messages
        modelBuilder.ApplyHistoryModule();    // requires ApplyMessagesModule()
    }
}
```

Core entities (`Guilds`, `Channels`, `Users`, `Members`, `Roles`) are already
exposed by the base context — you only declare the module `DbSet`s.

## 2. Choose a provider

The consumer owns the provider choice. Register a context factory:

```csharp
services.AddDbContextFactory<MyBotContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Bot")));
```

Any EF Core 10 relational provider works (PostgreSQL, SQL Server, SQLite, …). The
snowflake `ulong → long` conversion is applied automatically.

## 3. Use short-lived contexts

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

## Next steps

- [Migrations](migrations.md) — generate and apply EF Core migrations against your
  own context and provider.
- [Snowflake Conversion](snowflake-conversion.md) — how `ulong ↔ long` storage
  works and when to care.
- [Messages](messages.md) — soft-delete, embeds, attachments, reactions, and query
  filters.
- [DbContext Lifetime](dbcontext-lifetime.md) — patterns for `IDbContextFactory`
  in a concurrent bot.
