namespace Persistord.History.Entities;

/// <summary>An append-only snapshot of a message at a point in time. Carries a real
/// foreign key to <c>MessageEntity</c>; one message maps to many history rows.</summary>
public class MessageHistoryEntity
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The message snowflake id this row belongs to (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>Full snapshot of the message content at this point.</summary>
    public string? Content { get; set; }

    /// <summary>When this snapshot was recorded.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>What kind of change this row represents.</summary>
    public HistoryChangeType ChangeType { get; set; }
}
