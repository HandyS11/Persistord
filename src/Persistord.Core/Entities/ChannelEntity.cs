namespace Persistord.Core.Entities;

/// <summary>A Discord channel. Self-references via <see cref="ParentId"/> to model
/// category &#8594; channel &#8594; thread hierarchies.</summary>
public class ChannelEntity
{
    /// <summary>The channel snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The owning guild snowflake id.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The parent channel snowflake id (category or parent channel), if any.</summary>
    public ulong? ParentId { get; set; }

    /// <summary>The channel kind.</summary>
    public ChannelType Type { get; set; }

    /// <summary>The channel name.</summary>
    public string Name { get; set; } = string.Empty;
}
