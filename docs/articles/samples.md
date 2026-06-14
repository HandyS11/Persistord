# Samples

The `samples/` directory contains five runnable console projects that demonstrate
Persistord capabilities end-to-end. Every sample uses **SQLite** for zero-setup
— no database server required. Because Persistord is provider-agnostic, the same
model and context code runs on PostgreSQL, SQL Server, or any other EF Core 10
relational provider without changes.

## Running a sample

```bash
dotnet run --project samples/<SampleName>
```

Replace `<SampleName>` with one of the project names in the table below.

## Sample projects

| Project | What it shows |
| --- | --- |
| `Persistord.Sample` | Full end-to-end setup: Core, Messages, and History modules together with a generated SQLite migration. |
| `Persistord.Sample.CoreGraph` | Core graph entities — Guild, Channel (category → text → thread hierarchy), User, Member, Role — and the bit-faithful `ulong ↔ long` snowflake round-trip. See [Core Graph](core-graph.md). |
| `Persistord.Sample.Messages` | `MessageEntity` with a relational embed (Footer, Author, Fields), attachments, and reactions. See [Messages](messages.md). |
| `Persistord.Sample.History` | Append-only history: create, edit, and soft-delete a message while recording `HistoryChangeType.Created`, `HistoryChangeType.Edited`, and `HistoryChangeType.Deleted` rows. See [History](history.md). |
| `Persistord.Sample.DiscordNet` | Maps Discord.Net interface types (`IGuild`, `IMessage`, …) to Persistord entities using `.ToGuildEntity()`, `.ToMessageEntity()`, and `.ToHistoryEntity()`. See [Discord.Net Adapter](discord-net-adapter.md). |

## Source

All sample projects live in the repository's
[`samples/`](https://github.com/HandyS11/Persistord/tree/develop/samples)
directory.
