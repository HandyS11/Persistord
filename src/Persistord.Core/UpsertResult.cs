namespace Persistord.Core;

/// <summary>The outcome of an upsert.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="Entity">The tracked row, freshly created or already present.</param>
/// <param name="Changed">
/// True when the call inserted or updated a row; false when the row was already up to date and
/// no write was issued — which also means no <c>UpdatedAt</c> stamp was applied.
/// </param>
public readonly record struct UpsertResult<TEntity>(TEntity Entity, bool Changed)
    where TEntity : class;
