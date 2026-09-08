using Microsoft.EntityFrameworkCore;

namespace Persistord.Testing.Tests;

public sealed class WidgetRow
{
    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public string Key { get; set; } = string.Empty;
}

public sealed class FixtureContext(DbContextOptions<FixtureContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<WidgetRow> Widgets => Set<WidgetRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<WidgetRow>().HasIndex(w => new
        {
            w.GuildId, w.Key
        }).IsUnique();
    }
}
