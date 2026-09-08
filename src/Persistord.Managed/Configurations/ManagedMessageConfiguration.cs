using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>EF Core configuration for <see cref="ManagedMessage"/>.</summary>
public sealed class ManagedMessageConfiguration : IEntityTypeConfiguration<ManagedMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ManagedMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ManagedResourceConfiguration.ConfigureCommon(builder, "ManagedMessages");

        builder.Property(m => m.ContentHash).HasMaxLength(64);

        // A MessageDeleted gateway event carries only the message id, so the reconciler must be
        // able to find the record by DiscordId without scanning.
        builder.HasIndex(m => m.DiscordId);
    }
}
