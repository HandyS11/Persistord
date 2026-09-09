# Design — Fitting Persistord to a real bot (RustPlusBot findings)

**Date:** 2026-09-08
**Status:** Proposed
**Source:** Gap analysis of RustPlusBot (`../RustPlusBot`), the only production consumer of
`Persistord.Core 1.0.0-beta2`. Every count below was measured against that codebase on
`develop` at commit `5f8735d`.

## Context

Persistord v1 models Discord as a **mirror**: guilds, channels, users, members, roles,
messages, and message history, mapped from gateway objects. RustPlusBot has been in
production for three months on top of `DiscordDbContext` and uses exactly two things from
the library:

| Persistord piece | Used by the bot? | Evidence |
| --- | --- | --- |
| `DiscordDbContext` base class | Yes | `BotDbContext : DiscordDbContext` |
| Global `ulong → long` convention | Yes, on 44 `ulong` properties | Discord ids, Steam ids and Rust entity ids alike |
| `Guilds` / `Channels` / `Users` / `Members` / `Roles` tables | Never read or written | 0 rows in the live database next to 1 paired server and 12 provisioned channels |
| `Persistord.Messages`, `.History`, `.Adapters.DiscordNet` | Not referenced | Only `Persistord.Core` is in the bot's `Directory.Packages.props` |

The bot never mirrors Discord. It **owns** Discord resources — categories, channels,
anchored messages edited in place, webhooks — and needs to remember what it created,
keyed by its own stable keys and scoped to a guild. Everything a persistence layer should
provide for that is hand-written inside the bot, and the pieces Persistord does ship sit
unused in every migration.

This spec turns those findings into concrete library changes. It does not remove the
mirror model; it makes it opt-in and adds the missing "bot-owned state" layer beside it.

## Goals

- Make `Persistord.Core` useful to a bot that never mirrors Discord: conventions first,
  skeleton graph opt-in.
- Ship the primitives RustPlusBot re-implements today: bot-owned resources, guild as
  tenant root, upsert by natural key, timestamps, protected columns, test fixtures.
- Keep every addition provider-agnostic and Discord-library-agnostic, consistent with the
  v1 boundary.
- Stay backwards compatible for a consumer that already calls `base.OnModelCreating` and
  uses the five skeleton tables, or provide a one-line migration path.

## Non-Goals

- No gateway event handling, no automatic sync, no caching (unchanged from v1).
- No general-purpose ORM helpers beyond the narrow upsert described in §4.
- No diff-based history, no changes to the Messages/History data model.
- No new Discord-library adapter work; NetCord and DSharpPlus stay where the adapter
  spec left them.

## 1. Core: conventions vs skeleton graph

### 1.1 Problem

`DiscordDbContext.OnModelCreating` applies the five skeleton entities unconditionally.
A consumer who wants only the snowflake convention gets five empty tables in every
migration, in every model snapshot, and in every "clear all tables" routine. RustPlusBot
carries them through 17 migrations for nothing.

Two smaller defects sit next to it:

- `ConfigureConventions` does not call `base.ConfigureConventions`.
- The convention covers property values but not keys. A `ulong` primary key still looks
  like an identity column to EF, so RustPlusBot had to add `ValueGeneratedNever()` by hand
  on its one snowflake key (`GuildSettings.GuildId`). Persistord's own configurations do
  the same on all five skeleton keys.

### 1.2 Change

- Split the base context. `DiscordDbContext` keeps the conventions only. The skeleton
  graph moves behind an explicit `modelBuilder.ApplyCoreGraph()` (rename of today's
  `ApplyCoreConfiguration`, old name kept as `[Obsolete]` forwarder for one release).
  The five `DbSet` properties move to a `DiscordGraphDbContext : DiscordDbContext`
  subclass that applies the graph, so existing consumers switch base class and nothing
  else changes.
- Add a **snowflake-key convention**: every `ulong` / `ulong?` property that is part of a
  primary key gets `ValueGeneratedNever()`. Implemented as an `IModelFinalizingConvention`
  registered from `ConfigureConventions`, so it also covers consumer entities.
- Call `base.ConfigureConventions`.
- Document that the convention is "all unsigned 64-bit", not "Discord snowflakes". Steam64
  ids and any other `ulong` go through it. That is fine and should be stated.

### 1.3 Acceptance

- A context deriving from `DiscordDbContext` with no calls produces a model with **zero**
  entity types.
- A consumer `ulong` key on a consumer entity has `ValueGenerated == Never` without any
  fluent call.

## 2. Guild as tenant root

### 2.1 Problem

