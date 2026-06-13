namespace Persistord.Messages.Entities;

/// <summary>A reaction aggregate on a message, stored as a relational child.</summary>
public class ReactionEntity
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The owning message id (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>The emoji (unicode or <c>name:id</c> for custom emoji).</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>The reaction count.</summary>
    public int Count { get; set; }
}
