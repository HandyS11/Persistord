# Choosing an Adapter

Persistord's core packages (`Persistord.Core`, `Persistord.Messages`,
`Persistord.History`, and the opt-in `Persistord.Managed` / `Persistord.Protection` /
`Persistord.Testing`) never reference a Discord client library. Persistord entities
are plain EF Core models — nothing about them requires Discord.Net, DSharpPlus, or
NetCord.

An **adapter** package is a thin, opt-in layer of extension methods that map a
specific client library's model types onto Persistord entities. Because that mapping
is client-library-specific, it ships as a separate package per library rather than as
part of Core: your bot uses exactly one Discord client library, so you install at
most one adapter. Installing an adapter you don't use would pull in a Discord client
library dependency for no reason.

Persistord ships three:

- [Discord.Net Adapter](discord-net-adapter.md) — `Persistord.Adapters.DiscordNet`
- [DSharpPlus Adapter](dsharpplus-adapter.md) — `Persistord.Adapters.DSharpPlus`
- [NetCord Adapter](netcord-adapter.md) — `Persistord.Adapters.NetCord`

## Comparing the three

All three adapters expose the same seven mapper methods
(`ToGuildEntity`, `ToChannelEntity`, `ToUserEntity`, `ToMemberEntity`, `ToRoleEntity`,
`ToMessageEntity`, `ToHistoryEntity`) and map the same entities. **No adapter maps an
entity the others cannot** — there is no capability ranking here. What genuinely
differs is the shape of the client library each one binds to, and the consequences
that follow from it:

| | Discord.Net | DSharpPlus | NetCord |
| --- | --- | --- | --- |
| Binds to | Interfaces (`IGuild`, `IMessage`, …) | Concrete classes (`DiscordGuild`, …) | Base gateway/REST types (`RestGuild`, …) |
| Gateway *and* REST | Yes, via interfaces | Single class per entity | Yes, via base types |
| Extra parameters | none | `ToMemberEntity(guildId)`, `ToRoleEntity(guildId)` | none |
| Version range | `[3.20.1, 4.0.0)` | `[4.5.3, 5.0.0)` | `[1.0.0-beta.19, 2.0.0)` |
| Transitive weight | — | `Newtonsoft.Json` (netstandard2.0) | prerelease-only |

### The guild id parameter

The one signature difference across all three adapters: DSharpPlus's
`ToMemberEntity()` and `ToRoleEntity()` each take an extra `ulong guildId` argument
that the other two adapters don't need. `DiscordMember` and `DiscordRole` do not
expose their guild id anywhere on the public surface — it lives in an internal field,
and `DiscordMember.Guild` resolves through the client's guild cache and throws for an
uncached member. `MemberEntity.GuildId` is half of a composite primary key and
`RoleEntity.GuildId` is likewise required, so DSharpPlus's mappers ask the caller for
the id it already has at the point of mapping, rather than trying to recover it from
an object that can't reliably supply it. Discord.Net's `IGuildUser`/`IRole` and
NetCord's `GuildUser`/`Role` both carry their guild id directly, so their
`ToMemberEntity()`/`ToRoleEntity()` need no extra parameter.

### Channel types

Each client library expresses channel kind differently — Discord.Net and NetCord
through interfaces/classes, DSharpPlus through a `ChannelType` property — and each
has more channel kinds than Persistord's four-member `ChannelType`. Every adapter
collapses its own set down to `Text`, `Voice`, `Category`, or `Thread`, falling back
to `Text` for anything unrecognised. The collapsing tables are adapter-specific; see
each adapter's guide for its exact mapping.

### Dependency consequences

- **DSharpPlus** 4.5.x targets `netstandard2.0` and brings `Newtonsoft.Json` in
  transitively — DSharpPlus's dependency, not Persistord's.
- **NetCord** has no stable release, so this adapter carries a prerelease dependency.
  A local `dotnet pack` without an explicit version fails with NU5104; CD is
  unaffected because release tags are themselves prerelease.
- **Discord.Net** carries no unusual transitive weight beyond the library itself.

## When no adapter fits

The mappers in every adapter are ordinary extension methods over public data fields
— there is no hidden magic and no requirement to use one. If you use a Discord client
library Persistord doesn't ship an adapter for, or you need to shape the mapping
differently than an adapter does, hand-mapping is a fully supported path: construct
the Persistord entity yourself and set its properties from your client library's
objects, the same way each adapter's own mapper does internally.

## See also

- [Getting Started](getting-started.md) — installing Persistord and an adapter.
- [Introduction](introduction.md) — the package overview.
