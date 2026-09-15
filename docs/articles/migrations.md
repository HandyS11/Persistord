---
description: Generate and apply EF Core migrations for your own derived context, because Persistord ships the model and never the migrations.
---

# Migrations

Persistord ships the model, not migrations, so you generate them against your own derived context and database provider.

Migrations depend on the concrete provider you choose, which only your project knows.

## Generate and apply

Use the standard EF Core CLI tools against your bot project:

```bash
dotnet ef migrations add Initial --project YourBot.csproj
dotnet ef database update --project YourBot.csproj
```

This produces a migration that covers the full model: any modules you applied
in `OnModelCreating` (`ApplyMessagesModule()`, `ApplyHistoryModule()`), and —
only if your context derives `DiscordGraphDbContext` rather than the
conventions-only `DiscordDbContext` — the core skeleton entities (`Guilds`,
`Channels`, `Users`, `Members`, `Roles`). See [Core Graph](core-graph.md) for
which base class maps which tables.

## Runnable example

See the [Samples](samples.md) article for a runnable end-to-end example (SQLite,
all three modules, generated migration).

## See also

- [Getting Started](getting-started.md#6-run-it) — the first migration, end to end.
- [Upgrading](upgrading.md) — changes that show up in your next migration.
- [Troubleshooting](troubleshooting.md#dotnet-ef-cant-find-the-dbcontext) — when `dotnet ef` cannot find your context.
- [Samples](samples.md) — a runnable project with a generated SQLite migration.
