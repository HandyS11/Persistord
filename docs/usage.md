# Using Persistord

Persistord is a layered set of NuGet packages that model Discord data for EF Core:

| Package | Adds | Depends on |
| --- | --- | --- |
| `Persistord.Core` | snowflake conversion, base `DiscordDbContext`, core skeleton entities | — |
| `Persistord.Messages` | `MessageEntity` (soft-delete), embeds, attachments, reactions | Core |
| `Persistord.History` | append-only `MessageHistoryEntity` with a real FK to messages | Messages |

The library defines the **model only**. It never calls `UseSqlite`/`UseNpgsql`,
never talks to Discord, and never references a Discord client library. You compose
it into your own bot.

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

## 4. Migrations

Persistord ships the model, not migrations — you generate them against your own
context and provider:

```bash
dotnet ef migrations add Initial --project YourBot.csproj
dotnet ef database update --project YourBot.csproj
```

See [`samples/Persistord.Sample`](../samples/Persistord.Sample) for a runnable
end-to-end example (SQLite, all three modules, generated migration).

## Soft-delete & history

Deleting a message sets `IsDeleted`/`DeletedAt` rather than removing the row, and a
default query filter hides soft-deleted messages (`IgnoreQueryFilters()` to include
them, or `ApplyMessagesModule(filterDeleted: false)` to disable). Because the row
survives, `MessageHistoryEntity`'s foreign key to it — including the row logging the
deletion — stays valid. See the per-package READMEs for details.
