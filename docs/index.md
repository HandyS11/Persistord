# Persistord

A **provider-agnostic, Discord-library-agnostic** persistence layer for Discord bots,
built on EF Core 10. Persistord ships the **model only** — entities, conventions, and
module configurations. It never selects a database provider, never talks to Discord,
and never references a Discord client library.

## Where to start

- **[Introduction](articles/introduction.md)** — what Persistord is and the design philosophy.
- **[Getting Started](articles/getting-started.md)** — install, derive a context, pick a provider, save data.
- **[Migrations](articles/migrations.md)** — generate migrations against your own context.

## Guides

Snowflake conversion, the core graph, messages, soft-delete, history, `DbContext`
lifetime, and the Discord.Net adapter — see the **Articles** tab.

## Reference

The **API** tab is generated from the XML doc comments in the packages.
