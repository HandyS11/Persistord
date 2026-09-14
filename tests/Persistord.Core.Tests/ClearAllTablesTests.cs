using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistord.Core.Entities;
using Persistord.Testing;
using Persistord.Tests.Shared;
using Xunit;

namespace Persistord.Core.Tests;

public class ClearAllTablesTests
{
    [Fact]
    public async Task Clear_empties_every_table_dependents_first()
    {
        var (database, context) =
            SqliteFixture.Create<GuildPurgeTests.PurgeContext>(o => new GuildPurgeTests.PurgeContext(o));
        using (database)
        await using (context)
        {
            await context.Guilds.AddAsync(new GuildEntity
            {
                Id = 1UL
            });
            await context.Parents.AddAsync(new GuildPurgeTests.ScopedParent
            {
                Id = 10, GuildId = 1UL
            });
            await context.Children.AddAsync(new GuildPurgeTests.ScopedChild
            {
                GuildId = 1UL, ParentId = 10
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var deleted = await context.ClearAllTablesAsync();

            Assert.Equal(3, deleted);
            Assert.Empty(await context.Guilds.ToListAsync());
            Assert.Empty(await context.Parents.ToListAsync());
            Assert.Empty(await context.Children.ToListAsync());
        }
    }

    [Fact]
    public async Task Clear_joins_an_ambient_transaction_so_a_rollback_undoes_it()
    {
        var (database, context) =
            SqliteFixture.Create<GuildPurgeTests.PurgeContext>(o => new GuildPurgeTests.PurgeContext(o));
        using (database)
        await using (context)
        {
            await context.Guilds.AddAsync(new GuildEntity
            {
                Id = 1UL
            });
            await context.Parents.AddAsync(new GuildPurgeTests.ScopedParent
            {
                Id = 10, GuildId = 1UL
            });
            await context.Children.AddAsync(new GuildPurgeTests.ScopedChild
            {
                GuildId = 1UL, ParentId = 10
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                await context.ClearAllTablesAsync();
                await transaction.RollbackAsync();
            }

            Assert.Equal(1, await context.Guilds.CountAsync());
            Assert.Equal(1, await context.Parents.CountAsync());
            Assert.Equal(1, await context.Children.CountAsync());
        }
    }

    [Fact]
    public async Task Clear_on_an_empty_database_deletes_nothing()
    {
        var (database, context) =
            SqliteFixture.Create<GuildPurgeTests.PurgeContext>(o => new GuildPurgeTests.PurgeContext(o));
        using (database)
        await using (context)
        {
            Assert.Equal(0, await context.ClearAllTablesAsync());
        }
    }

    [Fact]
    public async Task Clear_guards_its_context() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).ClearAllTablesAsync());

    [Fact]
    public async Task Clear_issues_one_delete_per_table()
    {
        var commands = new NonQueryRecorder();
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<GuildPurgeTests.PurgeContext>(o => new GuildPurgeTests.PurgeContext(o), commands);

        await context.ClearAllTablesAsync();

        // ScopedChild is visited twice — once as ScopedParent's dependent, once in model order — but
        // must be ordered, and deleted, once.
        Assert.Equal(
            ["\"Guilds\"", "\"Children\"", "\"Notes\"", "\"Parents\""],
            commands.Texts.Select(text => text.Split(' ')[2]));
    }

    [Fact]
    public async Task Clear_leaves_owned_types_to_their_owners()
    {
        var (database, context) = SqliteFixture.Create<PartedWidgetContext>(o => new PartedWidgetContext(o));
        using (database)
        await using (context)
        {
            var widget = new PartedWidgetEntity
            {
                GuildId = 1UL,
                Key = "dash",
                Settings = new WidgetSettings
                {
                    Prefix = "!"
                },
            };
            widget.Children.Add(new PartedWidgetChild
            {
                Name = "child"
            });
            await context.PartedWidgets.AddAsync(widget);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            Assert.Equal(2, await context.ClearAllTablesAsync());
            Assert.Empty(await context.PartedWidgets.ToListAsync());
        }
    }

    [Fact]
    public async Task Clear_rolls_every_delete_back_when_one_fails()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<GuildPurgeTests.PurgeContext>(
            o => new GuildPurgeTests.PurgeContext(o),
            new FailingCommandInterceptor("DELETE FROM \"Parents\""));
        await SeedGraphAsync(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.ClearAllTablesAsync());

        Assert.Equal(1, await context.Guilds.CountAsync());
        Assert.Equal(1, await context.Children.CountAsync());
        Assert.Equal(1, await context.Parents.CountAsync());
    }

