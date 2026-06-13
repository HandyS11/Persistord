namespace Persistord.Messages.Entities;

/// <summary>A message attachment, stored as a relational child of a message.</summary>
public class AttachmentEntity
{
    /// <summary>The attachment snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The owning message id (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>The attachment file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The attachment URL.</summary>
    public string Url { get; set; } = string.Empty;
}
