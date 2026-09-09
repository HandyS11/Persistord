using NetCord;
using NetCord.Rest;
using Persistord.Core.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;

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
    /// categories have no parent by definition.
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
}
