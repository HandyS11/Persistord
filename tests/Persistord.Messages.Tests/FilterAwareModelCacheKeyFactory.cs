using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Messages.Tests;

/// <summary>
/// The same CLR context type builds two different models depending on <c>filterDeleted</c>;
/// including that flag in the model cache key prevents EF from reusing the wrong model.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ReplaceService.")]
internal sealed class FilterAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is TestContext c
            ? (context.GetType(), c.FilterDeleted, designTime)
            : context.GetType();
}
