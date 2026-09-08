using Microsoft.EntityFrameworkCore;
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
    }
}
