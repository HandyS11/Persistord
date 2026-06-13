using Discord;
using Persistord.Core.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;

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
            Id = guild.Id,
            Name = guild.Name ?? string.Empty,
            OwnerId = guild.OwnerId,
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
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            GlobalName = user.GlobalName,
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
            GuildId = member.GuildId,
            UserId = member.Id,
            Nickname = member.Nickname,
            JoinedAt = member.JoinedAt,
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
}
