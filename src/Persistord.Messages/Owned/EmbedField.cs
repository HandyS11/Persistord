namespace Persistord.Messages.Owned;

/// <summary>A single name/value field of an embed. Owned collection element.</summary>
public class EmbedField
{
    /// <summary>Field name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Field value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether the field renders inline.</summary>
    public bool Inline { get; set; }
}
