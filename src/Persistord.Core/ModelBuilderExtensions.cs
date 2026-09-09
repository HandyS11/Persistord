using Microsoft.EntityFrameworkCore;
using Persistord.Core.Configurations;

namespace Persistord.Core;

/// <summary>Model-building extensions that wire the core Persistord entities.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the configurations for all skeleton entities (guild, channel, user, member,
    /// role). Call from <c>OnModelCreating</c>, or derive <see cref="DiscordGraphDbContext"/>
    /// which calls it for you.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyCoreGraph(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder
            .ApplyConfiguration(new GuildEntityConfiguration())
            .ApplyConfiguration(new ChannelEntityConfiguration())
            .ApplyConfiguration(new UserEntityConfiguration())
            .ApplyConfiguration(new MemberEntityConfiguration())
            .ApplyConfiguration(new RoleEntityConfiguration());
    }

    /// <summary>Renamed to <see cref="ApplyCoreGraph"/>.</summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
#pragma warning disable S1133 // Intentional one-release forwarder; removal is tracked by the deprecation note above.
    [Obsolete("Renamed to ApplyCoreGraph. This forwarder is kept for one release and will be removed.")]
    public static ModelBuilder ApplyCoreConfiguration(this ModelBuilder modelBuilder) =>
        modelBuilder.ApplyCoreGraph();
#pragma warning restore S1133
}
