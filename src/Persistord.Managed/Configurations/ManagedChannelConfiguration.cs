using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>EF Core configuration for <see cref="ManagedChannel"/>.</summary>
public sealed class ManagedChannelConfiguration : IEntityTypeConfiguration<ManagedChannel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ManagedChannel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ManagedResourceConfiguration.ConfigureCommon(builder, "ManagedChannels");
    }
}
