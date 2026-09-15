---
description: Why a Discord bot creates one short-lived DbContext per unit of work through IDbContextFactory, and what breaks when it does not.
---

# DbContext Lifetime

A Discord bot should create one short-lived `DbContext` per unit of work, because a context is neither thread-safe nor built to live as long as the bot.

A bot is long-lived and handles many concurrent gateway events, while a context's change tracker
accumulates tracked entities with every operation and grows unbounded if the context is never
disposed.

## The right pattern: IDbContextFactory

Register a context **factory** rather than a context instance:

```csharp
services.AddDbContextFactory<MyBotContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Bot")));
```

Then resolve the factory (typically via constructor injection) and create a
short-lived context per unit of work — one per gateway event, one per command, one
per background task iteration:

```csharp
// Assumes MyBotContext derives DiscordGraphDbContext, which is what exposes Guilds.
await using var db = await factory.CreateDbContextAsync();

db.Guilds.Add(new GuildEntity { Id = guildId, Name = name, OwnerId = ownerId });
await db.SaveChangesAsync();
// db is disposed here — change tracker is discarded
```

`await using` disposes the context (and its change tracker) as soon as the work is
done. Because each context is independent, concurrent gateway handlers can each hold
their own context without sharing state.

## What goes wrong with a long-lived context

> [!WARNING]
> A context that lives as long as the bot fails in three ways:
>
> - **Memory leak** — the change tracker accumulates every entity it has ever seen until the
>   context is disposed.
> - **Stale data** — a tracking query still runs against the database, but an entity the context
>   already tracks keeps its tracked values rather than the ones the query just returned, so your
>   handlers can see outdated state.
> - **Thread-safety violations** — `DbContext` is not thread-safe; concurrent access to a shared
>   instance causes unpredictable failures.

## See also

- [Getting Started](getting-started.md) — end-to-end setup including the factory
  registration and a complete first-write example.
- [Providers](providers.md#journal_modewal-and-busy_timeout) — many short-lived contexts against one SQLite file.
- [Upsert](upsert.md) — the upsert every short-lived context in the guides calls.
