using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>Creates a context backed by a fresh open in-memory SQLite connection.</summary>
public static class SqliteFixture
{
    public static (SqliteConnection Connection, TestContext Context) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestContext(options);
        context.Database.EnsureCreated();
        return (connection, context);
    }
}
