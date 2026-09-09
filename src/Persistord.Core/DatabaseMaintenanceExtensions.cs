using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Internal;

namespace Persistord.Core;

/// <summary>Whole-database helpers for tests and local tooling.</summary>
public static class DatabaseMaintenanceExtensions
{
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
    /// <see cref="Internal.ModelDeleteOrder"/> deliberately does not order self-references, so before
    /// deleting anything, every self-referencing foreign key whose properties are all nullable (for
    /// example a category channel's <c>ParentId</c> pointing at another row of the same table) is set
    /// to <c>null</c> across the whole table. A self-referencing foreign key with a non-nullable
    /// property is left as-is and can still make the delete pass fail; model self-references as
    /// nullable if you need this method to clear them. A self-referencing foreign key backed by a
    /// shadow property (no CLR member) is skipped rather than attempted, because
    /// <see cref="Expression.Property(Expression, string)"/> throws <see cref="ArgumentException"/>
    /// for a property name with no corresponding CLR property; no entity shipped by Persistord has
    /// that shape today, but failing loudly there would be baffling. A reference cycle between two
    /// or more entity types cannot be ordered at all — no ordering satisfies every edge — so a
    /// restricting foreign key inside such a cycle can still fail the delete pass; break the cycle
    /// with a cascading or nullable foreign key if you need this method to clear it.
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

        await ClearNullableSelfReferencesAsync(context, order, cancellationToken).ConfigureAwait(false);

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

    private static async Task ClearNullableSelfReferencesAsync(
        DbContext context,
        IReadOnlyList<IEntityType> order,
        CancellationToken cancellationToken)
    {
        foreach (var entityType in order)
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (foreignKey.PrincipalEntityType != entityType
                    || foreignKey.Properties.Any(property => !property.IsNullable))
                {
                    continue;
                }

                foreach (var property in foreignKey.Properties)
                {
                    // Expression.Property(parameter, propertyName) resolves the name against the
                    // CLR type's public properties; a shadow property has no such member and would
                    // throw ArgumentException, so skip it instead of clearing it.
                    if (property.PropertyInfo is null)
                    {
                        continue;
                    }

                    await ((Task)ClearSelfReferenceMethod
                            .MakeGenericMethod(entityType.ClrType, property.ClrType)
                            .Invoke(null, [context, property.Name, cancellationToken])!)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static Task<int> DeleteAllAsync<TEntity>(DbContext context, CancellationToken cancellationToken)
        where TEntity : class =>
        context.Set<TEntity>().IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

    private static Task<int> ClearSelfReferenceAsync<TEntity, TProperty>(
        DbContext context,
        string propertyName,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var propertyAccess = Expression.Lambda<Func<TEntity, TProperty>>(
            Expression.Property(parameter, propertyName),
            parameter);

        return context.Set<TEntity>()
            .IgnoreQueryFilters()
            .ExecuteUpdateAsync(
                calls => calls.SetProperty(propertyAccess, default(TProperty)!),
                cancellationToken);
    }
#pragma warning disable S3011 // Deliberate: reaches our own private generic helpers to close them over each entity type.
    private static readonly MethodInfo DeleteAllMethod = typeof(DatabaseMaintenanceExtensions)
        .GetMethod(nameof(DeleteAllAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ClearSelfReferenceMethod = typeof(DatabaseMaintenanceExtensions)
        .GetMethod(nameof(ClearSelfReferenceAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning restore S3011
}
