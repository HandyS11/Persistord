using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Persistord.Testing.Tests;

public class SqliteTestDatabaseTests
{
    private static WidgetRow Widget(string key) => new()
    {
        GuildId = 1UL, Key = key
    };

    [Fact]
    public async Task Private_databases_do_not_see_each_other()
    {
        await using var first = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var second = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        await using var firstContext = first.CreateContext<FixtureContext>(o => new FixtureContext(o));
        await using var secondContext = second.CreateContext<FixtureContext>(o => new FixtureContext(o));

        await firstContext.Widgets.AddAsync(Widget("a"));
        await firstContext.SaveChangesAsync();

        Assert.Single(await firstContext.Widgets.ToListAsync());
        Assert.Empty(await secondContext.Widgets.ToListAsync());
    }

    [Fact]
    public async Task Private_shares_one_connection_across_contexts()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        await using (var writer = database.CreateContext<FixtureContext>(o => new FixtureContext(o)))
        {
            await writer.Widgets.AddAsync(Widget("a"));
            await writer.SaveChangesAsync();
        }

        await using var reader = database.CreateContext<FixtureContext>(o => new FixtureContext(o));
        Assert.Single(await reader.Widgets.ToListAsync());
    }

    [Fact]
    public async Task Shared_lets_independent_connections_see_the_same_rows()
    {
        await using var database = SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated);

        await using (var writer = database.CreateContext<FixtureContext>(o => new FixtureContext(o)))
        {
            await writer.Widgets.AddAsync(Widget("a"));
            await writer.SaveChangesAsync();
        }

        // A second context built from the connection string, not from the connection object.
        await using var reader = new FixtureContext(database.Options<FixtureContext>());
        Assert.Single(await reader.Widgets.ToListAsync());
    }

    [Fact]
    public async Task Shared_accepts_an_explicit_name()
    {
        await using var database = SqliteTestDatabase.Shared("named-fixture", TestSchema.EnsureCreated);
        Assert.Contains("named-fixture", database.ConnectionString, StringComparison.Ordinal);

        await using var context = database.CreateContext<FixtureContext>(o => new FixtureContext(o));
        await context.Widgets.AddAsync(Widget("a"));
        Assert.Equal(1, await context.SaveChangesAsync());
    }

    [Fact]
    public void Schema_defaults_to_migrate()
    {
        using var database = SqliteTestDatabase.Private();
        Assert.Equal(TestSchema.Migrate, database.Schema);
    }

    [Fact]
    public void CreateContext_guards_its_factory()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        Assert.Throws<ArgumentNullException>(() => database.CreateContext<FixtureContext>(null!));
    }

    [Fact]
    public async Task Migrate_applies_the_committed_migrations()
    {
        await using var database = SqliteTestDatabase.Private();
        await using var context = database.CreateContext<FixtureContext>(o => new FixtureContext(o));

        await context.Widgets.AddAsync(Widget("a"));
        await context.SaveChangesAsync();

        Assert.Single(await context.Widgets.ToListAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task CreateContext_configure_overrides_the_base_setup()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        await using var context = database.CreateContext<FixtureContext>(
            o => new FixtureContext(o),
            configure: builder => builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        Assert.Equal(QueryTrackingBehavior.NoTracking, context.ChangeTracker.QueryTrackingBehavior);

        // The configure overload builds the schema too.
        await context.Widgets.AddAsync(Widget("a"));
        Assert.Equal(1, await context.SaveChangesAsync());
    }

    [Fact]
    public void Private_is_an_in_memory_database()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        Assert.Equal("DataSource=:memory:", database.ConnectionString);
    }

    [Fact]
    public void Shared_defaults_to_a_fresh_guid_name()
    {
        using var first = SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated);
        using var second = SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated);

        Assert.Matches("^DataSource=[0-9a-f]{32};Mode=Memory;Cache=Shared$", first.ConnectionString);
        Assert.NotEqual(first.ConnectionString, second.ConnectionString);
    }

    [Fact]
    public void Private_hands_every_context_the_same_connection_object()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var first = database.CreateContext<FixtureContext>(o => new FixtureContext(o));
        using var second = database.CreateContext<FixtureContext>(o => new FixtureContext(o));

        Assert.Same(first.Database.GetDbConnection(), second.Database.GetDbConnection());
    }

    [Fact]
    public void Shared_gives_every_context_its_own_connection()
    {
        using var database = SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated);
        using var first = database.CreateContext<FixtureContext>(o => new FixtureContext(o));
        using var second = database.CreateContext<FixtureContext>(o => new FixtureContext(o));

        Assert.NotSame(first.Database.GetDbConnection(), second.Database.GetDbConnection());
    }

    [Fact]
    public async Task CreateContext_registers_the_interceptors_and_builds_the_schema_only_once()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var first = database.CreateContext<FixtureContext>(o => new FixtureContext(o));

        var commands = new CommandCounter();
        await using var context = database.CreateContext<FixtureContext>(o => new FixtureContext(o), commands);

        // The schema already exists, so creating a second context must not touch the database...
        Assert.Equal(0, commands.Count);

        // ...and the interceptor is live for the commands the context does run.
        await context.Widgets.CountAsync();
        Assert.Equal(1, commands.Count);
    }

    [Fact]
    public void Options_and_CreateContext_guard_their_arguments()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        Assert.Throws<ArgumentNullException>(
            "interceptors",
            () => database.Options<FixtureContext>((IInterceptor[])null!));
        Assert.Throws<ArgumentNullException>(
            "configure",
            () => database.Options((Action<DbContextOptionsBuilder<FixtureContext>>)null!));
        Assert.Throws<ArgumentNullException>(
            "interceptors",
            () => database.Options<FixtureContext>(_ => { }, null!));
        Assert.Throws<ArgumentNullException>(
            "factory",
            () => database.CreateContext<FixtureContext>(null!, _ => { }));
    }

    /// <summary>Counts every command the context sends, whatever its kind.</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Count++;
            return result;
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            Count++;
            return result;
        }
    }
}
