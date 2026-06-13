using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="ChannelEntity"/>, including the
/// self-referencing parent relationship.</summary>
public sealed class ChannelEntityConfiguration : IEntityTypeConfiguration<ChannelEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChannelEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired();
        builder.HasIndex(c => c.GuildId);
        builder.HasOne<ChannelEntity>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
