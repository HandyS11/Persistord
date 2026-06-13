using Microsoft.EntityFrameworkCore;
using Persistord.History.Configurations;

namespace Persistord.History;

/// <summary>Model-building extensions that wire the History module.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the <c>MessageHistoryEntity</c> configuration. Requires the Messages module,
    /// because history carries a relational foreign key to <c>MessageEntity</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyHistoryModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new MessageHistoryEntityConfiguration());
        return modelBuilder;
    }
}
