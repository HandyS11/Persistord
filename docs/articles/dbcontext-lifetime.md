# DbContext Lifetime

A Discord bot is long-lived and handles many concurrent gateway events. `DbContext`
is neither thread-safe nor designed to live for the bot's lifetime — its change
tracker accumulates tracked entities with every operation and grows unbounded if the
context is never disposed.

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
await using var db = await factory.CreateDbContextAsync();

db.Guilds.Add(new GuildEntity { Id = guildId, Name = name, OwnerId = ownerId });
await db.SaveChangesAsync();
// db is disposed here — change tracker is discarded
```

`await using` disposes the context (and its change tracker) as soon as the work is
done. Because each context is independent, concurrent gateway handlers can each hold
their own context without sharing state.

## What goes wrong with a long-lived context

- **Memory leak** — the change tracker accumulates every entity it has ever seen
  until the context is disposed.
- **Stale data** — EF Core returns cached entities from the first-level cache rather
  than re-querying, which can cause your handlers to see outdated state.
- **Thread-safety violations** — `DbContext` is not thread-safe; concurrent access
  to a shared instance causes unpredictable failures.

## See also

- [Getting Started](getting-started.md) — end-to-end setup including the factory
  registration and a complete first-write example.
