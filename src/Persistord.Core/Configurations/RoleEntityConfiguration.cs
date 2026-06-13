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
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).IsRequired();
        builder.HasIndex(r => r.GuildId);
    }
}
