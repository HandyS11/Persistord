namespace Persistord.Messages.Owned;

/// <summary>An embed stored as a relational child of a message.</summary>
public class Embed
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The owning message snowflake id (foreign key).</summary>
    public ulong MessageId { get; set; }

    /// <summary>Embed title.</summary>
    public string? Title { get; set; }

    /// <summary>Embed description.</summary>
    public string? Description { get; set; }

    /// <summary>Embed color (RGB integer).</summary>
    public int? Color { get; set; }

    /// <summary>Optional footer.</summary>
    public EmbedFooter? Footer { get; set; }

    /// <summary>Optional author block.</summary>
    public EmbedAuthor? Author { get; set; }

    /// <summary>Embed fields.</summary>
    public List<EmbedField> Fields { get; } = [];
}
