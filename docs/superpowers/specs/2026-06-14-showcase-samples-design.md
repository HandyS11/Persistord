# Showcase Samples — Design

## Goal

Add focused, runnable sample projects that showcase the full set of Persistord
capabilities. The existing `samples/Persistord.Sample` stays as the minimal
quick-start; new samples each demonstrate one capability area in depth.

## Decisions

- **Structure:** multiple focused sample projects (one per capability area), not
  one expanded walkthrough.
- **Provider:** SQLite for every sample, consistent with the existing sample.
- **Discord.Net adapter sample:** uses NSubstitute fakes of the Discord.Net
  interfaces (mirrors how the adapter tests work) so it runs fully offline.
- **Scope:** samples only — plus `Persistord.slnx` and `README.md` updates. No
  changes to library source, no new providers, no live Discord connection.

## Projects

All under `samples/`, each a console app (`OutputType=Exe`, `IsPackable=false`,
`GenerateDocumentationFile=false`), matching the existing sample's csproj shape.

| Project | Showcases | References |
| --- | --- | --- |
| `Persistord.Sample.CoreGraph` | Guilds, Channels (category → text → thread hierarchy via `ParentId`), Users, Members (composite `(GuildId, UserId)` key), Roles; snowflake `ulong ↔ long` round-trip proof | Core + EFCore.Sqlite + EFCore.Design |
| `Persistord.Sample.Messages` | `MessageEntity` with owned `Embed`s (footer/author/fields), `AttachmentEntity`, `ReactionEntity` | Core, Messages + EFCore.Sqlite + EFCore.Design |
| `Persistord.Sample.History` | Soft-delete (`IsDeleted`/`DeletedAt`), query-filter behavior (default-hidden vs `IgnoreQueryFilters()`), append-only `MessageHistoryEntity` across Created → Edited → Deleted | Core, Messages, History + EFCore.Sqlite + EFCore.Design |
| `Persistord.Sample.DiscordNet` | `.To*Entity()` mappers driven by NSubstitute fakes of `IGuild`/`IUser`/`IRole`/`IGuildChannel`/`IMessage`, then persisted | Core, Messages, History, Adapters.DiscordNet + EFCore.Sqlite + EFCore.Design + NSubstitute + Discord.Net |

Each sample defines its own `DiscordDbContext`-derived context wiring only the
modules it needs (CoreGraph wires no module; Messages wires
`ApplyMessagesModule()`; History wires Messages + History; DiscordNet wires
Messages + History).

## Common pattern per sample

Each `Program.cs` is a self-contained, runnable, heavily-commented walkthrough of
its one capability that:

1. Builds `DbContextOptions` with `UseSqlite` against a per-sample file db.
2. Calls `EnsureDeletedAsync()` then `EnsureCreatedAsync()` so the sample is
   idempotent and needs no migrations.
3. Performs the writes for its capability.
4. Reads back and `Console.WriteLine`s clear evidence: counts, round-tripped ids,
   filtered-vs-unfiltered query results, history rows in recorded order.

The CoreGraph sample explicitly proves the snowflake conversion by inserting an id
near `ulong.MaxValue` and showing it round-trips faithfully — the library's
headline feature. Comments note that any EF Core 10 relational provider works;
SQLite is chosen only to keep samples self-contained.

## Integration

- Add all four projects to `Persistord.slnx`.
- Update the README "Documentation" section: replace the single-sample bullet with
  a list naming each sample and its one-line focus.

## Verification

These are samples, so no unit tests are added. Verification is that each project
builds and `dotnet run` completes printing the expected evidence. This is run
during implementation and the output confirmed before completion.

## Out of scope

- Providers other than SQLite.
- Live Discord gateway connection.
- Changes to library source under `src/`.
- New migrations (samples use `EnsureCreated`).
