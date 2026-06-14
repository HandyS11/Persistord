using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Messages.Entities;

namespace Persistord.Messages.Configurations;

/// <summary>EF Core configuration for <see cref="MessageEntity"/>.</summary>
/// <param name="filterDeleted">When true, applies a global query filter that hides soft-deleted messages.</param>
public sealed class MessageEntityConfiguration(bool filterDeleted) : IEntityTypeConfiguration<MessageEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasIndex(m => new
        {
            m.ChannelId, m.Id
        });

        if (filterDeleted)
        {
            builder.HasQueryFilter(m => !m.IsDeleted);
        }

        builder.HasMany(m => m.Embeds).WithOne().HasForeignKey(e => e.MessageId);
        builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);
        builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);
    }
}
