using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildRootTests
{
    [Fact]
    public void Guild_name_and_owner_are_optional()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            var guild = context.Model.FindEntityType(typeof(GuildEntity))!;
            Assert.True(guild.FindProperty(nameof(GuildEntity.Name))!.IsNullable);
            Assert.True(guild.FindProperty(nameof(GuildEntity.OwnerId))!.IsNullable);
            Assert.True(guild.FindProperty(nameof(GuildEntity.JoinedAt))!.IsNullable);
            Assert.True(guild.FindProperty(nameof(GuildEntity.LeftAt))!.IsNullable);
        }
    }

    [Fact]
    public async Task An_id_only_guild_row_persists()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        await using (context)
        {
            await context.Guilds.AddAsync(new GuildEntity
            {
                Id = 7UL
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var guild = await context.Guilds.SingleAsync();
            Assert.Null(guild.Name);
            Assert.Null(guild.OwnerId);
        }
    }

    [Fact]
    public void ApplyGuildRoot_adds_a_cascading_fk_to_every_scoped_entity()
    {
        var (connection, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: true));
        using (connection)
        using (context)
        {
            var scoped = context.Model.FindEntityType(typeof(ScopedRow))!;
            var fk = Assert.Single(scoped.GetForeignKeys());

            Assert.Equal(typeof(GuildEntity), fk.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(ScopedRow.GuildId), Assert.Single(fk.Properties).Name);
            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void ApplyGuildRoot_without_cascade_registers_the_root_and_no_fk()
    {
        var (connection, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: false));
        using (connection)
        using (context)
        {
            Assert.NotNull(context.Model.FindEntityType(typeof(GuildEntity)));
            Assert.Empty(context.Model.FindEntityType(typeof(ScopedRow))!.GetForeignKeys());
        }
    }

    [Fact]
    public async Task Deleting_a_guild_deletes_its_scoped_rows_in_the_database()
    {
        var (connection, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: true));
        using (connection)
        await using (context)
        {
            await context.Guilds.AddAsync(new GuildEntity
            {
                Id = 1UL
            });
            await context.Scoped.AddRangeAsync(
                new ScopedRow
                {
                    GuildId = 1UL, Label = "a"
                },
                new ScopedRow
                {
                    GuildId = 1UL, Label = "b"
                });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            // ExecuteDelete goes straight to SQL, so this proves the database-level cascade
            // rather than EF's client-side fix-up of tracked dependents.
            await context.Guilds.Where(g => g.Id == 1UL).ExecuteDeleteAsync();

            Assert.Empty(await context.Scoped.ToListAsync());
        }
    }

    [Fact]
    public async Task The_left_guild_filter_hides_departed_guilds()
    {
        var (connection, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: true, filterLeftGuilds: true));
        using (connection)
        await using (context)
        {
            await context.Guilds.AddRangeAsync(
                new GuildEntity
                {
                    Id = 1UL
                },
                new GuildEntity
                {
                    Id = 2UL, LeftAt = DateTimeOffset.UtcNow
                });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            Assert.Equal(1UL, (await context.Guilds.SingleAsync()).Id);
            Assert.Equal(2, await context.Guilds.IgnoreQueryFilters().CountAsync());
        }
    }

    internal sealed class ScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    internal sealed class RootContext(
        DbContextOptions<RootContext> options,
        bool cascade = true,
        bool filterLeftGuilds = false) : Persistord.Core.DiscordDbContext(options)
    {
        public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

        public DbSet<ScopedRow> Scoped => Set<ScopedRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyGuildRoot(cascade, filterLeftGuilds);
        }
    }
}
