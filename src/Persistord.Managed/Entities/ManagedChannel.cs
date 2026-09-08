namespace Persistord.Managed.Entities;

/// <summary>A channel the bot created.</summary>
public sealed class ManagedChannel : ManagedResource
{
    /// <summary>The category or parent channel it was created under, if any.</summary>
    public ulong? ParentDiscordId { get; set; }
}
