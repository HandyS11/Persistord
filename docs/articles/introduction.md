---
description: What Persistord is, the promise it makes, and what it deliberately leaves to your bot.
---

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
- No general conflict-resolution engine. `Persistord.Core` does ship a narrow
  natural-key `UpsertAsync`/`UpsertIfChangedAsync` (create-or-update with
  lost-insert-race recovery) for rows the bot owns — that is deliberately not
  an engine, just the one write pattern the "persist what you choose" model
  needs.
- No caching layer.
- No diff-based history (full content snapshot per change in v1).

Mapping from Discord.Net / DSharpPlus / NetCord model types to Persistord entities
is the user's responsibility — though the optional `Persistord.Adapters.DiscordNet`,
`Persistord.Adapters.DSharpPlus` and `Persistord.Adapters.NetCord` packages provide
ready-made mappers for all three libraries.

## Packages

Persistord ships ten NuGet packages. The `Persistord` meta package bundles the library-neutral
mirror stack — `Persistord.Core`, `Persistord.Messages` and `Persistord.History` — and is the
recommended starting point. Three adapters map Discord.Net, DSharpPlus and NetCord types, and
`Persistord.Managed`, `Persistord.Protection` and `Persistord.Testing` are opt-in add-ons outside
the meta package. [Packages](packages.md) lists what each one adds, what it depends on, and how to
choose.

## See also

- [Getting Started](getting-started.md) — build a context and write your first records.
- [Packages](packages.md) — the ten packages, what each adds, and which ones you need.
- [Snowflake Conversion](snowflake-conversion.md) — how `ulong` snowflakes are stored.
