using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="RoleEntity"/>.</summary>
public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Stryker disable once Statement : equivalent — EF's key discovery convention already keys on Id.
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        // Stryker disable once Statement : equivalent — a non-nullable reference type is already required by convention.
        builder.Property(r => r.Name).IsRequired();
        builder.HasIndex(r => r.GuildId);
    }
}
