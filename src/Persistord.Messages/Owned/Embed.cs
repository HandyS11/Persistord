namespace Persistord.Messages.Owned;

/// <summary>An owned embed model. Has no key of its own; lives under a message.</summary>
public class Embed
{
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
