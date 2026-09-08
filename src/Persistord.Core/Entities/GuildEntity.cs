namespace Persistord.Core.Entities;

/// <summary>
/// A Discord guild (server), and the tenant root every <c>IGuildScoped</c> row hangs off.
/// Everything but the id is optional: a bot that owns Discord resources rather than mirroring
/// them stores an id and the lifecycle stamps and nothing else.
/// </summary>
public class GuildEntity
{
    /// <summary>The guild snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The guild name, when the consumer mirrors it.</summary>
    public string? Name { get; set; }

    /// <summary>The snowflake id of the guild owner, when the consumer mirrors it.</summary>
    public ulong? OwnerId { get; set; }

    /// <summary>When the bot joined the guild, when the consumer records it.</summary>
    public DateTimeOffset? JoinedAt { get; set; }

    /// <summary>
    /// When the bot left, or was removed from, the guild. <c>null</c> means the bot is still in
    /// it. The consumer decides whether a non-null value is a soft mark for a retention policy or
    /// the trigger for <c>PurgeGuildAsync</c>.
    /// </summary>
    public DateTimeOffset? LeftAt { get; set; }
}
