namespace Persistord.Core;

/// <summary>The outcome of an upsert.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="Entity">The tracked row, freshly created or already present.</param>
/// <param name="Changed">
/// True when the call issued a write; false when nothing was pending and no write was issued —
/// which also means no <c>UpdatedAt</c> stamp was applied. This reports whether the call wrote,
/// not whether the upserted row specifically changed: the dirty check runs across the whole
/// change tracker, so an unrelated pending change already in the context makes this true too,
/// because the save that follows flushes those alongside the upserted row.
/// </param>
public readonly record struct UpsertResult<TEntity>(TEntity Entity, bool Changed)
    where TEntity : class;
