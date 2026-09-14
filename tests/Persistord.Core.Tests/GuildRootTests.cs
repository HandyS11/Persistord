using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildRootTests
{
    [Fact]
    public void Guild_name_and_owner_are_optional()
    {
        var (database, context) = SqliteFixture.Create();
        using (database)
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
        var (database, context) = SqliteFixture.Create();
        using (database)
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
        var (database, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: true));
        using (database)
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
        var (database, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: false));
        using (database)
        using (context)
        {
            Assert.NotNull(context.Model.FindEntityType(typeof(GuildEntity)));
            Assert.Empty(context.Model.FindEntityType(typeof(ScopedRow))!.GetForeignKeys());
        }
    }

    [Fact]
    public void ApplyGuildRoot_configures_the_guild_even_without_a_DbSet_or_the_conventions()
    {
        // No DbSet<GuildEntity> to discover it and no SnowflakeKeyConvention to fix its key: the root
        // is only in the model, caller-supplied, because ApplyGuildRoot applies its configuration.
        var (database, context) =
            SqliteFixture.Create<BareRootContext>(o => new BareRootContext(o), createSchema: false);
        using (database)
        using (context)
        {
            var guild = context.Model.FindEntityType(typeof(GuildEntity));

            Assert.NotNull(guild);
            Assert.Equal(ValueGenerated.Never, guild.FindProperty(nameof(GuildEntity.Id))!.ValueGenerated);
        }
    }

    [Fact]
    public void ApplyGuildRoot_adds_the_guild_fk_beside_a_GuildId_fk_to_another_principal()
    {
        var (database, context) = SqliteFixture.Create<TenantContext>(o => new TenantContext(o), createSchema: false);
        using (database)
        using (context)
        {
            var row = context.Model.FindEntityType(typeof(TenantScopedRow))!;

            var guildFk = Assert.Single(row.GetForeignKeys(),
                fk => fk.PrincipalEntityType.ClrType == typeof(GuildEntity));
            Assert.Equal(nameof(TenantScopedRow.GuildId), Assert.Single(guildFk.Properties).Name);
            Assert.Equal(DeleteBehavior.Cascade, guildFk.DeleteBehavior);

            var tenantFk = Assert.Single(row.GetForeignKeys(),
                fk => fk.PrincipalEntityType.ClrType == typeof(TenantRow));
            Assert.Equal(DeleteBehavior.Restrict, tenantFk.DeleteBehavior);
        }
    }

    [Fact]
    public async Task Deleting_a_guild_deletes_its_scoped_rows_in_the_database()
    {
        var (database, context) = SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: true));
        using (database)
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
        var (database, context) =
            SqliteFixture.Create<RootContext>(o => new RootContext(o, cascade: true, filterLeftGuilds: true));
        using (database)
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

        public string Label { get; set; } = string.Empty;

        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class TenantRow
    {
        public ulong Id { get; set; }
    }

    /// <summary>Scoped, but its <c>GuildId</c> already references a consumer-owned tenant table.</summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class TenantScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    private sealed class BareRootContext(DbContextOptions<BareRootContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyGuildRoot(cascade: false);
        }
    }

    private sealed class TenantContext(DbContextOptions<TenantContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TenantScopedRow>()
                .HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(r => r.GuildId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.ApplyGuildRoot();
        }
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
