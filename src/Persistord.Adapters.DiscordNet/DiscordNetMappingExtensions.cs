using Discord;
using Persistord.Core.Entities;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using Embed = Persistord.Messages.Owned.Embed;
using EmbedAuthor = Persistord.Messages.Owned.EmbedAuthor;
using EmbedField = Persistord.Messages.Owned.EmbedField;
using EmbedFooter = Persistord.Messages.Owned.EmbedFooter;

namespace Persistord.Adapters.DiscordNet;

/// <summary>
/// Extension methods mapping Discord.Net interface types to Persistord entities.
/// Mappers copy data fields only; persistence-managed fields and EF-generated keys
/// are left at their defaults.
/// </summary>
public static class DiscordNetMappingExtensions
{
    /// <summary>Maps a Discord.Net guild channel to a <see cref="ChannelEntity"/>.</summary>
    /// <param name="channel">The guild channel to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public static ChannelEntity ToChannelEntity(this IGuildChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ChannelEntity
        {
            Id = channel.Id,
            GuildId = channel.GuildId,
            ParentId = (channel as INestedChannel)?.CategoryId,
            Type = MapChannelType(channel),
            Name = channel.Name ?? string.Empty,
        };
    }

    /// <summary>Maps a Discord.Net guild to a <see cref="GuildEntity"/>.</summary>
    /// <param name="guild">The guild to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guild"/> is <see langword="null"/>.</exception>
    public static GuildEntity ToGuildEntity(this IGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        return new GuildEntity
        {
            Id = guild.Id, Name = guild.Name ?? string.Empty, OwnerId = guild.OwnerId,
        };
    }

    /// <summary>Maps a Discord.Net user to a <see cref="UserEntity"/>.</summary>
    /// <param name="user">The user to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
    public static UserEntity ToUserEntity(this IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserEntity
        {
            Id = user.Id, Username = user.Username ?? string.Empty, GlobalName = user.GlobalName,
        };
    }

    /// <summary>Maps a Discord.Net role to a <see cref="RoleEntity"/>.</summary>
    /// <param name="role">The role to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="role"/> is <see langword="null"/>.</exception>
    public static RoleEntity ToRoleEntity(this IRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleEntity
        {
            Id = role.Id,
            GuildId = role.Guild.Id,
            Name = role.Name ?? string.Empty,
            Permissions = role.Permissions.RawValue,
            Color = unchecked((int)role.Color.RawValue),
        };
    }

    /// <summary>Maps a Discord.Net guild member to a <see cref="MemberEntity"/>.</summary>
    /// <param name="member">The guild user to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public static MemberEntity ToMemberEntity(this IGuildUser member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberEntity
        {
            GuildId = member.GuildId, UserId = member.Id, Nickname = member.Nickname, JoinedAt = member.JoinedAt,
        };
    }

    /// <summary>
    /// Maps a guild channel to a <see cref="ChannelType"/>.
    /// Arm order matters: in Discord.Net 3.x both <see cref="IThreadChannel"/> and <see cref="IVoiceChannel"/>
    /// extend <see cref="ITextChannel"/>, so more-derived types must be matched before <see cref="ITextChannel"/>
    /// to avoid silently falling into the Text arm.
    /// </summary>
    /// <param name="channel">The guild channel to map.</param>
    private static ChannelType MapChannelType(IGuildChannel channel) => channel switch
    {
        ICategoryChannel => ChannelType.Category,
        IThreadChannel => ChannelType.Thread,
        IVoiceChannel => ChannelType.Voice,
        ITextChannel => ChannelType.Text,
        _ => ChannelType.Text, // covers forum, stage, and future unknown channel types
    };

    /// <summary>
    /// Maps a Discord.Net message to a <see cref="MessageEntity"/>, including embeds,
    /// attachments, and reactions. Soft-delete state and EF-generated keys are left
    /// at their defaults; child foreign keys are filled by EF from the navigation
    /// collections on save.
    /// </summary>
    /// <param name="message">The Discord.Net message to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageEntity ToMessageEntity(this IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entity = new MessageEntity
        {
            Id = message.Id,
            ChannelId = message.Channel.Id,
            AuthorId = message.Author.Id,
            Content = message.Content,
            EditedAt = message.EditedTimestamp,
        };

        foreach (var attachment in message.Attachments)
        {
            entity.Attachments.Add(new AttachmentEntity
            {
                Id = attachment.Id,
                FileName = attachment.Filename ?? string.Empty,
                Url = attachment.Url ?? string.Empty,
            });
        }

        foreach (var (emote, metadata) in message.Reactions)
        {
            entity.Reactions.Add(new ReactionEntity
            {
                Emoji = MapEmote(emote), Count = metadata.ReactionCount,
            });
        }

        foreach (var embed in message.Embeds)
        {
            entity.Embeds.Add(MapEmbed(embed));
        }

        return entity;
    }

    /// <summary>
    /// Formats a reaction emote for storage: custom emotes become <c>name:id</c> (preserving
    /// the snowflake), while unicode emoji are stored as their raw <see cref="IEmote.Name"/>.
    /// </summary>
    /// <param name="emote">The Discord.Net emote to format.</param>
    private static string MapEmote(IEmote emote) => emote switch
    {
        Emote custom => $"{custom.Name}:{custom.Id}",
        _ => emote.Name ?? string.Empty,
    };

    /// <summary>
    /// Builds a <see cref="MessageHistoryEntity"/> snapshot of a message for the given
    /// change type. <see cref="MessageHistoryEntity.RecordedAt"/> is stamped with the
    /// current UTC time; the surrogate key is left for EF to assign.
    /// </summary>
    /// <param name="message">The Discord.Net message to snapshot.</param>
    /// <param name="changeType">The kind of change this snapshot records.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static MessageHistoryEntity ToHistoryEntity(this IMessage message, HistoryChangeType changeType)
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

    /// <summary>Maps a Discord.Net embed to a Persistord <see cref="Embed"/>.</summary>
    /// <param name="embed">The Discord.Net embed to map.</param>
    private static Embed MapEmbed(IEmbed embed)
    {
        var mapped = new Embed
        {
            Title = embed.Title,
            Description = embed.Description,
            Color = embed.Color.HasValue ? unchecked((int)embed.Color.Value.RawValue) : null,
        };

        if (embed.Footer.HasValue)
        {
            mapped.Footer = new EmbedFooter
            {
                Text = embed.Footer.Value.Text, IconUrl = embed.Footer.Value.IconUrl,
            };
        }

        if (embed.Author.HasValue)
        {
            mapped.Author = new EmbedAuthor
            {
                Name = embed.Author.Value.Name, Url = embed.Author.Value.Url,
            };
        }

        foreach (var field in embed.Fields)
        {
            mapped.Fields.Add(new EmbedField
            {
                Name = field.Name ?? string.Empty, Value = field.Value ?? string.Empty, Inline = field.Inline,
            });
        }

        return mapped;
    }
}
