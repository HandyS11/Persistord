using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.Managed.Entities;

namespace Persistord.Managed;

/// <summary>
/// Read and write helpers for bot-owned resource records. Thin wrappers over Core's
/// <c>UpsertAsync</c>, deliberately not a repository layer: the consumer keeps its own
/// <see cref="DbContext"/> and its own reconciler. Nothing here talks to Discord — "fetch the
/// message, repost it if it 404s, update the record" is the consumer's loop, and these helpers only
/// make the record side trivial.
/// </summary>
public static class ManagedStoreExtensions
{
    /// <summary>
    /// Creates or updates the record for one resource, keyed by
    /// <c>(guildId, scope, key)</c>.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="context">The context to read and write.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="scope">The consumer's partition, or <c>null</c> for guild-wide.</param>
    /// <param name="key">The consumer's stable key. Required.</param>
    /// <param name="discordId">The snowflake Discord returned for the resource.</param>
    /// <param name="configure">
    /// Sets the type-specific columns — a channel's parent, a message's channel and content hash, a
    /// webhook's token. Applied to created and existing rows alike.
    /// </param>
    /// <param name="cancellationToken">Cancels the read and the save.</param>
    /// <returns>The tracked record.</returns>
    public static Task<TResource> UpsertManagedAsync<TResource>(
        this DbContext context,
        ulong guildId,
        string? scope,
        string key,
        ulong discordId,
        Action<TResource>? configure = null,
        CancellationToken cancellationToken = default)
        where TResource : ManagedResource, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var normalized = ManagedScope.Normalize(scope);

        return context.Set<TResource>().UpsertAsync(
            r => r.GuildId == guildId && r.Scope == normalized && r.Key == key,
            () => new TResource
            {
                GuildId = guildId, Scope = normalized, Key = key
            },
            r =>
            {
                r.DiscordId = discordId;
                configure?.Invoke(r);
            },
            cancellationToken);
    }

    /// <summary>Reads one record by its natural key, or <c>null</c> when there is none.</summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="context">The context to read.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="scope">The consumer's partition, or <c>null</c> for guild-wide.</param>
    /// <param name="key">The consumer's stable key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or <c>null</c>.</returns>
    public static Task<TResource?> FindManagedAsync<TResource>(
        this DbContext context,
        ulong guildId,
        string? scope,
        string key,
        CancellationToken cancellationToken = default)
        where TResource : ManagedResource
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var normalized = ManagedScope.Normalize(scope);

        return context.Set<TResource>()
            .SingleOrDefaultAsync(
                r => r.GuildId == guildId && r.Scope == normalized && r.Key == key,
                cancellationToken);
    }

    /// <summary>
    /// Deletes every managed record of one scope, in one transaction. Use it when the thing the
    /// scope stood for is gone — a game server was unpaired, a playlist was deleted.
    /// </summary>
    /// <param name="context">The context to write.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="scope">The scope to erase, or <c>null</c> for the guild-wide one.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>The number of records deleted.</returns>
    /// <remarks>
    /// The deletes run as SQL and do not update the change tracker. This removes the bot's memory of
    /// the resources, not the resources themselves: tear those down in Discord first.
    /// </remarks>
    public static async Task<int> DeleteScopeAsync(
        this DbContext context,
        ulong guildId,
        string? scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var normalized = ManagedScope.Normalize(scope);

        // The implicit DisposeAsync await `await using` generates has no explicit await expression
        // to hang a ConfigureAwait(false) off; the BeginTransactionAsync call above already has one.
#pragma warning disable CA2007
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
#pragma warning restore CA2007

        var deleted = await DeleteScopeOfAsync<ManagedMessage>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);
        deleted += await DeleteScopeOfAsync<ManagedWebhook>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);
        deleted += await DeleteScopeOfAsync<ManagedChannel>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);
        deleted += await DeleteScopeOfAsync<ManagedCategory>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return deleted;
    }

    /// <summary>
    /// Lists the distinct scopes that still have records in the guild, sorted, excluding the
    /// guild-wide one. For a bot that scopes by game server, this answers "which servers do I still
    /// hold resources for?".
    /// </summary>
    /// <param name="context">The context to read.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The scopes, sorted ordinally.</returns>
    public static async Task<IReadOnlyList<string>> ListScopesAsync(
        this DbContext context,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scopes = new HashSet<string>(StringComparer.Ordinal);
        scopes.UnionWith(
            await ScopesOfAsync<ManagedCategory>(context, guildId, cancellationToken).ConfigureAwait(false));
        scopes.UnionWith(await ScopesOfAsync<ManagedChannel>(context, guildId, cancellationToken)
            .ConfigureAwait(false));
        scopes.UnionWith(await ScopesOfAsync<ManagedMessage>(context, guildId, cancellationToken)
            .ConfigureAwait(false));
        scopes.UnionWith(await ScopesOfAsync<ManagedWebhook>(context, guildId, cancellationToken)
            .ConfigureAwait(false));

        scopes.Remove(ManagedScope.Global);

        return [.. scopes.Order(StringComparer.Ordinal)];
    }

    private static Task<int> DeleteScopeOfAsync<TResource>(
        DbContext context,
        ulong guildId,
        string scope,
        CancellationToken cancellationToken)
        where TResource : ManagedResource =>
        context.Set<TResource>()
            .Where(r => r.GuildId == guildId && r.Scope == scope)
            .ExecuteDeleteAsync(cancellationToken);

    private static async Task<List<string>> ScopesOfAsync<TResource>(
        DbContext context,
        ulong guildId,
        CancellationToken cancellationToken)
        where TResource : ManagedResource =>
        await context.Set<TResource>()
            .Where(r => r.GuildId == guildId)
            .Select(r => r.Scope)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
