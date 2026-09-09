using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Persistord.Core.Internal;

namespace Persistord.Core;

/// <summary>Guild teardown helpers.</summary>
public static class GuildPurgeExtensions
{
#pragma warning disable S3011 // Deliberate: reaches our own private generic helper to close it over each entity type.
    private static readonly MethodInfo DeleteScopedMethod = typeof(GuildPurgeExtensions)
        .GetMethod(nameof(DeleteScopedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning restore S3011

    /// <summary>
    /// Deletes every <see cref="IGuildScoped"/> row belonging to <paramref name="guildId"/>, then
    /// the <see cref="GuildEntity"/> row itself when that type is part of the model. Among
    /// <see cref="IGuildScoped"/> types, dependents go before principals; everything runs in one
    /// transaction — an ambient transaction is joined rather than nested — and global query filters
    /// are ignored, so a guild already marked <see cref="GuildEntity.LeftAt"/> is still purged.
    /// </summary>
    /// <param name="context">The context whose model and connection to use.</param>
    /// <param name="guildId">The guild to erase.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// The deletes are executed as SQL and do not update the change tracker. Call this from a fresh
    /// context, or clear the tracker afterwards. The five skeleton entities
    /// (<see cref="GuildEntity"/>, <c>ChannelEntity</c>, <c>UserEntity</c>, <c>MemberEntity</c>,
    /// <c>RoleEntity</c>) do not implement <see cref="IGuildScoped"/> — that is a deliberate choice
    /// to keep an existing consumer's migrations from moving, and a consumer cannot retrofit the
    /// interface onto Persistord's own types — so <c>ApplyGuildRoot</c> wires no cascading foreign
    /// key for them and this method does not delete them. A consumer who mirrors Discord's graph
    /// and wants channels, users, members or roles purged with their guild must delete them itself
    /// before or after calling this method. The delete order only ever considers
    /// <see cref="IGuildScoped"/> types: a non-scoped entity is never part of it, so this method
    /// never deletes one. If a non-scoped entity has a restricting foreign key to a scoped row, this
    /// method can fail with a foreign-key violation when it tries to delete that scoped row (the
    /// transaction rolls back, so this is loud rather than silent). Give that non-scoped entity a
    /// cascading foreign key, or delete it yourself before calling this method. A reference cycle
    /// between two or more scoped entity types cannot be ordered either, so a restricting foreign
    /// key inside a cycle can fail the same way.
    /// </remarks>
    public static async Task<int> PurgeGuildAsync(
        this DbContext context,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = ModelDeleteOrder.Compute(
            context.Model,
            e => typeof(IGuildScoped).IsAssignableFrom(e.ClrType));

        // The implicit DisposeAsync await `await using` generates has no explicit await expression
        // to hang a ConfigureAwait(false) off; the BeginTransactionAsync call above already has one.
#pragma warning disable CA2007
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
#pragma warning restore CA2007

        var deleted = 0;
        foreach (var entityType in order)
        {
            deleted += await ((Task<int>)DeleteScopedMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, [context, guildId, cancellationToken])!)
                .ConfigureAwait(false);
        }

        if (context.Model.FindEntityType(typeof(GuildEntity)) is not null)
        {
            deleted += await context.Set<GuildEntity>()
                .IgnoreQueryFilters()
                .Where(g => g.Id == guildId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return deleted;
    }

    private static Task<int> DeleteScopedAsync<TEntity>(
        DbContext context,
        ulong guildId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Built by hand rather than written as `e => e.GuildId == guildId`: GuildId is declared on
        // IGuildScoped, and EF translates member access against the mapped CLR type.
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(
                Expression.Property(parameter, nameof(IGuildScoped.GuildId)),
                Expression.Constant(guildId)),
            parameter);

        return context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(predicate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
