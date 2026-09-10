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

    /// <summary>
    /// Builds a message. <paramref name="attachments"/>, <paramref name="reactions"/> and
    /// <paramref name="embeds"/> take raw JSON array bodies so a test can omit a key
    /// entirely (pass <see langword="null"/>) and exercise the absent-collection path.
    /// <paramref name="authorId"/> passed as <see langword="null"/> omits the "author" key
    /// entirely, exercising the author-less payload path (e.g. a message-delete event).
    /// </summary>
    internal static DiscordMessage MakeMessage(
        ulong id = 555UL,
        ulong channelId = 111UL,
        ulong? authorId = 789UL,
        string? content = "hello",
        string? editedAt = null,
        string? attachments = null,
        string? reactions = null,
        string? embeds = null)
    {
        var parts = new List<string>
        {
            $"\"id\":\"{id}\"",
            $"\"channel_id\":\"{channelId}\"",
        };

        if (authorId is { } aid) { parts.Add($"\"author\":{{\"id\":\"{aid}\",\"username\":\"author\"}}"); }
        if (content is not null) { parts.Add($"\"content\":\"{content}\""); }
        if (editedAt is not null) { parts.Add($"\"edited_timestamp\":\"{editedAt}\""); }
        if (attachments is not null) { parts.Add($"\"attachments\":{attachments}"); }
        if (reactions is not null) { parts.Add($"\"reactions\":{reactions}"); }
        if (embeds is not null) { parts.Add($"\"embeds\":{embeds}"); }

        return Make<DiscordMessage>($"{{{string.Join(",", parts)}}}");
    }

    internal const string OneAttachment =
        """[{"id":"900","filename":"shot.png","url":"https://cdn.example/shot.png"}]""";

    internal const string UnicodeAndCustomReactions =
        """[{"count":3,"emoji":{"id":null,"name":"thumbsup"}},{"count":1,"emoji":{"id":"12345","name":"blob"}}]""";

    internal const string FullEmbed =
        """
        [{"title":"a title","description":"a description","color":1122867,
          "footer":{"text":"a footer","icon_url":"https://cdn.example/i.png"},
          "author":{"name":"an author","url":"https://example/a"},
          "fields":[{"name":"fname","value":"fvalue","inline":true}]}]
        """;

    internal const string BareEmbed = """[{"title":"a title"}]""";
}
