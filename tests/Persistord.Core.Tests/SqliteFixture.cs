using Microsoft.EntityFrameworkCore;
using Persistord.Testing;

namespace Persistord.Core.Tests;

/// <summary>Creates a context over a private in-memory SQLite database.</summary>
public static class SqliteFixture
{
    public static (SqliteTestDatabase Database, TestContext Context) Create() =>
        Create<TestContext>(options => new TestContext(options));

    public static (SqliteTestDatabase Database, TContext Context) Create<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory,
        bool createSchema = true)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(factory);

        var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        var context = createSchema
            ? database.CreateContext(factory)
            : factory(database.Options<TContext>());

        return (database, context);
    }
}
