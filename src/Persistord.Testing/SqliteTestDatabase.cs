using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Testing;

/// <summary>How <see cref="SqliteTestDatabase"/> builds the schema.</summary>
public enum TestSchema
{
    /// <summary>
    /// Apply the context's migrations. Slower, and the point: a model that has drifted from the
    /// committed migrations fails here instead of in production.
    /// </summary>
    Migrate = 0,

    /// <summary>Create the schema straight from the model. Fast, and blind to migration drift.</summary>
    EnsureCreated = 1,
}

/// <summary>
/// An in-memory SQLite database that lives as long as the instance. Two flavours:
/// <see cref="Private"/> hands every context the one held-open connection, because
/// <c>DataSource=:memory:</c> is a different database per connection; <see cref="Shared"/> uses
/// shared-cache mode so DI scopes can open their own connections against the same database.
/// </summary>
public sealed class SqliteTestDatabase : IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly bool _sharedCache;
    private bool _schemaCreated;

    private SqliteTestDatabase(string connectionString, bool sharedCache, TestSchema schema)
    {
        ConnectionString = connectionString;
        Schema = schema;
        _sharedCache = sharedCache;
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    /// <summary>The connection string this database was opened with.</summary>
    public string ConnectionString { get; }

    /// <summary>How the schema is built the first time a context asks for it.</summary>
    public TestSchema Schema { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _connection.DisposeAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();

    /// <summary>Creates a private in-memory database backed by one held-open connection.</summary>
    /// <param name="schema">How to build the schema. Defaults to <see cref="TestSchema.Migrate"/>.</param>
    /// <returns>The database. Dispose it to drop the data.</returns>
    public static SqliteTestDatabase Private(TestSchema schema = TestSchema.Migrate) =>
        new("DataSource=:memory:", sharedCache: false, schema);

    /// <summary>
    /// Creates a shared-cache in-memory database several connections can open independently — what
    /// you need to test DI scopes, or two writers racing on the same row.
    /// </summary>
    /// <param name="name">The database name. Defaults to a fresh GUID, so tests never collide.</param>
    /// <param name="schema">How to build the schema. Defaults to <see cref="TestSchema.Migrate"/>.</param>
    /// <returns>The database. Dispose it to drop the data.</returns>
    public static SqliteTestDatabase Shared(string? name = null, TestSchema schema = TestSchema.Migrate) =>
        new(
            $"DataSource={name ?? Guid.NewGuid().ToString("N")};Mode=Memory;Cache=Shared",
            sharedCache: true,
            schema);

    /// <summary>
    /// Builds options for a context over this database, with
    /// <see cref="UniqueModelCacheKeyFactory"/> installed.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="interceptors">Interceptors to register, if any.</param>
    /// <returns>The options.</returns>
    public DbContextOptions<TContext> Options<TContext>(params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(interceptors);
        return BuildOptions<TContext>(interceptors, configure: null);
    }

    /// <summary>
    /// Builds options for a context over this database, with
    /// <see cref="UniqueModelCacheKeyFactory"/> installed, then lets <paramref name="configure"/>
    /// override anything on top of the base setup — most importantly, to pin a context-wide
    /// <see cref="QueryTrackingBehavior"/>:
    /// <code>
    /// database.Options&lt;MyContext&gt;(builder =&gt;
    ///     builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
    /// </code>
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="configure">Applied after the base setup, so it can override anything.</param>
    /// <param name="interceptors">Interceptors to register, if any.</param>
    /// <returns>The options.</returns>
    public DbContextOptions<TContext> Options<TContext>(
        Action<DbContextOptionsBuilder<TContext>> configure,
        params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(interceptors);
        return BuildOptions(interceptors, configure);
    }

    /// <summary>
    /// Creates a context over this database and builds the schema on the first call.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="factory">Constructs the context from its options.</param>
    /// <param name="interceptors">Interceptors to register, if any.</param>
    /// <returns>The context. Dispose it before disposing the database.</returns>
    public TContext CreateContext<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory,
        params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(interceptors);

        var context = factory(Options<TContext>(interceptors));
        EnsureSchema(context);
        return context;
    }

    /// <summary>
    /// Creates a context over this database and builds the schema on the first call, letting
    /// <paramref name="configure"/> override anything on top of the base setup — most importantly,
    /// to pin a context-wide <see cref="QueryTrackingBehavior"/>:
    /// <code>
    /// database.CreateContext(
    ///     o =&gt; new MyContext(o),
    ///     configure: builder =&gt; builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
    /// </code>
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="factory">Constructs the context from its options.</param>
    /// <param name="configure">Applied after the base setup, so it can override anything.</param>
    /// <param name="interceptors">Interceptors to register, if any.</param>
    /// <returns>The context. Dispose it before disposing the database.</returns>
    public TContext CreateContext<TContext>(
        Func<DbContextOptions<TContext>, TContext> factory,
        Action<DbContextOptionsBuilder<TContext>> configure,
        params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(interceptors);

        var context = factory(Options(configure, interceptors));
        EnsureSchema(context);
        return context;
    }

    private DbContextOptions<TContext> BuildOptions<TContext>(
        IInterceptor[] interceptors,
        Action<DbContextOptionsBuilder<TContext>>? configure)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>()
            .ReplaceService<IModelCacheKeyFactory, UniqueModelCacheKeyFactory>();

        if (_sharedCache)
        {
            builder.UseSqlite(ConnectionString);
        }
        else
        {
            builder.UseSqlite(_connection);
        }

        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        configure?.Invoke(builder);

        return builder.Options;
    }

    private void EnsureSchema(DbContext context)
    {
        if (_schemaCreated)
        {
            return;
        }

        _schemaCreated = true;
        if (Schema == TestSchema.Migrate)
        {
            context.Database.Migrate();
        }
        else
        {
            context.Database.EnsureCreated();
        }
    }
}
