using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Messages.Owned;

namespace Persistord.Messages.Configurations;

/// <summary>EF Core configuration for <see cref="Embed"/>: a relational child of a
/// message with owned footer/author blocks and a relational collection of fields.</summary>
public sealed class EmbedEntityConfiguration : IEntityTypeConfiguration<Embed>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Embed> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.OwnsOne(e => e.Footer);
        builder.OwnsOne(e => e.Author);
        builder.HasMany(e => e.Fields).WithOne().HasForeignKey(f => f.EmbedId);
    }
}
