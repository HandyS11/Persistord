# Persistord

[![CI](https://github.com/HandyS11/Persistord/actions/workflows/CI.yml/badge.svg)](https://github.com/HandyS11/Persistord/actions/workflows/CI.yml)
[![NuGet](https://img.shields.io/nuget/v/Persistord.Core.svg?label=Persistord.Core)](https://www.nuget.org/packages/Persistord.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A **provider-agnostic, Discord-library-agnostic** persistence layer for Discord bots,
built on EF Core 10.

Persistord ships the **model only** — entities, conventions, and module
configurations. It never selects a database provider, never talks to Discord, and
never references a Discord client library. You compose it into your own bot and stay
in control of the provider and the gateway.

## Why

Discord ids are 64-bit `ulong` snowflakes; relational providers store signed `long`.
Persistord handles the bit-faithful `ulong ↔ long` round-trip globally, models the
core Discord graph (guilds, channels, users, members, roles, messages), and adds
opt-in soft-delete and append-only history — without coupling you to a specific
database or Discord library.

## Packages

| Package | Adds | Depends on |
| --- | --- | --- |
| [`Persistord.Core`](src/Persistord.Core) | snowflake conversion, base `DiscordDbContext`, core skeleton entities | — |
| [`Persistord.Messages`](src/Persistord.Messages) | `MessageEntity` (soft-delete), embeds, attachments, reactions | Core |
| [`Persistord.History`](src/Persistord.History) | append-only `MessageHistoryEntity` with a real FK to messages | Messages |
| [`Persistord.Adapters.DiscordNet`](src/Persistord.Adapters.DiscordNet) | `.To*Entity()` mappers from [Discord.Net](https://github.com/discord-net/Discord.Net) types | Core, Messages, History |

The core packages are independent of any Discord client library. Install the
DiscordNet adapter **only** if you use Discord.Net.

## Install

```bash
dotnet add package Persistord.Core
dotnet add package Persistord.Messages      # optional: message persistence
dotnet add package Persistord.History       # optional: requires Messages
dotnet add package Persistord.Adapters.DiscordNet   # optional: Discord.Net mappers
```

## Quick start

### 1. Derive a context

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

### 2. Choose a provider

The consumer owns the provider choice. Any EF Core 10 relational provider works
(PostgreSQL, SQL Server, SQLite, …); the snowflake conversion is applied
automatically.

```csharp
services.AddDbContextFactory<MyBotContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Bot")));
```

### 3. Use short-lived contexts

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

await db.SaveChangesAsync();
```

If you use Discord.Net, the adapter maps gateway/REST types for you:

```csharp
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;

db.Messages.Add(socketMessage.ToMessageEntity());   // embeds, attachments, reactions included
db.MessageHistory.Add(socketMessage.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

## Soft-delete & history

Deleting a message sets `IsDeleted` / `DeletedAt` rather than removing the row, and a
default query filter hides soft-deleted messages (`IgnoreQueryFilters()` to include
them, or `ApplyMessagesModule(filterDeleted: false)` to disable). Because the row
survives, `MessageHistoryEntity`'s foreign key to it — including the row logging the
deletion — stays valid.

## Documentation

- [Usage guide](docs/usage.md) — end-to-end walkthrough, migrations, provider notes.
- Per-package READMEs: [Core](src/Persistord.Core), [Messages](src/Persistord.Messages),
  [History](src/Persistord.History), [Adapters.DiscordNet](src/Persistord.Adapters.DiscordNet).
- [`samples/Persistord.Sample`](samples/Persistord.Sample) — runnable example
  (SQLite, all three modules, generated migration).

## Building

```bash
dotnet restore
dotnet build
dotnet test
```

Requires the .NET 10 SDK. Formatting is enforced with ReSharper
(`dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder"`).

## License

[MIT](LICENSE) © Clergue Valentin
