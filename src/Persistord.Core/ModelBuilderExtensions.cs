using Microsoft.EntityFrameworkCore;
using Persistord.Core.Configurations;

namespace Persistord.Core;

/// <summary>Model-building extensions that wire the core Persistord entities.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the configurations for all core skeleton entities
    /// (guild, channel, user, member, role). Call from <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyCoreConfiguration(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder
            .ApplyConfiguration(new GuildEntityConfiguration())
            .ApplyConfiguration(new ChannelEntityConfiguration())
            .ApplyConfiguration(new UserEntityConfiguration())
            .ApplyConfiguration(new MemberEntityConfiguration())
            .ApplyConfiguration(new RoleEntityConfiguration());
    }
}
