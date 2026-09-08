using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Internal;

namespace Persistord.Core;

/// <summary>Whole-database helpers for tests and local tooling.</summary>
public static class DatabaseMaintenanceExtensions
{
#pragma warning disable S3011 // Deliberate: reaches our own private generic helper to close it over each entity type.
    private static readonly MethodInfo DeleteAllMethod = typeof(DatabaseMaintenanceExtensions)
        .GetMethod(nameof(DeleteAllAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning restore S3011

    /// <summary>
    /// Deletes every row of every mapped entity type, dependents before principals, in one
    /// transaction. Plain <c>DELETE</c> on every relational provider: no <c>PRAGMA</c>, no
    /// <c>TRUNCATE</c>, no provider branch.
    /// </summary>
    /// <param name="context">The context whose model and connection to use.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// The deletes are executed as SQL and do not update the change tracker. Intended for test
    /// teardown and "reset my local database" tooling, not for production code paths.
    /// </remarks>
    public static async Task<int> ClearAllTablesAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = ModelDeleteOrder.Compute(context.Model, _ => true);

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
            deleted += await ((Task<int>)DeleteAllMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, [context, cancellationToken])!)
                .ConfigureAwait(false);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return deleted;
    }

    private static Task<int> DeleteAllAsync<TEntity>(DbContext context, CancellationToken cancellationToken)
        where TEntity : class =>
        context.Set<TEntity>().IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
}