    [Fact]
    public async Task Clear_only_nulls_self_references_whose_every_column_is_a_nullable_clr_property()
    {
        var commands = new NonQueryRecorder();
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<ReferenceShapesContext>(o => new ReferenceShapesContext(o), commands);

        var owner = new Owner();
        var parent = new Node
        {
            Owner = owner
        };
        await context.AddRangeAsync(owner, parent, new Node
        {
            Parent = parent, Owner = owner
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await context.ClearAllTablesAsync();

        // Node.ParentId is the one self-reference whose columns are all nullable CLR properties.
        // Node.OwnerId is nullable but not a self-reference; Cell mixes a non-nullable column into its
        // self-reference; ShadowNode's self-reference has no CLR property to write through.
        var update = Assert.Single(commands.Texts, text => text.StartsWith("UPDATE", StringComparison.Ordinal));
        Assert.StartsWith("UPDATE \"Node\"", update, StringComparison.Ordinal);
        Assert.Contains("\"ParentId\" = NULL", update, StringComparison.Ordinal);
        Assert.Empty(await context.Set<Node>().ToListAsync());
    }

    [Theory]
    [InlineData("BEGIN")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("COMMIT")]
    public async Task Clear_never_resumes_on_the_callers_synchronization_context(string yieldOn)
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<TestContext>(o => new TestContext(o), new YieldingInterceptor(yieldOn));

        Assert.Equal(0, await SynchronizationContextProbe.CountPostsAsync(() => context.ClearAllTablesAsync()));
    }

    private static async Task SeedGraphAsync(GuildPurgeTests.PurgeContext context)
    {
        await context.Guilds.AddAsync(new GuildEntity
        {
            Id = 1UL
        });
        await context.Parents.AddAsync(new GuildPurgeTests.ScopedParent
        {
            Id = 10, GuildId = 1UL
        });
        await context.Children.AddAsync(new GuildPurgeTests.ScopedChild
        {
            GuildId = 1UL, ParentId = 10
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    /// <summary>Records the SQL of every asynchronous non-query command, which is how ExecuteDelete and ExecuteUpdate run.</summary>
    private sealed class NonQueryRecorder : DbCommandInterceptor
    {
        public List<string> Texts { get; } = [];

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Texts.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class Owner
    {
        public long Id { get; set; }
    }

    internal sealed class Node
    {
        public long Id { get; set; }

        public long? ParentId { get; set; }

        public Node? Parent { get; set; }

        public long? OwnerId { get; set; }

        public Owner? Owner { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class Cell
    {
        public long Id { get; set; }

        public long Row { get; set; }

        public long? ParentId { get; set; }

        public long ParentRow { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class ShadowNode
    {
        public long Id { get; set; }
    }

    internal sealed class ReferenceShapesContext(DbContextOptions<ReferenceShapesContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Node>().HasOne(n => n.Parent).WithMany().HasForeignKey(n => n.ParentId);
            modelBuilder.Entity<Node>().HasOne(n => n.Owner).WithMany().HasForeignKey(n => n.OwnerId);
            modelBuilder.Entity<Cell>().HasKey(c => new
            {
                c.Id, c.Row
            });
            modelBuilder.Entity<Cell>().HasOne<Cell>().WithMany().HasForeignKey(c => new
            {
                c.ParentId, c.ParentRow
            });
            modelBuilder.Entity<ShadowNode>().HasOne<ShadowNode>().WithMany().HasForeignKey("ParentId");
        }
    }
}
