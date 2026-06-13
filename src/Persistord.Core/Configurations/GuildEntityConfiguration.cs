using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="GuildEntity"/>.</summary>
public sealed class GuildEntityConfiguration : IEntityTypeConfiguration<GuildEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GuildEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Name).IsRequired();
    }
}
