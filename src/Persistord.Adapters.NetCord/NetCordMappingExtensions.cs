using NetCord;
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
