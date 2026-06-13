namespace Persistord.Core.Entities;

/// <summary>A Discord user (account-level, guild-independent).</summary>
public class UserEntity
{
    /// <summary>The user snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The user's username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The user's global display name, if set.</summary>
    public string? GlobalName { get; set; }
}
