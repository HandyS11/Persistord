using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Core.Tests;

/// <summary>
/// A shared-cache in-memory SQLite database several contexts can open independently, which
/// is what makes a genuine two-writer race testable. The held-open connection keeps the
/// database alive for the lifetime of the instance.
/// </summary>
public sealed class SharedSqliteDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;

    public SharedSqliteDatabase()
    {
        ConnectionString = $"DataSource={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    public string ConnectionString { get; }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    public TContext CreateContext<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory,
        params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(ConnectionString)
            .ReplaceService<IModelCacheKeyFactory, UniqueModelCacheKeyFactory>();
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return factory(builder.Options);
    }
}
