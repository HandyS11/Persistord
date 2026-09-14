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
        // Stryker disable once Statement : equivalent — EF's key discovery convention already keys on Id.
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
    }
}
