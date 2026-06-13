namespace Persistord.Messages.Owned;

/// <summary>A single name/value field of an embed, stored as a relational child.</summary>
public class EmbedField
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>The owning embed surrogate id (foreign key).</summary>
    public long EmbedId { get; set; }

    /// <summary>Field name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Field value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether the field renders inline.</summary>
    public bool Inline { get; set; }
}
