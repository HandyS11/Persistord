using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Messages.Tests;

/// <summary>
/// Returns a unique key per call so EF never reuses a cached model. The same CLR context
/// type builds two different models depending on <c>filterDeleted</c>, and a single shared
/// model would also hide per-test coverage of the entity configurations and let
/// configuration mutants survive. Rebuilding every time sidesteps both problems.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ReplaceService.")]
internal sealed class UniqueModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => Guid.NewGuid();
}
