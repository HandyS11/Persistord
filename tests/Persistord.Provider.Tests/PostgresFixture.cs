using Testcontainers.PostgreSql;
using Xunit;

namespace Persistord.Provider.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string? ConnectionString { get; private set; }

    public bool Available => ConnectionString is not null;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
#pragma warning disable CA1031 // Docker may be unavailable (e.g. Windows CI leg); tests using this fixture skip.
        catch
#pragma warning restore CA1031
        {
            ConnectionString = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