Every one of the bot's 20 domain entities carries a `ulong GuildId`. None has a foreign
key to `Guilds`. There is no query filter. Isolation is a `.Where(x => x.GuildId == guildId)`
predicate repeated about 74 times across 13 stores. Five entities keep `GuildId` as a
non-key passenger column that the upsert path silently overwrites. Purging a guild is a
hand-written sequence: tear down Discord resources, stop each server, delete each server,
then four `ExecuteDelete` calls for tables that have no cascade — untransacted. Nothing
handles the bot being kicked from a guild, so rows outlive the guild forever.

The existing `GuildEntity` cannot serve as that root: it requires `Name` and `OwnerId`,
which a bot that never mirrors Discord has no reason to store, and nothing points at it.

### 2.2 Change

Add to `Persistord.Core`:

- **`IGuildScoped`** marker interface: `ulong GuildId { get; }`.
- **Guild-scope convention** (model finalizing): for every `IGuildScoped` entity, add an
  index on `GuildId` if none contains it as first column, and optionally an FK to the
  guild root with `DeleteBehavior.Cascade`. The FK is opt-in via
  `modelBuilder.ApplyGuildRoot(cascade: true)` because it forces the consumer to insert
  the guild row before any scoped row.
- **Light guild root**: make `GuildEntity.Name` and `OwnerId` optional (`string?`,
  `ulong?`), and add lifecycle columns `JoinedAt` (`DateTimeOffset?`) and `LeftAt`
  (`DateTimeOffset?`). `LeftAt != null` means "the bot is no longer in this guild"; the
  consumer decides whether that is a soft mark or a hard purge trigger. This is a
  breaking change to `GuildEntity` and belongs in the next prerelease.
- **Purge helper**: `DbContext.PurgeGuildAsync(ulong guildId, CancellationToken)` that
  opens a transaction, deletes every `IGuildScoped` entity type in reverse FK-dependency
  order via `ExecuteDeleteAsync`, then deletes the guild row. Provider-agnostic: the order
  comes from the EF model, not from a pragma.
- **Optional global filter**: `ApplyGuildRoot(filterLeftGuilds: true)` adds
  `HasQueryFilter(g => g.LeftAt == null)` on the guild root only. No per-entity tenant
  filter — EF query filters cannot take a runtime guild id without a context field, and
  the bot resolves its scope from the interaction, not from ambient state.

### 2.3 Acceptance

- Deleting a `GuildEntity` cascades to every `IGuildScoped` row when `cascade: true`.
- `PurgeGuildAsync` on a guild with rows in five scoped tables and no FK to the root
  leaves zero rows and runs in one transaction.
- An `IGuildScoped` entity with a composite key that does not start with `GuildId` gets a
  separate `GuildId` index.

## 3. Bot-owned Discord resources (`Persistord.Managed`)

### 3.1 Problem

This is the single largest gap. RustPlusBot persists what it created in three tables and
five scattered columns, and fails to persist three further kinds of state:

| State | Where the bot keeps it | Cost |
| --- | --- | --- |
| Provisioned categories, channels, anchored messages | `ProvisionedCategory` / `ProvisionedChannel` / `ProvisionedMessage`, keyed `(GuildId, RustServerId?, Key)` | Three bespoke tables and five hand-written upserts |
| Per-device and per-alert message ids | A `MessageId` column on `SmartSwitch`, `SmartAlarm`, `SmartStorageMonitor`, `VendingNotification`, `VendingStockNotification` | Same "edit in place, repost if 404" logic re-implemented per feature |
| Chat-bridge webhooks | Not persisted; re-discovered by name via `GetWebhooksAsync` once per channel per boot | Renaming the webhook creates a silent duplicate; teardown leaves stale clients |
| `#map` image message | Not persisted; recovered by scanning the last 10 messages for bot posts with attachments | More than 10 messages between renders orphans an image forever |
| Pending pairing prompts | In-memory dictionary, message id included | Every restart orphans a live Accept/Dismiss message |

Anchored dashboards, per-item embeds and managed webhooks are what almost every Discord
bot persists. The planned Cameras subsystem will need one more of each.

### 3.2 Change

New package `Persistord.Managed`, depends on Core only.

Entities, all `IGuildScoped`:

