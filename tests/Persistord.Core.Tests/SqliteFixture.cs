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
        bool createSchema = true)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(factory);

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCacheKeyFactory, UniqueModelCacheKeyFactory>()
            .Options;
        var context = factory(options);
        if (createSchema)
        {
            context.Database.EnsureCreated();
        }

        return (connection, context);
    }
}
