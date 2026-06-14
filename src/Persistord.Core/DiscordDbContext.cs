using Microsoft.EntityFrameworkCore;
using Persistord.Core.Conversions;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>
/// Base EF Core context that ships the core Discord skeleton and the global
/// snowflake conversion. Inherit it, declare module <c>DbSet</c>s, and apply
/// module configurations in <c>OnModelCreating</c>. The library never selects a
/// provider; the consumer calls <c>UseSqlite</c>/<c>UseNpgsql</c>/etc.
/// </summary>
/// <remarks>Initializes the context with the given options.</remarks>
/// <param name="options">The context options supplied by the consumer.</param>
public abstract class DiscordDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Persisted guilds.</summary>
    public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

    /// <summary>Persisted channels.</summary>
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();

    /// <summary>Persisted users.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Persisted guild members.</summary>
    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    /// <summary>Persisted roles.</summary>
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyCoreConfiguration();
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<ulong>().HaveConversion<UlongToLongConverter>();
        configurationBuilder.Properties<ulong?>().HaveConversion<NullableUlongToLongConverter>();
    }
}
