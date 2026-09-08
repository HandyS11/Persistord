using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>EF Core configuration for <see cref="ManagedCategory"/>.</summary>
public sealed class ManagedCategoryConfiguration : IEntityTypeConfiguration<ManagedCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ManagedCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ManagedResourceConfiguration.ConfigureCommon(builder, "ManagedCategories");
    }
}
