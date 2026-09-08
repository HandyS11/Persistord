using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Configurations;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>Model-building extensions that make <see cref="GuildEntity"/> the tenant root.</summary>
public static class GuildRootExtensions
{
    /// <summary>
    /// Registers <see cref="GuildEntity"/> as the guild root and, when <paramref name="cascade"/>
    /// is true, adds a cascading foreign key from every <see cref="IGuildScoped"/> entity's
    /// <c>GuildId</c> to it. Call it last in <c>OnModelCreating</c>: it wires the entity types the
    /// model knows about at that point.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="cascade">
    /// True (default) to add the foreign key with <see cref="DeleteBehavior.Cascade"/>, so deleting
    /// a guild row deletes everything scoped to it. This makes the guild row a prerequisite: insert
    /// it before any scoped row. False registers the root and leaves scoped entities untouched, which
    /// is the right choice when scoped rows may outlive their guild row.
    /// </param>
    /// <param name="filterLeftGuilds">
    /// True to add a global query filter that hides guilds with a non-null
    /// <see cref="GuildEntity.LeftAt"/>. Off by default. The filter applies to the root only —
    /// per-entity tenant filtering is not possible without ambient state, and the consumer resolves
    /// its guild from the interaction anyway.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyGuildRoot(
        this ModelBuilder modelBuilder,
        bool cascade = true,
        bool filterLeftGuilds = false)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new GuildEntityConfiguration());

        if (filterLeftGuilds)
        {
            modelBuilder.Entity<GuildEntity>().HasQueryFilter(g => g.LeftAt == null);
        }

        if (!cascade)
        {
            return modelBuilder;
        }

        var scoped = modelBuilder.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && typeof(IGuildScoped).IsAssignableFrom(e.ClrType))
            .Select(e => e.ClrType)
            .ToList();

        foreach (var clrType in scoped)
        {
            modelBuilder.Entity(clrType)
                .HasOne(typeof(GuildEntity))
                .WithMany()
                .HasForeignKey(nameof(IGuildScoped.GuildId))
                .OnDelete(DeleteBehavior.Cascade);
        }

        return modelBuilder;
    }
}
