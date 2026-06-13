namespace Persistord.Core.Entities;

/// <summary>A Discord role within a guild.</summary>
public class RoleEntity
{
    /// <summary>The role snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The owning guild snowflake id.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The role name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The raw permission bitfield.</summary>
    public ulong Permissions { get; set; }

    /// <summary>The role color (RGB integer).</summary>
    public int Color { get; set; }
}
