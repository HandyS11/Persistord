using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Core.Tests;

/// <summary>
/// Returns a unique key per call so EF never reuses a cached model. Without this the
/// model builds once for the whole test run, which both hides per-test coverage of the
/// entity configurations and lets configuration mutants survive.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ReplaceService.")]
internal sealed class UniqueModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), designTime, Guid.NewGuid());
}
