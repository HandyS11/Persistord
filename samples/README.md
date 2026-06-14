# Persistord Samples

Runnable, focused walkthroughs. Every sample uses **SQLite** for a zero-setup run;
the library itself is provider-agnostic, so the same code works on PostgreSQL, SQL
Server, etc.

Run any sample with:

```bash
dotnet run --project samples/<SampleName>
```

| Sample | Shows |
| --- | --- |
| [`Persistord.Sample`](Persistord.Sample) | Minimal quick-start — all three modules with a generated migration. |
| [`Persistord.Sample.CoreGraph`](Persistord.Sample.CoreGraph) | Guilds, channels, users, members, roles, and the snowflake `ulong ↔ long` round-trip. |
| [`Persistord.Sample.Messages`](Persistord.Sample.Messages) | Messages with embeds, attachments, and reactions. |
| [`Persistord.Sample.History`](Persistord.Sample.History) | Soft-delete, query filters, and append-only history. |
| [`Persistord.Sample.DiscordNet`](Persistord.Sample.DiscordNet) | `.To*Entity()` mappers driven by faked Discord.Net types. |
