using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;

namespace Persistord.Adapters.NetCord.Tests;

/// <summary>
/// Builders for real NetCord model instances. NetCord exposes public constructors over
/// its JSON models, so these tests use genuine objects rather than mocks.
/// </summary>
internal static class NetCordFakes
{
    private static readonly RestClient Client = new();

    internal static IGuildChannel MakeChannel(
        ChannelType type,
        ulong id = 111UL,
        ulong guildId = 222UL,
        string name = "general",
        ulong? parentId = null) =>
        (IGuildChannel)Channel.CreateFromJson(
            new JsonChannel
            {
                Id = id,
                GuildId = guildId,
                Name = name,
                Type = type,
                ParentId = parentId
            },
            Client);

    internal static RestGuild MakeGuild(
        ulong id = 100UL,
        string name = "a-guild",
        ulong ownerId = 101UL) =>
        new(new JsonGuild
        {
            Id = id, Name = name, OwnerId = ownerId
        }, Client);

    internal static User MakeUser(
        ulong id = 789UL,
        string username = "someone",
        string? globalName = "Someone") =>
        new(new JsonUser
        {
            Id = id, Username = username, GlobalName = globalName
        }, Client);

    internal static GuildUser MakeMember(
        ulong guildId = 222UL,
        ulong userId = 789UL,
        string? nickname = "nick",
        DateTimeOffset? joinedAt = null) =>
        new(
            new JsonGuildUser
            {
                User = new JsonUser
                {
                    Id = userId, Username = "someone"
                },
                Nickname = nickname,
                JoinedAt = joinedAt,
            },
            guildId,
            Client);

    internal static Role MakeRole(
        ulong id = 333UL,
        ulong guildId = 222UL,
        string name = "admin",
        Permissions permissions = Permissions.Administrator,
        int color = 0xFF00FF) =>
        new(
            new JsonRole
            {
                Id = id,
                Name = name,
                Permissions = permissions,
                Colors = new JsonRoleColors
                {
                    PrimaryColor = new Color(color)
                },
            },
            guildId,
            Client);

    internal static JsonAttachment MakeAttachment(
        ulong id = 900UL,
        string fileName = "shot.png",
        string url = "https://cdn.example/shot.png") =>
        new()
        {
            Id = id, FileName = fileName, Url = url
        };

    internal static JsonMessageReaction MakeReaction(
        int count = 3,
        ulong? emojiId = null,
        string? emojiName = "\U0001F44D") =>
        new()
        {
            Count = count,
            Emoji = new JsonEmoji
            {
                Id = emojiId, Name = emojiName
            }
        };

    internal static JsonEmbed MakeEmbed(
        string? title = "a title",
        string? description = "a description",
        int? color = 0x112233,
        string? footerText = "a footer",
        string? authorName = "an author") =>
        new()
        {
            Title = title,
            Description = description,
            Color = color is { } raw ? new Color(raw) : null,
            Footer = footerText is null
                ? null
                : new JsonEmbedFooter
                {
                    Text = footerText, IconUrl = "https://cdn.example/i.png"
                },
            Author = authorName is null
                ? null
                : new JsonEmbedAuthor
                {
                    Name = authorName, Url = "https://example/a"
                },
            Fields =
            [
                new JsonEmbedField
                {
                    Name = "fname", Value = "fvalue", Inline = true
                }
            ],
        };

    internal static RestMessage MakeMessage(
        ulong id = 555UL,
        ulong channelId = 111UL,
        ulong authorId = 789UL,
        string content = "hello",
        DateTimeOffset? editedAt = null,
        JsonAttachment[]? attachments = null,
        JsonMessageReaction[]? reactions = null,
        JsonEmbed[]? embeds = null) =>
        new(
            new JsonMessage
            {
                Id = id,
                ChannelId = channelId,
                Content = content,
                Author = new JsonUser
                {
                    Id = authorId, Username = "author"
                },
                EditedAt = editedAt,
                Attachments = attachments ?? [],
                Reactions = reactions ?? [],
                Embeds = embeds ?? [],

                // RestMessage's constructor LINQ-projects each of these and throws
                // ArgumentNullException if any is left null. Do not remove.
                MentionedUsers = [],
                MentionedRoleIds = [],
                MentionedChannels = [],
                Components = [],
                Stickers = [],
                MessageSnapshots = [],
            },
            Client);
}
