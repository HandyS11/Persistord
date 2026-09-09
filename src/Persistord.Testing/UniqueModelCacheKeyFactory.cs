using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Testing;

/// <summary>
/// Returns a unique key per call so EF never reuses a cached model. Without it the model builds
/// once per context type for the whole test run, which hides per-test coverage of the entity
/// configurations, lets configuration mutants survive, and makes two contexts configured
/// differently silently share one model. <see cref="SqliteTestDatabase"/> installs it for you.
/// </summary>
public sealed class UniqueModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (context.GetType(), designTime, Guid.NewGuid());
    }
}
