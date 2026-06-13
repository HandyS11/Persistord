namespace Persistord.Core.Entities;

/// <summary>A guild membership: a user within a guild. Keyed by the composite
/// <c>(GuildId, UserId)</c>.</summary>
public class MemberEntity
{
    /// <summary>The guild snowflake id (part of the composite key).</summary>
    public ulong GuildId { get; set; }

    /// <summary>The user snowflake id (part of the composite key).</summary>
    public ulong UserId { get; set; }

    /// <summary>The per-guild nickname, if set.</summary>
    public string? Nickname { get; set; }

    /// <summary>When the user joined the guild.</summary>
    public DateTimeOffset? JoinedAt { get; set; }
}
