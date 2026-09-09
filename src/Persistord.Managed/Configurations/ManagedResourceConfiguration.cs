using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>The shape every managed resource shares.</summary>
internal static class ManagedResourceConfiguration
{
    /// <summary>Configures the key, scope/key uniqueness, and table name common to every managed resource.</summary>
    /// <typeparam name="TResource">The concrete managed resource type.</typeparam>
    /// <param name="builder">The entity type builder to configure.</param>
    /// <param name="tableName">The pinned table name for <typeparamref name="TResource"/>.</param>
    public static void ConfigureCommon<TResource>(EntityTypeBuilder<TResource> builder, string tableName)
        where TResource : ManagedResource
    {
        builder.ToTable(tableName);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.Scope).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(64);
        builder.HasIndex(r => new
        {
            r.GuildId, r.Scope, r.Key
        }).IsUnique();
    }
}
