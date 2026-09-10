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
