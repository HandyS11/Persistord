using NetCord;
using NetCord.Rest;
using Persistord.Core.Entities;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using Embed = Persistord.Messages.Owned.Embed;
using EmbedAuthor = Persistord.Messages.Owned.EmbedAuthor;
using EmbedField = Persistord.Messages.Owned.EmbedField;
using EmbedFooter = Persistord.Messages.Owned.EmbedFooter;
using NetCordEmbed = global::NetCord.Embed;

namespace Persistord.Adapters.NetCord;

/// <summary>
/// Extension methods mapping NetCord model types to Persistord entities.
/// Mappers copy data fields only; persistence-managed fields and EF-generated keys
/// are left at their defaults.
/// </summary>
public static class NetCordMappingExtensions
{
    /// <summary>Maps a NetCord guild channel to a <see cref="ChannelEntity"/>.</summary>
    /// <remarks>
    /// <c>IGuildChannel</c> carries <c>Id</c>, <c>GuildId</c> and <c>Name</c> but neither
    /// a parent nor a channel kind, so both are recovered from the concrete class:
    /// <c>ParentId</c> lives on <c>TextGuildChannel</c> (and so on its voice, stage,
    /// announcement and thread subclasses) and separately on <c>ForumGuildChannel</c>;
    /// categories have no parent by definition, and <c>DirectoryGuildChannel</c> — which
    /// derives from <c>TextChannel</c>, not <c>TextGuildChannel</c> — exposes no
    /// <c>ParentId</c> either.
    /// </remarks>
    /// <param name="channel">The guild channel to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public static ChannelEntity ToChannelEntity(this IGuildChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ChannelEntity
        {
            Id = channel.Id,
            GuildId = channel.GuildId,
            ParentId = channel switch
            {
                TextGuildChannel text => text.ParentId,
                ForumGuildChannel forum => forum.ParentId,
                _ => null,
            },
            Type = MapChannelType(channel),
            Name = channel.Name,
        };
    }

    /// <summary>Maps a NetCord guild to a <see cref="GuildEntity"/>.</summary>
    /// <remarks>
    /// Binds <c>RestGuild</c> rather than the gateway <c>Guild</c> because the gateway
    /// type derives from it, so one method serves both. <c>JoinedAt</c> and
    /// <c>LeftAt</c> are intentionally not set: they track the bot's own membership
    /// lifecycle, which the consumer owns, not data carried on a Discord guild.
    /// </remarks>
    /// <param name="guild">The guild to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guild"/> is <see langword="null"/>.</exception>
    public static GuildEntity ToGuildEntity(this RestGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        return new GuildEntity
        {
            Id = guild.Id, Name = guild.Name, OwnerId = guild.OwnerId,
        };
    }

    /// <summary>Maps a NetCord user to a <see cref="UserEntity"/>.</summary>
    /// <param name="user">The user to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
    public static UserEntity ToUserEntity(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserEntity
        {
            Id = user.Id, Username = user.Username, GlobalName = user.GlobalName,
        };
    }

    /// <summary>Maps a NetCord guild member to a <see cref="MemberEntity"/>.</summary>
    /// <remarks>
    /// Binds <c>GuildUser</c>, not its <c>PartialGuildUser</c> base: the partial type
    /// deliberately omits <c>GuildId</c>, which is half of <see cref="MemberEntity"/>'s
    /// composite key.
    /// </remarks>
    /// <param name="member">The guild member to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public static MemberEntity ToMemberEntity(this GuildUser member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberEntity
        {
            GuildId = member.GuildId, UserId = member.Id, Nickname = member.Nickname, JoinedAt = member.JoinedAt,
        };
    }

    /// <summary>Maps a NetCord role to a <see cref="RoleEntity"/>.</summary>
    /// <remarks>
    /// <c>Permissions</c> is a <c>[Flags] enum : ulong</c>, so the cast is lossless.
    /// <c>Color</c> takes the role's primary colour; NetCord's <c>Color.RawValue</c> is
    /// already <c>int</c>, so unlike the Discord.Net adapter no unchecked cast is needed.
    /// Gradient and holographic roles' secondary and tertiary colours are dropped —
    /// <see cref="RoleEntity"/> stores a single colour.
    /// </remarks>
    /// <param name="role">The role to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="role"/> is <see langword="null"/>.</exception>
    public static RoleEntity ToRoleEntity(this Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleEntity
        {
            Id = role.Id,
            GuildId = role.GuildId,
            Name = role.Name,
            Permissions = (ulong)role.Permissions,
            Color = role.Colors.PrimaryColor.RawValue,
        };
    }

    /// <summary>
    /// Maps a NetCord message to a <see cref="MessageEntity"/>, including embeds,
    /// attachments, and reactions. Soft-delete state and EF-generated keys are left
    /// at their defaults; child foreign keys are filled by EF from the navigation
    /// collections on save.
    /// </summary>
    /// <remarks>
    /// Binds <c>RestMessage</c> so the gateway <c>Message</c>, which derives from it,
    /// maps through the same method.
    /// </remarks>
    /// <param name="message">The message to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageEntity ToMessageEntity(this RestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entity = new MessageEntity
        {
            Id = message.Id,
            ChannelId = message.ChannelId,
            AuthorId = message.Author.Id,
            Content = message.Content,
            EditedAt = message.EditedAt,
        };

        foreach (var attachment in message.Attachments)
        {
            entity.Attachments.Add(new AttachmentEntity
            {
                Id = attachment.Id, FileName = attachment.FileName, Url = attachment.Url,
            });
        }

        foreach (var reaction in message.Reactions)
        {
            entity.Reactions.Add(new ReactionEntity
            {
                Emoji = FormatEmoji(reaction.Emoji.Id, reaction.Emoji.Name), Count = reaction.Count,
            });
        }

        foreach (var embed in message.Embeds)
        {
            entity.Embeds.Add(MapEmbed(embed));
        }

        return entity;
    }

    /// <summary>
    /// Builds a <see cref="MessageHistoryEntity"/> snapshot of a message for the given
    /// change type. <see cref="MessageHistoryEntity.RecordedAt"/> is stamped with the
    /// current UTC time; the surrogate key is left for EF to assign.
    /// </summary>
    /// <param name="message">The message to snapshot.</param>
    /// <param name="changeType">The kind of change this snapshot records.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageHistoryEntity ToHistoryEntity(this RestMessage message, HistoryChangeType changeType)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageHistoryEntity
        {
            MessageId = message.Id,
            Content = message.Content,
            RecordedAt = DateTimeOffset.UtcNow,
            ChangeType = changeType,
        };
    }

    /// <summary>
    /// Maps a NetCord channel to a <see cref="ChannelType"/>.
    /// <para>
    /// NetCord carries no channel-type property: the kind is the class itself. Arm order
    /// matters, because <c>GuildThread</c>, <c>VoiceGuildChannel</c> and
    /// <c>StageGuildChannel</c> all derive from <c>TextGuildChannel</c> — the more
    /// derived arms must be matched first or they fall into the text fallback.
    /// </para>
    /// </summary>
    /// <param name="channel">The channel to classify.</param>
    private static ChannelType MapChannelType(IGuildChannel channel) => channel switch
    {
        GuildThread => ChannelType.Thread,
        IVoiceGuildChannel => ChannelType.Voice,
        CategoryGuildChannel => ChannelType.Category,
        // covers TextGuildChannel, AnnouncementGuildChannel, forum, media forum,
        // directory, and any channel class NetCord adds later
        _ => ChannelType.Text,
    };

    /// <summary>
    /// Formats a reaction emoji for storage: custom emoji become <c>name:id</c>
    /// (preserving the snowflake), while unicode emoji are stored as their raw name.
    /// Matches the Discord.Net adapter's format so stored values are portable.
    /// </summary>
    /// <param name="id">The custom emoji's snowflake, or <see langword="null"/> for a unicode emoji.</param>
    /// <param name="name">The emoji name, or the unicode character itself.</param>
    private static string FormatEmoji(ulong? id, string? name) =>
        id is { } emojiId ? $"{name}:{emojiId}" : name ?? string.Empty;

    /// <summary>Maps a NetCord embed to a Persistord <see cref="Embed"/>.</summary>
    /// <param name="embed">The NetCord embed to map.</param>
    private static Embed MapEmbed(NetCordEmbed embed)
    {
        var mapped = new Embed
        {
            Title = embed.Title, Description = embed.Description, Color = embed.Color?.RawValue,
        };

        if (embed.Footer is { } footer)
        {
            mapped.Footer = new EmbedFooter
            {
                Text = footer.Text, IconUrl = footer.IconUrl,
            };
        }

        if (embed.Author is { } author)
        {
            mapped.Author = new EmbedAuthor
            {
                Name = author.Name, Url = author.Url,
            };
        }

        foreach (var field in embed.Fields)
        {
            mapped.Fields.Add(new EmbedField
            {
                Name = field.Name, Value = field.Value, Inline = field.Inline,
            });
        }

        return mapped;
    }
}
