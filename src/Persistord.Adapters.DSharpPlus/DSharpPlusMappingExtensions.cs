using DSharpPlus.Entities;
using Persistord.Core.Entities;
using ChannelType = Persistord.Core.Entities.ChannelType;
using DSharpPlusChannelType = DSharpPlus.ChannelType;

namespace Persistord.Adapters.DSharpPlus;

/// <summary>
/// Extension methods mapping DSharpPlus model types to Persistord entities.
/// Mappers copy data fields only; persistence-managed fields and EF-generated keys
/// are left at their defaults.
/// </summary>
public static class DSharpPlusMappingExtensions
{
    /// <summary>Maps a DSharpPlus channel to a <see cref="ChannelEntity"/>.</summary>
    /// <remarks>
    /// Binds <c>DiscordChannel</c> rather than a thread-specific subclass: DSharpPlus
    /// carries the channel kind in the <c>Type</c> property, so <c>DiscordThreadChannel</c>
    /// maps through this same method. <c>DiscordChannel.GuildId</c> is nullable because DM
    /// and group-DM channels have none; <see cref="ChannelEntity.GuildId"/> is not, so an
    /// absent guild id becomes <c>0</c> rather than an exception.
    /// </remarks>
    /// <param name="channel">The channel to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public static ChannelEntity ToChannelEntity(this DiscordChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ChannelEntity
        {
            Id = channel.Id,
            GuildId = channel.GuildId ?? 0UL,
            ParentId = channel.ParentId,
            Type = MapChannelType(channel.Type),
            Name = channel.Name ?? string.Empty,
        };
    }

    /// <summary>Maps a DSharpPlus guild to a <see cref="GuildEntity"/>.</summary>
    /// <remarks>
    /// <c>JoinedAt</c> and <c>LeftAt</c> are intentionally not set: they track the bot's
    /// own membership lifecycle, which the consumer owns, not data carried on a Discord
    /// guild.
    /// </remarks>
    /// <param name="guild">The guild to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="guild"/> is <see langword="null"/>.</exception>
    public static GuildEntity ToGuildEntity(this DiscordGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        return new GuildEntity
        {
            Id = guild.Id, Name = guild.Name, OwnerId = guild.OwnerId,
        };
    }

    /// <summary>Maps a DSharpPlus user to a <see cref="UserEntity"/>.</summary>
    /// <remarks>
    /// <see cref="UserEntity.GlobalName"/> is left <see langword="null"/>: DSharpPlus
    /// 4.5.3 exposes no equivalent of Discord's <c>global_name</c> field.
    /// <c>DiscordMember.DisplayName</c> is not one — it is a cache-resolved
    /// nickname-or-username fallback, and it throws on a member the client has not
    /// cached.
    /// </remarks>
    /// <param name="user">The user to map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
    public static UserEntity ToUserEntity(this DiscordUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserEntity
        {
            Id = user.Id, Username = user.Username ?? string.Empty,
        };
    }

    /// <summary>Maps a DSharpPlus guild member to a <see cref="MemberEntity"/>.</summary>
    /// <remarks>
    /// The guild id is a parameter rather than a field read: <c>DiscordMember</c> keeps
    /// its guild id in an <c>internal</c> field, and its public <c>Guild</c> property
    /// resolves through the client's guild cache and throws for any member the cache
    /// does not hold. Since the id is half of <see cref="MemberEntity"/>'s composite key
    /// it cannot be omitted, so the caller — which always has it in hand from the event
    /// or command context — supplies it.
    /// </remarks>
    /// <param name="member">The guild member to map.</param>
    /// <param name="guildId">The snowflake id of the guild this membership belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <see langword="null"/>.</exception>
    public static MemberEntity ToMemberEntity(this DiscordMember member, ulong guildId)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberEntity
        {
            GuildId = guildId, UserId = member.Id, Nickname = member.Nickname, JoinedAt = member.JoinedAt,
        };
    }

    /// <summary>Maps a DSharpPlus role to a <see cref="RoleEntity"/>.</summary>
    /// <remarks>
    /// The guild id is a parameter for the same reason as on
    /// <see cref="ToMemberEntity"/>: <c>DiscordRole</c> exposes no guild id at all.
    /// <c>Permissions</c> is an <c>Int64</c>-backed <c>[Flags]</c> enum while
    /// <see cref="RoleEntity.Permissions"/> is <c>ulong</c>, so the conversion is
    /// unchecked — it reinterprets the bitfield rather than sign-extending it.
    /// </remarks>
    /// <param name="role">The role to map.</param>
    /// <param name="guildId">The snowflake id of the guild this role belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="role"/> is <see langword="null"/>.</exception>
    public static RoleEntity ToRoleEntity(this DiscordRole role, ulong guildId)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleEntity
        {
            Id = role.Id,
            GuildId = guildId,
            Name = role.Name ?? string.Empty,
            Permissions = unchecked((ulong)role.Permissions),
            Color = role.Color.Value,
        };
    }

    /// <summary>
    /// Maps a DSharpPlus channel kind to a Persistord <see cref="ChannelType"/>.
    /// <para>
    /// DSharpPlus has fourteen channel kinds and Persistord has four, so the
    /// translation collapses them. Kinds with no Persistord equivalent — DMs, group
    /// DMs, directories, forums, and DSharpPlus's own <c>Unknown</c> sentinel — fall
    /// back to <see cref="ChannelType.Text"/> rather than throwing, so a channel kind
    /// Discord adds later will not break a running bot.
    /// </para>
    /// </summary>
    /// <param name="type">The DSharpPlus channel kind.</param>
    private static ChannelType MapChannelType(DSharpPlusChannelType type) => type switch
    {
        DSharpPlusChannelType.Voice or DSharpPlusChannelType.Stage => ChannelType.Voice,
        DSharpPlusChannelType.Category => ChannelType.Category,
        DSharpPlusChannelType.NewsThread
            or DSharpPlusChannelType.PublicThread
            or DSharpPlusChannelType.PrivateThread => ChannelType.Thread,
        _ => ChannelType.Text,
    };
}