```text
ManagedCategory { Id (long, surrogate), GuildId, Scope (string?, 64), Key (string, 64),
                  DiscordId (ulong), CreatedAt, UpdatedAt }
    unique (GuildId, Scope, Key)

ManagedChannel  { Id, GuildId, Scope?, Key, DiscordId, ParentDiscordId (ulong?),
                  CreatedAt, UpdatedAt }
    unique (GuildId, Scope, Key)

ManagedMessage  { Id, GuildId, Scope?, Key, ChannelDiscordId, DiscordId,
                  ContentHash (string?, 64), CreatedAt, UpdatedAt }
    unique (GuildId, Scope, Key); index (DiscordId)

ManagedWebhook  { Id, GuildId, Scope?, Key, ChannelDiscordId, DiscordId,
                  Token (string, protected — see §6), CreatedAt, UpdatedAt }
    unique (GuildId, Scope, Key)
```

Design points:

- `Scope` is an opaque consumer string, not a foreign key. RustPlusBot would store the
  Rust server id; a music bot would store nothing. Keeping it a string avoids coupling
  the module to any consumer table while still giving `DeleteScopeAsync(guildId, scope)`.
- **Uniqueness with a nullable `Scope`.** SQL treats NULLs as distinct in unique indexes,
  so `(GuildId, NULL, Key)` would accept duplicates on SQLite and PostgreSQL alike.
  Persist `Scope` as non-nullable with `""` for "global" and expose it as `string?` on the
  entity through a value converter. RustPlusBot currently relies on a per-guild lock to
  avoid exactly this duplicate.
- `ContentHash` exists for render gating: the consumer stores a hash of the last payload
  and skips the edit when unchanged. RustPlusBot keeps this in a process-local dictionary
  today and pays one redundant edit per message per restart.
- `ManagedMessage.DiscordId` is indexed so a `MessageDeleted` gateway event can be
  matched back to a record without loading the table.
- **Store helpers** as `DbContext` extensions, not a repository layer:
  `UpsertManagedAsync<T>(guildId, scope, key, discordId, ...)`,
  `FindManagedAsync<T>(guildId, scope, key)`, `DeleteScopeAsync(guildId, scope)`,
  `ListScopesAsync(guildId)`. They are thin wrappers over the §4 upsert primitive.

### 3.3 What the module deliberately does not do

- It never talks to Discord. "Fetch the message; if 404, repost and update the record" is
  the consumer's reconciler. The module only makes the record trivial to keep.
- It has no notion of channel type, permissions or name. Those are spec-driven in the
  consumer and re-derived every reconcile pass.

### 3.4 Acceptance

- Two `UpsertManagedAsync` calls with the same `(guildId, null, key)` produce one row.
- `DeleteScopeAsync` removes categories, channels, messages and webhooks of that scope
  and nothing from other scopes or the global scope.

## 4. Upsert by natural key

### 4.1 Problem

RustPlusBot implements find-then-add-or-update by hand in more than 20 store methods
across 11 classes. Six of them carry a byte-identical recovery block:

```csharp
catch (DbUpdateException)
{
    context.Entry(row).State = EntityState.Detached;
    row = await query.SingleOrDefaultAsync(ct) ?? throw;
    mutate(row);
    await context.SaveChangesAsync(ct);
}
```

Each has its own paraphrase of the same four-line comment explaining the insert race.
The v1 non-goal "no upsert engine or conflict resolution" was written for the mirror
model, where the consumer decides what to persist. For bot-owned state, the natural-key
upsert **is** the write path.

### 4.2 Change

Add to `Persistord.Core` one extension, no engine:

```csharp
public static Task<TEntity> UpsertAsync<TEntity>(
    this DbSet<TEntity> set,
    Expression<Func<TEntity, bool>> naturalKey,
    Func<TEntity> create,
    Action<TEntity> update,
    CancellationToken cancellationToken = default) where TEntity : class;
```

Semantics: `SingleOrDefaultAsync(naturalKey)`; if null, `create()` then `Add`; else
`update(existing)`; `SaveChangesAsync`; on `DbUpdateException` from the insert branch,
detach, re-read once, apply `update`, save again. Returns the tracked row. The `update`
delegate is also applied to a freshly created row so callers write the mutation once.

An `UpsertAsync` overload returning `bool changed` covers the "dirty-check short-circuit"
the bot performs in four places to avoid touching `UpdatedAt` on a no-op.

### 4.3 Acceptance

- Concurrent inserts of the same natural key from two contexts yield one row and no
  exception, verified with a `SaveChangesInterceptor` that injects the interfering write
  (RustPlusBot has this test shape already in its connection store tests).

## 5. Timestamps

### 5.1 Problem

The bot stamps `CreatedAt` / `UpdatedAt` by hand at 21 sites from an injected `IClock`.
Naming drifted across features: `CreatedAt`, `CreatedUtc`, `PostedUtc`, `LastSeenUtc`.

