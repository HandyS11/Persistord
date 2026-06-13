namespace Persistord.Messages.Owned;

/// <summary>The author block of an embed. Owned; no identity of its own.</summary>
public class EmbedAuthor
{
    /// <summary>Author name.</summary>
    public string? Name { get; set; }

    /// <summary>Author URL.</summary>
    public string? Url { get; set; }
}
