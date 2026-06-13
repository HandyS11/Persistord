using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.History.Entities;
using Persistord.Messages.Entities;

namespace Persistord.History.Configurations;

/// <summary>EF Core configuration for <see cref="MessageHistoryEntity"/>.</summary>
public sealed class MessageHistoryEntityConfiguration : IEntityTypeConfiguration<MessageHistoryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageHistoryEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedOnAdd();
        builder.HasIndex(h => new { h.MessageId, h.RecordedAt });

        builder.HasOne<MessageEntity>()
            .WithMany()
            .HasForeignKey(h => h.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
