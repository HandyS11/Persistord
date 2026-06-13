namespace Persistord.Core.Entities;

/// <summary>A Discord guild (server).</summary>
public class GuildEntity
{
    /// <summary>The guild snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The guild name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The snowflake id of the guild owner.</summary>
    public ulong OwnerId { get; set; }
}