### 5.2 Change

- `ICreatedAt { DateTimeOffset CreatedAt { get; set; } }` and
  `IUpdatedAt { DateTimeOffset UpdatedAt { get; set; } }` in `Persistord.Core`.
- A `TimestampInterceptor : SaveChangesInterceptor` that stamps `Added` and `Modified`
  entries from a `TimeProvider` (defaults to `TimeProvider.System`; tests inject a fake).
  Registered by the consumer with `options.AddInterceptors(new TimestampInterceptor(tp))`
  or via `DiscordDbContext`'s constructor overload taking a `TimeProvider`.
- All `Persistord.Managed` entities implement both interfaces.
- Document the SQLite caveat once, in the provider article (§8): `DateTimeOffset` cannot
  be used in `ORDER BY` on SQLite. Recommend ordering by the surrogate key, which the
  managed entities have.

## 6. Protected columns (`Persistord.Protection`)

### 6.1 Problem

RustPlusBot stores two secrets (`PlayerCredential.ProtectedPlayerToken`,
`FcmRegistration.ProtectedFcmCredentials`) and encrypts them by calling
`protector.Protect(...)` at five write branches. A sixth branch that forgets the call
stores plaintext, and nothing in the model can notice. Decryption happens in feature
code, so stores return ciphertext strings typed as ordinary `string`.

Webhook tokens (§3) are the same shape and Discord-generic.

### 6.2 Change

New optional package `Persistord.Protection`, depends on Core and
`Microsoft.AspNetCore.DataProtection.Abstractions`:

- `Protected<string>` is too invasive; instead ship a **`ProtectedStringConverter :
  ValueConverter<string, string>`** built from an `IDataProtector`, plus a
  `[Protected]` attribute and a convention that applies the converter to every annotated
  `string` property. The protector purpose string is fixed per context
  (`"Persistord.Protection.v1"`) and documented.
- The converter is created per context instance, so the consumer supplies the
  `IDataProtectionProvider` through the `DbContext` constructor or `DbContextOptions`
  extension (`options.UseDataProtection(provider)`).
- `ManagedWebhook.Token` is annotated `[Protected]`; when the Protection package is not
  referenced, the annotation is inert and the token is stored as-is. The Managed README
  says so in bold.
- `Unprotect` failures surface as `CryptographicException` from materialization. The
  article covers key-ring loss and recommends `PersistKeysToFileSystem` next to the
  database — RustPlusBot currently leaves the key ring in the default per-user folder and
  would lose every credential with it.

## 7. Test fixtures (`Persistord.Testing`)

### 7.1 Problem

RustPlusBot builds an in-memory SQLite `BotDbContext` in two verbatim-duplicate helper
classes and inlines the shared-cache variant in about 20 test files; 24 files run the
full 17-migration chain per test. Persistord's own test tree duplicates
`UniqueModelCacheKeyFactory` three times and ships none of it.

### 7.2 Change

New package `Persistord.Testing`, depends on Core and `Microsoft.EntityFrameworkCore.Sqlite`:

- `SqliteTestDatabase` : `IAsyncDisposable`. Two factories:
  `Private()` for `DataSource=:memory:` with a held-open connection, and
  `Shared(name?)` for `Mode=Memory;Cache=Shared` so DI scopes can open their own
  connections against the same database. Both expose `Options<TContext>()` and a
  `CreateContext<TContext>(Func<DbContextOptions<TContext>, TContext>)`.
- `Schema` enum on both factories: `EnsureCreated` (fast) or `Migrate` (catches
  model/migration drift). Default `Migrate`, matching RustPlusBot's deliberate choice.
- `UniqueModelCacheKeyFactory` made public here so per-test configuration variants are
  visible to coverage and mutation tools.
- Model assertion helpers: `AssertUniqueIndex<T>(params string[] columns)`,
  `AssertCascade<TChild, TParent>()`, `AssertSnowflakeKey<T>()`. RustPlusBot has eight
  copy-pasted "insert server, insert child, remove server, assert empty" schema tests
  that reduce to `AssertCascade`.

## 8. Provider hygiene

### 8.1 Problem

The bot's stated PostgreSQL path runs through Persistord, yet it already carries
SQLite-only code that Persistord could absorb or at least document:

- `ClearAllAsync` issues `PRAGMA defer_foreign_keys = ON` and deletes tables in model
  order.
- Two stores sort on the client with the comment "SQLite cannot ORDER BY a
  DateTimeOffset column".
