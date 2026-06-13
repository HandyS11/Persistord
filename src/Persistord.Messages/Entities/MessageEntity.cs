using Persistord.Messages.Owned;

namespace Persistord.Messages.Entities;

/// <summary>A persisted Discord message. Uses soft-delete so that history rows
/// keeping a foreign key to this row survive a delete.</summary>
public class MessageEntity
{
    /// <summary>The message snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The channel snowflake id the message belongs to.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The author snowflake id.</summary>
    public ulong AuthorId { get; set; }

    /// <summary>The message content.</summary>
    public string? Content { get; set; }

    /// <summary>When the message was last edited, if ever.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Whether the message has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the message was soft-deleted, if applicable.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Owned embeds.</summary>
    public List<Embed> Embeds { get; } = [];

    /// <summary>Relational attachments.</summary>
    public List<AttachmentEntity> Attachments { get; } = [];

    /// <summary>Relational reactions.</summary>
    public List<ReactionEntity> Reactions { get; } = [];
}
