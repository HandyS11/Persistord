using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Messages.Entities;

namespace Persistord.Messages.Configurations;

/// <summary>EF Core configuration for <see cref="AttachmentEntity"/>. The id is a
/// Discord snowflake supplied by the caller, so it is never store-generated.</summary>
public sealed class AttachmentEntityConfiguration : IEntityTypeConfiguration<AttachmentEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AttachmentEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
    }
}
