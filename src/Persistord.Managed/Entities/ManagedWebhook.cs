using Persistord.Core.Abstractions;

namespace Persistord.Managed.Entities;

/// <summary>
/// A webhook the bot created. Persisting it is what stops the bot re-discovering webhooks by name
/// on every boot — a rename creates a silent duplicate.
/// </summary>
public sealed class ManagedWebhook : ManagedResource
{
    /// <summary>The channel the webhook posts to.</summary>
    public ulong ChannelDiscordId { get; set; }

    /// <summary>
    /// The webhook token. Annotated <see cref="ProtectedAttribute"/>, which is inert unless the
    /// consumer references <c>Persistord.Protection</c> and calls <c>ApplyProtection</c>:
    /// <b>without that, this token is stored in plaintext.</b>
    /// </summary>
    [Protected]
    public string Token { get; set; } = string.Empty;
}
