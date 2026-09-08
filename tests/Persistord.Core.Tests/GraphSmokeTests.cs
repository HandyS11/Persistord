using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

/// <summary>
/// End-to-end coverage over <see cref="DiscordGraphDbContext"/> — the graph Persistord itself
/// ships — rather than a purpose-built model. <see cref="ApplyGuildRoot"/>, <see cref="PurgeGuildAsync"/>
/// and <see cref="ClearAllTablesAsync"/> were each unit-tested against a bespoke model; this class
/// exercises all three together against the real skeleton, including a channel category/child
/// hierarchy and a consumer entity that owns its own guild relationship.
/// </summary>
public class GraphSmokeTests
{
    private static async Task SeedSkeletonAsync(SmokeGraphContext context)
    {
        await context.Guilds.AddAsync(new GuildEntity
        {
            Id = 1UL
        });
        await context.Channels.AddRangeAsync(
            new ChannelEntity
            {
                Id = 100UL, GuildId = 1UL, Type = ChannelType.Category, Name = "category"
            },
            new ChannelEntity
            {
                Id = 200UL, GuildId = 1UL, ParentId = 100UL, Type = ChannelType.Text, Name = "general"
            });
        await context.Users.AddAsync(new UserEntity
        {
            Id = 500UL, Username = "someone"
        });
        await context.Members.AddAsync(new MemberEntity
        {
            GuildId = 1UL, UserId = 500UL
        });
        await context.Roles.AddAsync(new RoleEntity
        {
            Id = 600UL, GuildId = 1UL, Name = "member"
        });
        await context.ScopedRows.AddAsync(new ScopedWithOwnRelationship
        {
            GuildId = 1UL
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [Fact]
    public void ApplyGuildRoot_preserves_a_consumer_entitys_own_guild_relationship()
    {
        var (connection, context) = SqliteFixture.Create<SmokeGraphContext>(
            o => new SmokeGraphContext(o), createSchema: false);
        using (connection)
        using (context)
        {
            var scopedType = context.Model.FindEntityType(typeof(ScopedWithOwnRelationship))!;
            var foreignKey = Assert.Single(scopedType.GetForeignKeys());

            Assert.Equal(typeof(GuildEntity), foreignKey.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(ScopedWithOwnRelationship.GuildId), Assert.Single(foreignKey.Properties).Name);
            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
            Assert.Equal(nameof(ScopedWithOwnRelationship.Guild), foreignKey.DependentToPrincipal?.Name);
        }
    }

    [Fact]
    public async Task PurgeGuildAsync_deletes_the_guild_and_scoped_rows_but_leaves_the_skeleton_graph()
    {
        var (connection, context) = SqliteFixture.Create<SmokeGraphContext>(o => new SmokeGraphContext(o));
        using (connection)
        await using (context)
        {
            await SeedSkeletonAsync(context);

            var deleted = await context.PurgeGuildAsync(1UL);

            // Only the guild row and the IGuildScoped consumer row participate: the five skeleton
            // entities are deliberately not IGuildScoped (finding 4), so they are untouched.
            Assert.Equal(2, deleted);
            Assert.Empty(await context.Guilds.ToListAsync());
            Assert.Empty(await context.ScopedRows.ToListAsync());
            Assert.Equal(2, await context.Channels.CountAsync());
            Assert.Single(await context.Users.ToListAsync());
            Assert.Single(await context.Members.ToListAsync());
            Assert.Single(await context.Roles.ToListAsync());
        }
    }

    [Fact]
    public async Task ClearAllTablesAsync_empties_the_whole_graph_including_a_channel_hierarchy()
    {
        var (connection, context) = SqliteFixture.Create<SmokeGraphContext>(o => new SmokeGraphContext(o));
        using (connection)
        await using (context)
        {
            await SeedSkeletonAsync(context);

            // ChannelEntity self-references through ParentId with DeleteBehavior.Restrict, and
            // ModelDeleteOrder deliberately does not order self-references: without nulling the
            // self-reference first, a plain DELETE FROM Channels trips its own foreign key.
            var exception = await Record.ExceptionAsync(() => context.ClearAllTablesAsync());

            Assert.Null(exception);
        }
    }

    [Fact]
    public async Task ClearAllTablesAsync_reports_every_row_deleted_across_the_graph()
    {
        var (connection, context) = SqliteFixture.Create<SmokeGraphContext>(o => new SmokeGraphContext(o));
        using (connection)
        await using (context)
        {
            await SeedSkeletonAsync(context);

            // guild + 2 channels + user + member + role + scoped row.
            var deleted = await context.ClearAllTablesAsync();

            Assert.Equal(7, deleted);
            Assert.Empty(await context.Guilds.ToListAsync());
            Assert.Empty(await context.Channels.ToListAsync());
            Assert.Empty(await context.Users.ToListAsync());
            Assert.Empty(await context.Members.ToListAsync());
            Assert.Empty(await context.Roles.ToListAsync());
            Assert.Empty(await context.ScopedRows.ToListAsync());
        }
    }

    /// <summary>A consumer entity that declares its own guild navigation and foreign key, the shape
    /// finding 3 was reproduced against.</summary>
    internal sealed class ScopedWithOwnRelationship : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }

        public GuildEntity? Guild { get; set; }
    }

    internal sealed class SmokeGraphContext(DbContextOptions<SmokeGraphContext> options)
        : Persistord.Core.DiscordGraphDbContext(options)
    {
        public DbSet<ScopedWithOwnRelationship> ScopedRows => Set<ScopedWithOwnRelationship>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScopedWithOwnRelationship>()
                .HasOne(r => r.Guild)
                .WithMany()
                .HasForeignKey(r => r.GuildId);
            modelBuilder.ApplyGuildRoot();
        }
    }
}
