using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.Core.Entities;
using Persistord.Managed;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Tests;

public sealed class ManagedContext : Persistord.Core.DiscordDbContext
{
    private readonly bool _guildRoot;

    public ManagedContext(DbContextOptions<ManagedContext> options, bool guildRoot = false)
        : base(options) => _guildRoot = guildRoot;

    public ManagedContext(DbContextOptions<ManagedContext> options, TimeProvider timeProvider, bool guildRoot = false)
        : base(options, timeProvider) => _guildRoot = guildRoot;

    public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

    public DbSet<ManagedCategory> Categories => Set<ManagedCategory>();

    public DbSet<ManagedChannel> Channels => Set<ManagedChannel>();

    public DbSet<ManagedMessage> Messages => Set<ManagedMessage>();

    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();

        if (_guildRoot)
        {
            modelBuilder.ApplyGuildRoot();
        }
    }
}
