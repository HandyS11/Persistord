using Persistord.Core.Abstractions;

namespace Persistord.Managed.Entities;

/// <summary>
/// The shared shape of every Discord resource the bot creates and owns: a surrogate key, the owning
/// guild, an opaque consumer <see cref="Scope"/>, the consumer's stable <see cref="Key"/>, the
/// snowflake Discord handed back, and timestamps. Not an entity type itself — EF maps only the
/// concrete resources, each to its own table.
/// </summary>
public abstract class ManagedResource : IGuildScoped, ICreatedAt, IUpdatedAt
{
    /// <summary>
    /// Surrogate primary key. Managed resources carry one so callers have something stable and
    /// orderable to sort by: SQLite cannot <c>ORDER BY</c> a <see cref="DateTimeOffset"/> column.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// An opaque partition inside the guild, chosen by the consumer — a game-server id, a playlist
    /// id, whatever its resources hang off. Not a foreign key, so the module never couples to a
    /// consumer table. <see cref="ManagedScope.Global"/> means guild-wide.
    /// </summary>
    public string Scope { get; set; } = ManagedScope.Global;

    /// <summary>The consumer's stable key for this resource, unique within a guild and scope.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The snowflake of the Discord object the bot created.</summary>
    public ulong DiscordId { get; set; }

    /// <summary>When the record was first written.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The owning guild snowflake id.</summary>
    public ulong GuildId { get; set; }

    /// <summary>When the record was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
