using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>
/// <see cref="DiscordDbContext"/> plus the opt-in Discord skeleton graph: guilds, channels,
/// users, members and roles. Derive this when the context mirrors Discord objects; derive
/// <see cref="DiscordDbContext"/> when it only needs the conventions.
/// </summary>
/// <remarks>Initializes the context with the given options.</remarks>
/// <param name="options">The context options supplied by the consumer.</param>
public abstract class DiscordGraphDbContext(DbContextOptions options) : DiscordDbContext(options)
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
        modelBuilder.ApplyCoreGraph();
    }
}
