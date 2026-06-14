# Migrations

Persistord ships the **model**, not migrations. Migrations depend on the concrete
database provider you choose, so they must be generated against your own derived
context and your own project.

## Generate and apply

Use the standard EF Core CLI tools against your bot project:

```bash
dotnet ef migrations add Initial --project YourBot.csproj
dotnet ef database update --project YourBot.csproj
```

This produces a migration that covers the full model: the core skeleton entities
(`Guilds`, `Channels`, `Users`, `Members`, `Roles`) and any modules you applied
in `OnModelCreating` (`ApplyMessagesModule()`, `ApplyHistoryModule()`).

## Runnable example

See the [Samples](samples.md) article for a runnable end-to-end example (SQLite,
all three modules, generated migration).
