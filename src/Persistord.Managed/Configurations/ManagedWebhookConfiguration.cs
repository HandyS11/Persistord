using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>EF Core configuration for <see cref="ManagedWebhook"/>.</summary>
public sealed class ManagedWebhookConfiguration : IEntityTypeConfiguration<ManagedWebhook>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ManagedWebhook> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ManagedResourceConfiguration.ConfigureCommon(builder, "ManagedWebhooks");

        builder.Property(w => w.Token).IsRequired();
    }
}
