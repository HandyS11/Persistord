using Microsoft.EntityFrameworkCore.Metadata;

namespace Persistord.Core.Internal;

/// <summary>
/// Orders entity types so every dependent comes before the principal it points at, which is what
/// lets plain <c>DELETE</c> statements run without tripping a restricted foreign key. The order
/// comes from the EF model, so it holds on every relational provider — no pragma, no
/// provider-specific deferral. Self-references and reference cycles are skipped; the rest of the
/// order stays deterministic.
/// </summary>
internal static class ModelDeleteOrder
{
    /// <summary>Computes a dependents-before-principals delete order over the entity types the model knows.</summary>
    /// <param name="model">The model whose entity types and foreign keys to walk.</param>
    /// <param name="include">A filter selecting which entity types take part in the order.</param>
    /// <returns>The selected entity types, dependents before principals.</returns>
    public static IReadOnlyList<IEntityType> Compute(IModel model, Func<IEntityType, bool> include)
    {
        var candidates = model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindPrimaryKey() is not null && include(e))
            .ToList();

        var selected = new HashSet<IEntityType>(candidates);
        var visited = new HashSet<IEntityType>();
        var ordered = new List<IEntityType>(candidates.Count);

        foreach (var entityType in candidates)
        {
            Visit(entityType, selected, visited, ordered);
        }

        return ordered;
    }

    private static void Visit(
        IEntityType entityType,
        HashSet<IEntityType> selected,
        HashSet<IEntityType> visited,
        List<IEntityType> ordered)
    {
        if (!visited.Add(entityType))
        {
            return;
        }

        var dependents = entityType.GetReferencingForeignKeys()
            .Select(reference => reference.DeclaringEntityType)
            .Where(dependent => dependent != entityType && selected.Contains(dependent));

        foreach (var dependent in dependents)
        {
            Visit(dependent, selected, visited, ordered);
        }

        ordered.Add(entityType);
    }
}
