using Microsoft.EntityFrameworkCore;
using Persistord.Managed.Configurations;

namespace Persistord.Managed;

/// <summary>Model-building extensions that wire the Managed module.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the configurations for the four bot-owned resource types. Call from
    /// <c>OnModelCreating</c>. Declare the <c>DbSet</c>s you actually use — the module maps all four
    /// either way, so an unused one costs an empty table.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyManagedModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder
            .ApplyConfiguration(new ManagedCategoryConfiguration())
            .ApplyConfiguration(new ManagedChannelConfiguration())
            .ApplyConfiguration(new ManagedMessageConfiguration())
            .ApplyConfiguration(new ManagedWebhookConfiguration());
    }
}