- No WAL, no busy timeout, no retry strategy, one explicit transaction in the whole
  codebase, and around 60 concurrent DI scopes hitting one SQLite file.

### 8.2 Change

- `DbContext.ClearAllTablesAsync()` in Core: topological order from the EF model's FK
  graph, one transaction, plain `DELETE`, no pragma. Works on every relational provider.
- A **Providers** article with one section per provider. SQLite: `DateTimeOffset`
  ordering, `journal_mode=WAL`, `busy_timeout`, why `EnableRetryOnFailure` does not
  exist for SQLite and what to do instead, and the NULLs-are-distinct unique-index rule.
  PostgreSQL: `NULLS NOT DISTINCT`, `timestamptz` and offsets, and the Npgsql
  snowflake round-trip already covered by the provider test.
- A **Guild lifecycle** recipe: what to do on `JoinedGuild` and `LeftGuild` with the §2
  root, and how to run `PurgeGuildAsync` behind a per-guild lock.
- An **Upsert** recipe for §4 and a **Managed resources** recipe for §3.

## 9. What stays as-is, and what the bot should not adopt

- `Persistord.Messages` and `Persistord.History` are unchanged. RustPlusBot should not
  adopt them: chat history is out of scope for the bot and every feed post is
  fire-and-forget.
- `Persistord.Adapters.DiscordNet` is unchanged. The bot never mirrors Discord objects.
- `UserEntity`, `MemberEntity`, `RoleEntity` stay in the opt-in graph. The bot's only
  plausible future use is resolving an owner id to a display name, which its specs defer.

## 10. Sequencing

| Step | Package | Breaking? | Unblocks |
| --- | --- | --- | --- |
| 1 | Core: conventions/graph split, snowflake-key convention, `base.ConfigureConventions` | Yes, base class rename for graph users | Everything below |
| 2 | Core: `UpsertAsync`, `ICreatedAt`/`IUpdatedAt`, `TimestampInterceptor` | No | §3 |
| 3 | Core: `IGuildScoped`, guild root changes, `PurgeGuildAsync`, `ClearAllTablesAsync` | Yes, `GuildEntity` shape | Bot guild purge and `LeftGuild` |
| 4 | `Persistord.Testing` | No | Everything can be tested in the bot without copy-paste |
| 5 | `Persistord.Managed` | No | Bot provisioning tables, webhook rows, Cameras |
| 6 | `Persistord.Protection` | No | Bot credential columns, webhook tokens |
| 7 | Docs: Providers, Guild lifecycle, Upsert, Managed recipes | No | — |

Steps 1 to 3 ship together as `1.0.0-beta3` because they are the breaking ones. Steps 4
to 7 can follow as `beta4` without touching consumers.

### RustPlusBot migration once this ships

- Switch `BotDbContext` to the conventions-only base; drop the five skeleton tables plus
  the bot's own two dead tables (`EventSubscriptions`, `PairedEntities`) in one migration.
- Mark the 20 domain entities `IGuildScoped`; add the guild root with cascade; replace
  `GuildPurgeService`'s manual sequence with `PurgeGuildAsync`; subscribe `LeftGuild`.
- Replace `ProvisionedCategory` / `ProvisionedChannel` / `ProvisionedMessage` and the
  five `MessageId` columns with `Persistord.Managed`; add webhook rows and stop
  re-discovering webhooks by name; persist the `#map` image id and pending pairing prompt
  ids.
- Replace the 20 hand-written upserts and 6 recovery blocks with `UpsertAsync`; delete
  the 21 timestamp stamps in favour of the interceptor; annotate the two secret columns
  `[Protected]` and remove the five `Protect()` calls.
- Replace the two fixture helpers and ~20 inlined blocks with `Persistord.Testing`.

## Open questions

- Should `GuildEntity` lifecycle be `LeftAt` (nullable timestamp) or a `GuildStatus`
  enum? Timestamp is proposed because it answers "when" for retention policies.
- Should `Persistord.Managed` store `ContentHash` at all, or leave render gating to the
  consumer? Proposed yes: it costs one column and removes a process-local cache.
- Does `UpsertAsync` belong in Core or in a `Persistord.Extensions` package? Proposed
  Core: `Persistord.Managed` depends on it and Core is the only mandatory package.

## Decisions to lock before planning

- Conventions-only base context with an opt-in graph.
- `IGuildScoped` + light guild root with cascade and lifecycle columns.
- `Persistord.Managed` as the home for bot-owned resources with an opaque `Scope`.
- Narrow `UpsertAsync`, not a conflict-resolution engine.
- `Persistord.Protection` and `Persistord.Testing` as optional packages.
