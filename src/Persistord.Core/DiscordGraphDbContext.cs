using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>
/// <see cref="DiscordDbContext"/> plus the opt-in Discord skeleton graph: guilds, channels,
/// users, members and roles. Derive this when the context mirrors Discord objects; derive
/// <see cref="DiscordDbContext"/> when it only needs the conventions.
/// </summary>
public abstract class DiscordGraphDbContext : DiscordDbContext
{
    /// <summary>Initializes the context with the given options.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    protected DiscordGraphDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Initializes the context with the given options and a clock for timestamp stamping.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    /// <param name="timeProvider">The clock to stamp from.</param>
    protected DiscordGraphDbContext(DbContextOptions options, TimeProvider timeProvider)
        : base(options, timeProvider)
    {
    }

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
