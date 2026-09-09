using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Core;

/// <summary>
/// Natural-key upsert for rows the bot owns. This is a narrow helper, not a conflict-resolution
/// engine: read by natural key, create or mutate, save, and recover exactly once from a lost
/// insert race.
/// </summary>
public static class UpsertExtensions
{
    /// <summary>
    /// Creates or updates the single row matching <paramref name="naturalKey"/> and returns it.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="set">The set to read and write.</param>
    /// <param name="naturalKey">
    /// A predicate that matches at most one row. Back it with a unique index: that index is what
    /// turns a concurrent duplicate insert into the <see cref="DbUpdateException"/> this method
    /// recovers from.
    /// </param>
    /// <param name="create">Builds the row when none matches. The natural-key values belong here.</param>
    /// <param name="update">
    /// Applies the mutation. Called for a freshly created row too, so the caller writes the
    /// mutation once.
    /// </param>
    /// <param name="cancellationToken">Cancels the read and the save.</param>
    /// <returns>The tracked row.</returns>
    public static async Task<TEntity> UpsertAsync<TEntity>(
        this DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> naturalKey,
        Func<TEntity> create,
        Action<TEntity> update,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var result = await set
            .UpsertIfChangedAsync(naturalKey, create, update, cancellationToken)
            .ConfigureAwait(false);
        return result.Entity;
    }

    /// <summary>
    /// Same as <see cref="UpsertAsync{TEntity}"/>, but also reports whether the call actually
    /// wrote. Use it to skip the work that follows a no-op write.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="set">The set to read and write.</param>
    /// <param name="naturalKey">A predicate that matches at most one row.</param>
    /// <param name="create">Builds the row when none matches.</param>
    /// <param name="update">Applies the mutation, to created and existing rows alike.</param>
    /// <param name="cancellationToken">Cancels the read and the save.</param>
    /// <returns>The tracked row and whether the call issued a write.</returns>
    /// <remarks>
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> flushes every pending change
    /// tracked by the context, not only the natural-key row this method upserted. The dirty check
    /// behind <see cref="UpsertResult{TEntity}.Changed"/> is <see cref="ChangeTracker.HasChanges"/>
    /// over the whole context, for the same reason: <c>context.Entry(row).State</c> only reflects
    /// that entry's own scalars, so a mutation that lands on an owned type, an EF complex type, or
    /// a collection navigation would leave the principal entry <see cref="EntityState.Unchanged"/>
    /// and the write would be silently dropped if the check stopped there. The trade-off is that an
    /// unrelated pending change already in the context also reports <c>Changed: true</c> — which is
    /// the honest reading, since <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> writes
    /// it regardless.
    /// </remarks>
    public static async Task<UpsertResult<TEntity>> UpsertIfChangedAsync<TEntity>(
        this DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> naturalKey,
        Func<TEntity> create,
        Action<TEntity> update,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(naturalKey);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(update);

        var context = set.GetService<ICurrentDbContext>().Context;

        var existing = await set.AsTracking()
            .SingleOrDefaultAsync(naturalKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return await ApplyAsync(context, existing, update, cancellationToken).ConfigureAwait(false);
        }

        var created = create();
        update(created);
#pragma warning disable VSTHRD103 // AddAsync is for generators needing DB access; our keys never do.
        set.Add(created);
#pragma warning restore VSTHRD103

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new UpsertResult<TEntity>(created, true);
        }
        catch (DbUpdateException)
        {
            // Lost the insert race: another writer created the same natural key between our read
            // and our insert. Drop our copy, re-read the winner once, and apply the mutation to
            // it. If there is still no winner the failure was not a race and must surface.
            context.Entry(created).State = EntityState.Detached;

            var winner = await set.AsTracking()
                .SingleOrDefaultAsync(naturalKey, cancellationToken)
                .ConfigureAwait(false);
            if (winner is null)
            {
                throw;
            }

            return await ApplyAsync(context, winner, update, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<UpsertResult<TEntity>> ApplyAsync<TEntity>(
        DbContext context,
        TEntity row,
        Action<TEntity> update,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        update(row);

        // The dirty check has to see the whole graph. context.Entry(row).State only reflects that
        // entry's own scalars: a mutation that lands on an owned type, a complex type or a
        // collection navigation leaves the principal entry Unchanged, and returning early on that
        // would drop the write. ChangeTracker.HasChanges() runs change detection across every
        // tracked entry, so it also catches an unrelated pending change already in the context —
        // which is honest, because the save below flushes those too.
        if (!context.ChangeTracker.HasChanges())
        {
            return new UpsertResult<TEntity>(row, false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new UpsertResult<TEntity>(row, true);
    }
}
