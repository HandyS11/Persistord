using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Persistord.Testing;

/// <summary>
/// Assertions about the shape of an EF model, so a schema test is one line instead of a
/// seed-mutate-assert round trip against the database. Failures throw
/// <see cref="InvalidOperationException"/> with a message naming the entity and what was expected:
/// the package stays free of any test-framework dependency, so it works with xunit, NUnit and
/// MSTest alike.
/// </summary>
public static class ModelAssertions
{
    /// <summary>Asserts that the entity has a unique index over exactly these properties, in order.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="context">A context whose model to inspect.</param>
    /// <param name="propertyNames">The index's properties, in order.</param>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TEntity"/> is not part of the model, or has no unique index over
    /// <paramref name="propertyNames"/>.
    /// </exception>
    public static void AssertUniqueIndex<TEntity>(this DbContext context, params string[] propertyNames)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(propertyNames);

        var entityType = FindEntityType<TEntity>(context);
        var found = entityType.GetIndexes().Any(index =>
            index.IsUnique
            && index.Properties.Select(p => p.Name).SequenceEqual(propertyNames, StringComparer.Ordinal));

        if (!found)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} has no unique index over ({string.Join(", ", propertyNames)}). "
                + $"Indexes found: {DescribeIndexes(entityType)}.");
        }
    }

    /// <summary>
    /// Asserts that the child has a foreign key to the parent that deletes with
    /// <see cref="DeleteBehavior.Cascade"/> — the one-line replacement for an
    /// insert-parent, insert-child, delete-parent, assert-empty test.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <typeparam name="TParent">The principal entity type.</typeparam>
    /// <param name="context">A context whose model to inspect.</param>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TChild"/> is not part of the model, has no foreign key to
    /// <typeparamref name="TParent"/>, or that foreign key does not cascade.
    /// </exception>
    public static void AssertCascade<TChild, TParent>(this DbContext context)
        where TChild : class
        where TParent : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var child = FindEntityType<TChild>(context);
        var foreignKey = child.GetForeignKeys()
                             .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(TParent))
                         ?? throw new InvalidOperationException(
                             $"{typeof(TChild).Name} has no foreign key to {typeof(TParent).Name}.");

        if (foreignKey.DeleteBehavior != DeleteBehavior.Cascade)
        {
            throw new InvalidOperationException(
                $"{typeof(TChild).Name} -> {typeof(TParent).Name} deletes with "
                + $"{foreignKey.DeleteBehavior}, not {nameof(DeleteBehavior.Cascade)}.");
        }
    }

    /// <summary>
    /// Asserts that the entity's primary key is an unsigned 64-bit value the caller supplies: at
    /// least one <see cref="ulong"/> key property, every one of them
    /// <see cref="ValueGenerated.Never"/> and stored as a <see cref="long"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="context">A context whose model to inspect.</param>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TEntity"/> is not part of the model, has no primary key, its primary key
    /// has no <see cref="ulong"/> property, one such property is not <see cref="ValueGenerated.Never"/>,
    /// or one such property is not stored as a <see cref="long"/>.
    /// </exception>
    public static void AssertSnowflakeKey<TEntity>(this DbContext context)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var entityType = FindEntityType<TEntity>(context);
        var key = entityType.FindPrimaryKey()
                  ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no primary key.");

        var snowflakes = key.Properties
            .Where(p => p.ClrType == typeof(ulong) || p.ClrType == typeof(ulong?))
            .ToList();

        if (snowflakes.Count == 0)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name}'s primary key has no ulong property: "
                + $"({string.Join(", ", key.Properties.Select(p => p.Name))}).");
        }

        foreach (var property in snowflakes)
        {
            if (property.ValueGenerated != ValueGenerated.Never)
            {
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name}.{property.Name} is {property.ValueGenerated}, "
                    + $"not {nameof(ValueGenerated.Never)}: EF would treat it as an identity column.");
            }

            var providerType = property.GetValueConverter()?.ProviderClrType;
            if (providerType != typeof(long) && providerType != typeof(long?))
            {
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name}.{property.Name} is not stored as a long "
                    + $"(provider type: {providerType?.Name ?? "none"}). Is the context a DiscordDbContext?");
            }
        }
    }

    private static IEntityType FindEntityType<TEntity>(DbContext context) =>
        context.Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");

    private static string DescribeIndexes(IEntityType entityType)
    {
        var indexes = entityType.GetIndexes()
            .Select(i => $"{(i.IsUnique ? "unique " : string.Empty)}"
                         + $"({string.Join(", ", i.Properties.Select(p => p.Name))})")
            .ToList();

        return indexes.Count == 0 ? "none" : string.Join(", ", indexes);
    }
}
