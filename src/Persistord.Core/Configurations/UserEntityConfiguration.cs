using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="UserEntity"/>.</summary>
public sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Stryker disable once Statement : equivalent — EF's key discovery convention already keys on Id.
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        // Stryker disable once Statement : equivalent — a non-nullable reference type is already required by convention.
        builder.Property(u => u.Username).IsRequired();
    }
}
