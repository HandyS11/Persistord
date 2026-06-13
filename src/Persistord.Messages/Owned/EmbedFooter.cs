namespace Persistord.Messages.Owned;

/// <summary>The footer of an embed. Owned; no identity of its own.</summary>
public class EmbedFooter
{
    /// <summary>Footer text.</summary>
    public string? Text { get; set; }

    /// <summary>Footer icon URL.</summary>
    public string? IconUrl { get; set; }
}
