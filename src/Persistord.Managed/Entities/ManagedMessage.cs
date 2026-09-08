namespace Persistord.Managed.Entities;

/// <summary>
/// A message the bot posted and edits in place — a dashboard, a per-item embed, a prompt whose
/// buttons must survive a restart.
/// </summary>
public sealed class ManagedMessage : ManagedResource
{
    /// <summary>The channel the message lives in.</summary>
    public ulong ChannelDiscordId { get; set; }

    /// <summary>
    /// A hash of the payload last rendered into this message, for render gating: hash the next
    /// payload, compare, and skip the edit when it matches. <c>null</c> until the consumer sets it.
    /// The module never computes it — the payload is the consumer's.
    /// </summary>
    public string? ContentHash { get; set; }
}
