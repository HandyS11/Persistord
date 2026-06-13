using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Messages.Entities;

namespace Persistord.Messages.Configurations;

/// <summary>EF Core configuration for <see cref="MessageEntity"/>.</summary>
public sealed class MessageEntityConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    private readonly bool _filterDeleted;

    /// <summary>Creates the configuration.</summary>
    /// <param name="filterDeleted">When true, applies a global query filter that hides soft-deleted messages.</param>
    public MessageEntityConfiguration(bool filterDeleted)
    {
        _filterDeleted = filterDeleted;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasIndex(m => new { m.ChannelId, m.Id });

        if (_filterDeleted)
        {
            builder.HasQueryFilter(m => !m.IsDeleted);
        }

        builder.OwnsMany(m => m.Embeds, e =>
        {
            e.OwnsOne(x => x.Footer);
            e.OwnsOne(x => x.Author);
            e.OwnsMany(x => x.Fields);
        });

        builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);
        builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);
    }
}
