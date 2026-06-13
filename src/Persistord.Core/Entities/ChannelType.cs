namespace Persistord.Core.Entities;

/// <summary>Discord channel kinds, used as the table-per-hierarchy discriminator.</summary>
public enum ChannelType
{
    /// <summary>A text channel.</summary>
    Text = 0,

    /// <summary>A voice channel.</summary>
    Voice = 2,

    /// <summary>A category that parents other channels.</summary>
    Category = 4,

    /// <summary>A thread.</summary>
    Thread = 11,
}
