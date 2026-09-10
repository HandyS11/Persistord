using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace Persistord.Adapters.DSharpPlus.Tests;

/// <summary>
/// Builders for real DSharpPlus model instances.
/// <para>
/// Every DSharpPlus constructor and property setter is <c>internal</c>, and the
/// properties are non-virtual, so the entities can be neither constructed nor mocked
/// directly. They can, however, be deserialized: DSharpPlus annotates its internal
/// setters (and the internal backing fields behind its read-only collections) with
/// Newtonsoft <c>[JsonProperty]</c>, so Newtonsoft populates them when allowed to use
/// the internal parameterless constructor. The inputs below are therefore real Discord
/// gateway payloads.
/// </para>
/// </summary>
internal static class DSharpPlusFakes
{
    private static readonly JsonSerializerSettings Settings =
        new() { ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };

    internal static T Make<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;

    internal static DiscordChannel MakeChannel(
        int type = 0,
        ulong id = 111UL,
        ulong? guildId = 222UL,
        string name = "general",
        ulong? parentId = null)
    {
        var guildPart = guildId is { } gid ? $"\"guild_id\":\"{gid}\"," : string.Empty;
        var parentPart = parentId is { } pid ? $",\"parent_id\":\"{pid}\"" : string.Empty;

        return Make<DiscordChannel>(
            $"{{\"id\":\"{id}\",{guildPart}\"name\":\"{name}\",\"type\":{type}{parentPart}}}");
    }

    internal static DiscordGuild MakeGuild(
        ulong id = 100UL,
        string name = "a-guild",
        ulong ownerId = 101UL) =>
        Make<DiscordGuild>($"{{\"id\":\"{id}\",\"name\":\"{name}\",\"owner_id\":\"{ownerId}\"}}");

    internal static DiscordUser MakeUser(
        ulong id = 789UL,
        string username = "someone") =>
        Make<DiscordUser>($"{{\"id\":\"{id}\",\"username\":\"{username}\"}}");

    /// <summary>
    /// The real gateway nests the account under "user", but DSharpPlus reassembles that
    /// through an internal TransportMember path that standalone deserialization does not
    /// reach — a nested payload leaves Id at 0. A flat "id" populates it. "roles" must be
    /// present: the roles collection is lazily projected and the initialiser dereferences it.
    /// </summary>
    internal static DiscordMember MakeMember(
        ulong userId = 789UL,
        string? nickname = "nick",
        string joinedAt = "2026-02-03T04:05:06+00:00")
    {
        var nickPart = nickname is null ? "null" : $"\"{nickname}\"";

        return Make<DiscordMember>(
            $"{{\"id\":\"{userId}\",\"nick\":{nickPart},\"joined_at\":\"{joinedAt}\",\"roles\":[]}}");
    }

    internal static DiscordRole MakeRole(
        ulong id = 333UL,
        string name = "admin",
        long permissions = 8L,
        int color = 0xFF00FF) =>
        Make<DiscordRole>(
            $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"permissions\":{permissions},\"color\":{color}}}");
}
