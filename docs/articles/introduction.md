# Introduction

Persistord is a **provider-agnostic, Discord-library-agnostic** persistence layer
for Discord bots, built on EF Core 10.

## What it is

Persistord ships the **model only**: entities, conventions, and module
configurations. It never selects a database provider, never talks to Discord, and
never references a Discord client library. You compose it into your own bot and stay
in control of the provider and the gateway.

The library defines the data shape of common Discord entities and the plumbing —
a base `DbContext`, snowflake value conversions, and opt-in modules — while leaving
you in full control of _what_ gets persisted, _when_, and _with which_ database
provider.

## The promise

The guiding promise is **"persist whatever you choose to"** — not state replication
or gateway sync. Persistord gives you a clean, reliable relational model to write
into; it never mirrors live state automatically.

## Why it exists

Discord ids are 64-bit `ulong` snowflakes; relational providers store signed `long`.
Persistord handles the bit-faithful `ulong ↔ long` round-trip globally via a single
convention registered in `ConfigureConventions`, so you never annotate individual id
properties. See [snowflake conversion](snowflake-conversion.md) for details.

Beyond that, the library models the core Discord graph — guilds, channels, users,
members, roles, and messages — as plain POCOs, plus opt-in soft-delete and
append-only history.

## What it is not

The following are explicit non-goals in v1:

- No gateway event handling, no automatic sync, no reconnect backfill, no
  reconciliation.
- No upsert engine or conflict resolution (sidestepped by the "persist what you
  choose" model).
- No caching layer.
- No diff-based history (full content snapshot per change in v1).

Mapping from Discord.Net / DSharpPlus / NetCord model types to Persistord entities
is the user's responsibility — though the optional `Persistord.Adapters.DiscordNet`
package provides ready-made mappers if you use Discord.Net.

## Packages

Persistord is split into five NuGet packages:

**`Persistord`** — the convenience meta package. Installing it pulls in the full
library-neutral stack (`Core`, `Messages`, and `History`) in one reference. This is
the recommended starting point.

**`Persistord.Core`** — the foundation: snowflake conversion, the abstract
`DiscordDbContext` base class, and the core skeleton entities (`GuildEntity`,
`ChannelEntity`, `UserEntity`, `MemberEntity`, `RoleEntity`).

**`Persistord.Messages`** — the optional message-persistence module. Adds
`MessageEntity` (with soft-delete), owned embeds, and relational attachments and
reactions, wired in via `ApplyMessagesModule()`.

**`Persistord.History`** — the optional append-only history module. Adds
`MessageHistoryEntity` with a real foreign key to `MessageEntity`, wired in via
`ApplyHistoryModule()`. Depends on `Persistord.Messages`.

**`Persistord.Adapters.DiscordNet`** — an optional adapter that maps Discord.Net
interface types (`IGuild`, `IMessage`, etc.) to Persistord entities via `.To*Entity()`
extension methods. Install only if you use Discord.Net; the core packages never
reference a Discord client library.

The dependency graph is linear: `Core ← Messages ← History`. Each module is a
separate NuGet package you opt into explicitly.

To get started, see [Getting Started](getting-started.md).
