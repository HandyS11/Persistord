using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildPurgeTests
{
    private static async Task SeedAsync(PurgeContext context)
    {
        await context.Guilds.AddRangeAsync(
            new GuildEntity
            {
                Id = 1UL
            },
            new GuildEntity
            {
                Id = 2UL
            });
        await context.Parents.AddRangeAsync(
            new ScopedParent
            {
                Id = 10, GuildId = 1UL
            },
            new ScopedParent
            {
                Id = 20, GuildId = 2UL
            });
        await context.Children.AddRangeAsync(
            new ScopedChild
            {
                GuildId = 1UL, ParentId = 10
            },
            new ScopedChild
            {
                GuildId = 2UL, ParentId = 20
            });
        await context.Notes.AddRangeAsync(
            new ScopedNote
            {
                GuildId = 1UL
            },
            new ScopedNote
            {
                GuildId = 2UL
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Purge_removes_every_scoped_row_of_one_guild_and_the_guild_itself()
    {
        var (database, context) = SqliteFixture.Create<PurgeContext>(o => new PurgeContext(o));
        using (database)
        await using (context)
        {
            await SeedAsync(context);

            var deleted = await context.PurgeGuildAsync(1UL);

            Assert.Equal(4, deleted); // parent + child + note + guild row
            Assert.Empty(await context.Children.Where(c => c.GuildId == 1UL).ToListAsync());
            Assert.Empty(await context.Parents.Where(p => p.GuildId == 1UL).ToListAsync());
            Assert.Empty(await context.Notes.Where(n => n.GuildId == 1UL).ToListAsync());
            Assert.Empty(await context.Guilds.Where(g => g.Id == 1UL).ToListAsync());
        }
    }

    [Fact]
    public async Task Purge_leaves_other_guilds_alone()
    {
        var (database, context) = SqliteFixture.Create<PurgeContext>(o => new PurgeContext(o));
        using (database)
        await using (context)
        {
            await SeedAsync(context);

            await context.PurgeGuildAsync(1UL);

            Assert.Single(await context.Parents.ToListAsync());
            Assert.Single(await context.Children.ToListAsync());
            Assert.Single(await context.Notes.ToListAsync());
            Assert.Single(await context.Guilds.ToListAsync());
        }
    }

    [Fact]
    public async Task Purge_deletes_dependents_before_principals()
    {
        // ScopedChild -> ScopedParent is Restrict, so deleting the parent first would throw.
        var (database, context) = SqliteFixture.Create<PurgeContext>(o => new PurgeContext(o));
        using (database)
        await using (context)
        {
            await SeedAsync(context);

            var exception = await Record.ExceptionAsync(() => context.PurgeGuildAsync(1UL));

            Assert.Null(exception);
        }
    }

    [Fact]
    public async Task Purge_reaches_a_guild_hidden_by_the_left_filter()
    {
        var (database, context) =
            SqliteFixture.Create<PurgeContext>(o => new PurgeContext(o, filterLeftGuilds: true));
        using (database)
        await using (context)
        {
            await context.Guilds.AddAsync(new GuildEntity
            {
                Id = 1UL, LeftAt = DateTimeOffset.UtcNow
            });
            await context.Notes.AddAsync(new ScopedNote
            {
                GuildId = 1UL
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var deleted = await context.PurgeGuildAsync(1UL);

            Assert.Equal(2, deleted);
            Assert.Empty(await context.Guilds.IgnoreQueryFilters().ToListAsync());
        }
    }

    [Fact]
    public async Task Purge_joins_an_ambient_transaction_so_a_rollback_undoes_it()
    {
        var (database, context) = SqliteFixture.Create<PurgeContext>(o => new PurgeContext(o));
        using (database)
        await using (context)
        {
            await SeedAsync(context);

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                await context.PurgeGuildAsync(1UL);
                await transaction.RollbackAsync();
            }

            Assert.Equal(2, await context.Guilds.CountAsync());
            Assert.Equal(2, await context.Notes.CountAsync());
        }
    }

    [Fact]
    public async Task Purge_guards_its_context() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).PurgeGuildAsync(1UL));

    internal sealed class ScopedParent : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    internal sealed class ScopedChild : IGuildScoped
    {
        public long Id { get; set; }

        public long ParentId { get; set; }

        public ulong GuildId { get; set; }
    }

    internal sealed class ScopedNote : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    internal sealed class PurgeContext(DbContextOptions<PurgeContext> options, bool filterLeftGuilds = false)
        : Persistord.Core.DiscordDbContext(options)
    {
        public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

        public DbSet<ScopedParent> Parents => Set<ScopedParent>();

        public DbSet<ScopedChild> Children => Set<ScopedChild>();

        public DbSet<ScopedNote> Notes => Set<ScopedNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScopedParent>().Property(p => p.Id).ValueGeneratedNever();
            modelBuilder.Entity<ScopedChild>()
                .HasOne<ScopedParent>()
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // cascade: false keeps the scoped tables free of a guild FK, which is the harder
            // case the purge helper exists for: nothing in the database deletes these rows.
            modelBuilder.ApplyGuildRoot(cascade: false, filterLeftGuilds);
        }
    }
}
