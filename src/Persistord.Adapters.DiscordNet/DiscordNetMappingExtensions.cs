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
