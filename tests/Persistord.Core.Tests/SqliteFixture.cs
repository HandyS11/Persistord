using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Core.Tests;

/// <summary>Creates a context backed by a fresh open in-memory SQLite connection.</summary>
public static class SqliteFixture
{
    public static (SqliteConnection Connection, TestContext Context) Create() =>
        Create<TestContext>(options => new TestContext(options));

    public static (SqliteConnection Connection, TContext Context) Create<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory,
        bool createSchema = true,
        Action<DbContextOptionsBuilder<TContext>>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(factory);

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var builder = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCacheKeyFactory, UniqueModelCacheKeyFactory>();
        configure?.Invoke(builder);
        var context = factory(builder.Options);
        if (createSchema)
        {
            context.Database.EnsureCreated();
        }

        return (connection, context);
    }
}
