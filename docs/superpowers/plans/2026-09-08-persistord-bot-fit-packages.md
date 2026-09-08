# Persistord Bot-Fit — Satellite Packages (beta4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the three optional packages a real bot needs on top of the bot-fit Core — `Persistord.Testing`, `Persistord.Managed`, `Persistord.Protection` — and the documentation for the whole bot-fit release.

**Architecture:** Each package depends on `Persistord.Core` and nothing else from the repo. `Persistord.Testing` wraps in-memory SQLite and EF model assertions. `Persistord.Managed` is four entities with one shared shape (`ManagedResource`) plus `DbContext` extension methods layered on Core's `UpsertAsync`; it never talks to Discord. `Persistord.Protection` turns a `[Protected]` string property into an encrypted column via a value converter built from an `IDataProtector`. The `[Protected]` attribute itself lives in Core so `Persistord.Managed` can annotate a webhook token without depending on the Protection package.

**Tech Stack:** .NET 10, EF Core 10 (floor `[10.0.0, 11.0.0)`), `Microsoft.AspNetCore.DataProtection.Abstractions`, `Microsoft.EntityFrameworkCore.Sqlite`, xunit 2.9.

**Spec:** `docs/superpowers/specs/2026-09-08-persistord-bot-fit-design.md` (§3, §6, §7, §8, §9)

**Prerequisite:** the sibling plan `2026-09-08-persistord-bot-fit-core.md` must be complete. This plan uses `IGuildScoped`, `ICreatedAt`/`IUpdatedAt`, `TimestampInterceptor`, `UpsertAsync`, `ApplyGuildRoot` and `PurgeGuildAsync` from it.

---

## Global Constraints

Identical to the Core plan; repeated here because tasks are executed by workers who see only their own task.

- **Target framework** `net10.0`. `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`.
- **`TreatWarningsAsErrors=true`** with `AnalysisLevel=latest-all` plus NetAnalyzers, Roslynator, SonarAnalyzer, VS-Threading. A warning fails the build.
- **`GenerateDocumentationFile=true`** for `src/**`: every public and protected member needs an XML doc comment.
- **`CA2007`**: in `src/**`, every awaited task needs `.ConfigureAwait(false)`. Not required under `tests/**` or `samples/**`.
- **EF Core floor is frozen** at `[10.0.0, 11.0.0)`. New shipped dependencies use the same range form.
- **Central Package Management**: versions in `Directory.Packages.props`; `<PackageReference>` carries no `Version` — except the deliberate `VersionOverride` in Task 1.
- **Style**: file-scoped namespaces, 4-space indent, 120-column limit, opening brace on its own line.
- **Null guards**: `ArgumentNullException.ThrowIfNull(...)` on public reference parameters; the repo pins them with null-guard tests.
- **Every new packable project needs**: `IsPackable=true`, `PackageId`, `Description`, `Authors=HandyS11`, `PackageLicenseExpression=MIT`, `PackageReadmeFile=README.md`, `PackageTags`, and the `<None Include="…/README.md" Pack="true" PackagePath="\" …/>` item — copy the shape from `src/Persistord.Core/Persistord.Core.csproj`.
- **Every new package needs five wiring edits**: `Persistord.slnx`, a pack step in `.github/workflows/CD.yml`, a matrix entry in `.github/workflows/Mutation.yml`, a `stryker-config.json` in its test project, and `Directory.Packages.props` for any new dependency.
- **Build/test commands** use `dtk dotnet …` (a filtering wrapper around `dotnet`); plain `dotnet …` is identical and is what CI runs.
- **Commit style**: conventional commits.

---

## File Structure

**Created — `Persistord.Testing`:**

- `src/Persistord.Testing/Persistord.Testing.csproj`, `README.md`
- `src/Persistord.Testing/SqliteTestDatabase.cs` — `Private()` / `Shared()` in-memory databases, `TestSchema` enum
- `src/Persistord.Testing/UniqueModelCacheKeyFactory.cs` — public, so per-test model variants stay visible to coverage and mutation tools
- `src/Persistord.Testing/ModelAssertions.cs` — `AssertUniqueIndex`, `AssertCascade`, `AssertSnowflakeKey`
- `tests/Persistord.Testing.Tests/**`

**Created — `Persistord.Managed`:**

- `src/Persistord.Managed/Persistord.Managed.csproj`, `README.md`
- `src/Persistord.Managed/ManagedScope.cs`
- `src/Persistord.Managed/Entities/ManagedResource.cs`, `ManagedCategory.cs`, `ManagedChannel.cs`, `ManagedMessage.cs`, `ManagedWebhook.cs`
- `src/Persistord.Managed/Configurations/ManagedResourceConfiguration.cs` (internal shared shape) + one configuration per entity
- `src/Persistord.Managed/ModelBuilderExtensions.cs` — `ApplyManagedModule()`
- `src/Persistord.Managed/ManagedStoreExtensions.cs` — `UpsertManagedAsync`, `FindManagedAsync`, `DeleteScopeAsync`, `ListScopesAsync`
- `tests/Persistord.Managed.Tests/**`

**Created — `Persistord.Protection`:**

- `src/Persistord.Protection/Persistord.Protection.csproj`, `README.md`
- `src/Persistord.Protection/ProtectionPurposes.cs`, `ProtectedStringConverter.cs`, `ProtectionModelBuilderExtensions.cs`
- `tests/Persistord.Protection.Tests/**`

**Created — Core (one file, non-breaking):** `src/Persistord.Core/Abstractions/ProtectedAttribute.cs`

**Created — docs:** `docs/articles/managed-resources.md`, `protection.md`, `testing.md`, `providers.md`, `guild-lifecycle.md`, `upsert.md`

**Modified:** `Persistord.slnx`, `Directory.Packages.props`, `.github/workflows/CD.yml`, `.github/workflows/Mutation.yml`, `.github/dependabot.yml`, `README.md`, `docs/articles/toc.yml`, `docs/articles/recipes.md`, `docs/articles/getting-started.md`, `docs/articles/troubleshooting.md`, and the four existing test projects (Task 3).

**Deliberately unchanged:** the `Persistord` meta package keeps bundling only Core + Messages + History — the three new packages are opt-in and two of them are not runtime dependencies of a bot's data model. `Persistord.Messages`, `Persistord.History` and `Persistord.Adapters.DiscordNet` are untouched (spec §9).

---

## Task 1: `Persistord.Testing` — in-memory SQLite fixtures

**Files:**
- Create: `src/Persistord.Testing/Persistord.Testing.csproj`, `src/Persistord.Testing/README.md`, `src/Persistord.Testing/SqliteTestDatabase.cs`, `src/Persistord.Testing/UniqueModelCacheKeyFactory.cs`
- Create: `tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj`, `stryker-config.json`, `FixtureContext.cs`, `DesignTimeFixtureContextFactory.cs`, `SqliteTestDatabaseTests.cs`
- Modify: `Persistord.slnx`, `.github/workflows/CD.yml`, `.github/workflows/Mutation.yml`

**Interfaces:**
- Consumes: `Persistord.Core.DiscordDbContext`.
- Produces:
  - `enum Persistord.Testing.TestSchema { Migrate, EnsureCreated }`
  - `sealed class SqliteTestDatabase : IAsyncDisposable, IDisposable` with `static Private(TestSchema schema = TestSchema.Migrate)`, `static Shared(string? name = null, TestSchema schema = TestSchema.Migrate)`, `string ConnectionString { get; }`, `TestSchema Schema { get; }`, `DbContextOptions<TContext> Options<TContext>(params IInterceptor[] interceptors)`, `TContext CreateContext<TContext>(Func<DbContextOptions<TContext>, TContext> factory, params IInterceptor[] interceptors)`
  - `sealed class UniqueModelCacheKeyFactory : IModelCacheKeyFactory` (public)

- [ ] **Step 1: Scaffold the project and wire it up**

Create `src/Persistord.Testing/Persistord.Testing.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.Testing</PackageId>
    <Description>In-memory SQLite fixtures and EF Core model assertions for testing Persistord-based contexts.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>discord;efcore;persistence;testing;sqlite</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <!-- VersionOverride, not the central pin: this is a SHIPPED dependency, so it must be a
         floor a consumer can satisfy with any 10.0.x, exactly like the EF Core references in
         Persistord.Core. The central 10.0.11 pin stays as-is so test and sample projects keep
         exercising the latest SQLite provider. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" VersionOverride="[10.0.0, 11.0.0)" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Persistord.Core/Persistord.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="$(MSBuildProjectDirectory)/README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

Add to `Persistord.slnx`, in the `/src/` folder: `<Project Path="src/Persistord.Testing/Persistord.Testing.csproj" />`, and in `/tests/`: `<Project Path="tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj" />`.

Add to `.github/workflows/CD.yml`, after the `Persistord.Adapters.DiscordNet` pack step:

```yaml
      - name: Pack NuGet Package Persistord.Testing
        run: cd ./src/Persistord.Testing/ && dotnet pack --configuration Release -p:Version=$VERSION
```

Add to the `.github/workflows/Mutation.yml` matrix:

```yaml
          - source: Persistord.Testing.csproj
            testdir: tests/Persistord.Testing.Tests
```

Create `tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj` by copying `tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj` and changing the project references to `../../src/Persistord.Testing/Persistord.Testing.csproj`, plus a `<PackageReference Include="Microsoft.EntityFrameworkCore.Design" />` (needed by `dotnet ef` in Step 5).

Create `tests/Persistord.Testing.Tests/stryker-config.json` by copying the Core one and changing `"module"` to `"Persistord.Testing"`.

- [ ] **Step 2: Write the failing tests**

Create `tests/Persistord.Testing.Tests/FixtureContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Persistord.Testing.Tests;

public sealed class WidgetRow
{
    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public string Key { get; set; } = string.Empty;
}

public sealed class FixtureContext(DbContextOptions<FixtureContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<WidgetRow> Widgets => Set<WidgetRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<WidgetRow>().HasIndex(w => new
        {
            w.GuildId, w.Key
        }).IsUnique();
    }
}
```

Create `tests/Persistord.Testing.Tests/SqliteTestDatabaseTests.cs`:

```csharp
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

        await using var firstContext = first.CreateContext(o => new FixtureContext(o));
        await using var secondContext = second.CreateContext(o => new FixtureContext(o));

        firstContext.Widgets.Add(Widget("a"));
        await firstContext.SaveChangesAsync();

        Assert.Single(await firstContext.Widgets.ToListAsync());
        Assert.Empty(await secondContext.Widgets.ToListAsync());
    }

    [Fact]
    public async Task Private_shares_one_connection_across_contexts()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        await using (var writer = database.CreateContext(o => new FixtureContext(o)))
        {
            writer.Widgets.Add(Widget("a"));
            await writer.SaveChangesAsync();
        }

        await using var reader = database.CreateContext(o => new FixtureContext(o));
        Assert.Single(await reader.Widgets.ToListAsync());
    }

    [Fact]
    public async Task Shared_lets_independent_connections_see_the_same_rows()
    {
        await using var database = SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated);

        await using (var writer = database.CreateContext(o => new FixtureContext(o)))
        {
            writer.Widgets.Add(Widget("a"));
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

        await using var context = database.CreateContext(o => new FixtureContext(o));
        context.Widgets.Add(Widget("a"));
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
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj`
Expected: compile error — `SqliteTestDatabase` does not exist.

- [ ] **Step 4: Write the fixture**

Create `src/Persistord.Testing/UniqueModelCacheKeyFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Testing;

/// <summary>
/// Returns a unique key per call so EF never reuses a cached model. Without it the model builds
/// once per context type for the whole test run, which hides per-test coverage of the entity
/// configurations, lets configuration mutants survive, and makes two contexts configured
/// differently silently share one model. <see cref="SqliteTestDatabase"/> installs it for you.
/// </summary>
public sealed class UniqueModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (context.GetType(), designTime, Guid.NewGuid());
    }
}
```

Create `src/Persistord.Testing/SqliteTestDatabase.cs`:

```csharp
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
    Migrate,

    /// <summary>Create the schema straight from the model. Fast, and blind to migration drift.</summary>
    EnsureCreated,
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

        return builder.Options;
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

        var context = factory(Options<TContext>(interceptors));
        EnsureSchema(context);
        return context;
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _connection.DisposeAsync().ConfigureAwait(false);

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
```

- [ ] **Step 5: Add a real migration so the `Migrate` path is exercised**

`TestSchema.Migrate` is the default, so it needs a test with genuine migrations. Create `tests/Persistord.Testing.Tests/DesignTimeFixtureContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Persistord.Testing.Tests;

public sealed class DesignTimeFixtureContextFactory : IDesignTimeDbContextFactory<FixtureContext>
{
    public FixtureContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FixtureContext>().UseSqlite("DataSource=design.db").Options);
}
```

Generate the migration:

```bash
dotnet tool restore
dotnet ef migrations add Initial \
  --project tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj \
  --startup-project tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj
```

Then add the test that proves migrations run:

```csharp
    [Fact]
    public async Task Migrate_applies_the_committed_migrations()
    {
        await using var database = SqliteTestDatabase.Private();
        await using var context = database.CreateContext(o => new FixtureContext(o));

        context.Widgets.Add(Widget("a"));
        await context.SaveChangesAsync();

        Assert.Single(await context.Widgets.ToListAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }
```

- [ ] **Step 6: Run the tests**

Run: `dtk dotnet test tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj`
Expected: PASS, all seven.

- [ ] **Step 7: Write the package README**

Create `src/Persistord.Testing/README.md` following the shape of `src/Persistord.Core/README.md`: what it is (test-only helpers, safe to reference from test projects), a `SqliteTestDatabase.Private` example, a `Shared` example with the note about DI scopes and racing writers, the `TestSchema` trade-off in two sentences, `UniqueModelCacheKeyFactory` and why it matters for coverage and mutation scores, and a License section.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(testing): add Persistord.Testing with in-memory SQLite fixtures"
```

---

## Task 2: `Persistord.Testing` — model assertions

**Files:**
- Create: `src/Persistord.Testing/ModelAssertions.cs`, `tests/Persistord.Testing.Tests/ModelAssertionsTests.cs`
- Modify: `src/Persistord.Testing/README.md`

**Interfaces:**
- Consumes: `SqliteTestDatabase` (Task 1).
- Produces, all on `DbContext` and all throwing `InvalidOperationException` on failure:
  - `void ModelAssertions.AssertUniqueIndex<TEntity>(this DbContext context, params string[] propertyNames) where TEntity : class`
  - `void ModelAssertions.AssertCascade<TChild, TParent>(this DbContext context) where TChild : class where TParent : class`
  - `void ModelAssertions.AssertSnowflakeKey<TEntity>(this DbContext context) where TEntity : class`

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Testing.Tests/ModelAssertionsTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Testing.Tests;

public class ModelAssertionsTests
{
    private static AssertionContext Context(SqliteTestDatabase database) =>
        database.CreateContext(o => new AssertionContext(o));

    [Fact]
    public void AssertUniqueIndex_passes_on_a_matching_index()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        context.AssertUniqueIndex<ScopedRow>(nameof(ScopedRow.GuildId), nameof(ScopedRow.Key));
    }

    [Fact]
    public void AssertUniqueIndex_names_the_entity_and_the_columns_when_it_fails()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertUniqueIndex<ScopedRow>(nameof(ScopedRow.Key)));

        Assert.Contains(nameof(ScopedRow), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ScopedRow.Key), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertUniqueIndex_rejects_a_non_unique_index()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        Assert.Throws<InvalidOperationException>(() =>
            context.AssertUniqueIndex<ScopedRow>(nameof(ScopedRow.Label)));
    }

    [Fact]
    public void AssertCascade_passes_when_the_child_cascades_from_the_parent()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        context.AssertCascade<ScopedRow, GuildEntity>();
    }

    [Fact]
    public void AssertCascade_fails_when_there_is_no_relationship()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        Assert.Throws<InvalidOperationException>(() => context.AssertCascade<UnrelatedRow, GuildEntity>());
    }

    [Fact]
    public void AssertSnowflakeKey_passes_on_a_ulong_key()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        context.AssertSnowflakeKey<GuildEntity>();
    }

    [Fact]
    public void AssertSnowflakeKey_fails_on_a_surrogate_key()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<UnrelatedRow>());
    }

    [Fact]
    public void Assertions_fail_loudly_for_an_unmapped_type()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<NotMapped>());
    }

    public sealed class ScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;
    }

    public sealed class UnrelatedRow
    {
        public long Id { get; set; }
    }

    public sealed class NotMapped
    {
        public long Id { get; set; }
    }

    public sealed class AssertionContext(DbContextOptions<AssertionContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

        public DbSet<ScopedRow> Scoped => Set<ScopedRow>();

        public DbSet<UnrelatedRow> Unrelated => Set<UnrelatedRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScopedRow>().HasIndex(r => new
            {
                r.GuildId, r.Key
            }).IsUnique();
            modelBuilder.Entity<ScopedRow>().HasIndex(r => r.Label);
            modelBuilder.ApplyGuildRoot();
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj --filter FullyQualifiedName~ModelAssertionsTests`
Expected: compile error — `AssertUniqueIndex` and friends do not exist.

- [ ] **Step 3: Write the assertions**

Create `src/Persistord.Testing/ModelAssertions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Persistord.Testing;

/// <summary>
/// Assertions about the shape of an EF model, so a schema test is one line instead of a
/// seed-mutate-assert round trip against the database. Failures throw
/// <see cref="InvalidOperationException"/> with a message naming the entity and what was expected:
/// the package stays free of any test-framework dependency, so it works with xunit, NUnit and
/// MSTest alike.
/// </summary>
public static class ModelAssertions
{
    /// <summary>Asserts that the entity has a unique index over exactly these properties, in order.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="context">A context whose model to inspect.</param>
    /// <param name="propertyNames">The index's properties, in order.</param>
    public static void AssertUniqueIndex<TEntity>(this DbContext context, params string[] propertyNames)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(propertyNames);

        var entityType = FindEntityType<TEntity>(context);
        var found = entityType.GetIndexes().Any(index =>
            index.IsUnique
            && index.Properties.Select(p => p.Name).SequenceEqual(propertyNames, StringComparer.Ordinal));

        if (!found)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} has no unique index over ({string.Join(", ", propertyNames)}). "
                + $"Indexes found: {DescribeIndexes(entityType)}.");
        }
    }

    /// <summary>
    /// Asserts that the child has a foreign key to the parent that deletes with
    /// <see cref="DeleteBehavior.Cascade"/> — the one-line replacement for an
    /// insert-parent, insert-child, delete-parent, assert-empty test.
    /// </summary>
    /// <typeparam name="TChild">The dependent entity type.</typeparam>
    /// <typeparam name="TParent">The principal entity type.</typeparam>
    /// <param name="context">A context whose model to inspect.</param>
    public static void AssertCascade<TChild, TParent>(this DbContext context)
        where TChild : class
        where TParent : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var child = FindEntityType<TChild>(context);
        var foreignKey = child.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(TParent));

        if (foreignKey is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TChild).Name} has no foreign key to {typeof(TParent).Name}.");
        }

        if (foreignKey.DeleteBehavior != DeleteBehavior.Cascade)
        {
            throw new InvalidOperationException(
                $"{typeof(TChild).Name} -> {typeof(TParent).Name} deletes with "
                + $"{foreignKey.DeleteBehavior}, not {nameof(DeleteBehavior.Cascade)}.");
        }
    }

    /// <summary>
    /// Asserts that the entity's primary key is an unsigned 64-bit value the caller supplies: at
    /// least one <see cref="ulong"/> key property, every one of them
    /// <see cref="ValueGenerated.Never"/> and stored as a <see cref="long"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="context">A context whose model to inspect.</param>
    public static void AssertSnowflakeKey<TEntity>(this DbContext context)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var entityType = FindEntityType<TEntity>(context);
        var key = entityType.FindPrimaryKey()
                  ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no primary key.");

        var snowflakes = key.Properties
            .Where(p => p.ClrType == typeof(ulong) || p.ClrType == typeof(ulong?))
            .ToList();

        if (snowflakes.Count == 0)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name}'s primary key has no ulong property: "
                + $"({string.Join(", ", key.Properties.Select(p => p.Name))}).");
        }

        foreach (var property in snowflakes)
        {
            if (property.ValueGenerated != ValueGenerated.Never)
            {
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name}.{property.Name} is {property.ValueGenerated}, "
                    + $"not {nameof(ValueGenerated.Never)}: EF would treat it as an identity column.");
            }

            var providerType = property.GetValueConverter()?.ProviderClrType;
            if (providerType != typeof(long) && providerType != typeof(long?))
            {
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name}.{property.Name} is not stored as a long "
                    + $"(provider type: {providerType?.Name ?? "none"}). Is the context a DiscordDbContext?");
            }
        }
    }

    private static IEntityType FindEntityType<TEntity>(DbContext context) =>
        context.Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");

    private static string DescribeIndexes(IEntityType entityType)
    {
        var indexes = entityType.GetIndexes()
            .Select(i => $"{(i.IsUnique ? "unique " : string.Empty)}({string.Join(", ", i.Properties.Select(p => p.Name))})")
            .ToList();

        return indexes.Count == 0 ? "none" : string.Join(", ", indexes);
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dtk dotnet test tests/Persistord.Testing.Tests/Persistord.Testing.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Document the assertions**

Add a `## Model assertions` section to `src/Persistord.Testing/README.md`: the three signatures, one example each, and the sentence that they throw `InvalidOperationException` so no test framework is required.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(testing): add model-shape assertions"
```

---

## Task 3: Dogfood the fixtures in the existing test projects

**Files:**
- Delete: `tests/Persistord.Core.Tests/UniqueModelCacheKeyFactory.cs`, `tests/Persistord.Messages.Tests/UniqueModelCacheKeyFactory.cs`, `tests/Persistord.History.Tests/UniqueModelCacheKeyFactory.cs`, `tests/Persistord.Core.Tests/SharedSqliteDatabase.cs`
- Modify: `tests/Persistord.Core.Tests/SqliteFixture.cs`, `tests/Persistord.Messages.Tests/TestContext.cs`, `tests/Persistord.History.Tests/TestContext.cs`, the four test `.csproj` files, and every call site of the fixtures
- Modify: `tests/Persistord.Core.Tests/UpsertTests.cs` (drop the local shared-database helper)

**Interfaces:**
- Consumes: `SqliteTestDatabase`, `UniqueModelCacheKeyFactory` (Task 1).
- Produces: `SqliteFixture.Create<TContext>(…)` returning `(SqliteTestDatabase Database, TContext Context)`; `Persistord.Messages.Tests.TestContext.Create(bool filterDeleted = true)` and `Persistord.History.Tests.TestContext.Create()` returning the same tuple shape.

**Why this task exists:** the spec (§7.1) counts three copies of `UniqueModelCacheKeyFactory` in this repo's own test tree and none of it shipped. Fixing that is also the only real integration test the new package gets.

- [ ] **Step 1: Reference the package from every test project**

Add to `tests/Persistord.Core.Tests`, `tests/Persistord.Messages.Tests`, `tests/Persistord.History.Tests`, `tests/Persistord.Adapters.DiscordNet.Tests`:

```xml
    <ProjectReference Include="../../src/Persistord.Testing/Persistord.Testing.csproj" />
```

- [ ] **Step 2: Rewrite `SqliteFixture` over the package**

Replace `tests/Persistord.Core.Tests/SqliteFixture.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Testing;

namespace Persistord.Core.Tests;

/// <summary>Creates a context over a private in-memory SQLite database.</summary>
public static class SqliteFixture
{
    public static (SqliteTestDatabase Database, TestContext Context) Create() =>
        Create(options => new TestContext(options));

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
```

Delete `tests/Persistord.Core.Tests/UniqueModelCacheKeyFactory.cs`.

- [ ] **Step 3: Update the call sites**

Every fixture call site names the first tuple element `connection`. Rename it:

```bash
grep -rl 'var (connection, context)' tests | xargs sed -i 's/var (connection, context)/var (database, context)/; s/using (connection)/using (database)/'
```

Then fix what the compiler still flags — the `Persistord.Messages.Tests` and `Persistord.History.Tests` `TestContext.Create()` helpers, which must now return `(SqliteTestDatabase, TestContext)`:

```csharp
    public static (SqliteTestDatabase Database, TestContext Context) Create(bool filterDeleted = true)
    {
        var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        return (database, database.CreateContext(options => new TestContext(options, filterDeleted)));
    }
```

(The History variant takes no parameter.) Delete both projects' `UniqueModelCacheKeyFactory.cs` and drop the now-unused `Microsoft.Data.Sqlite`/`Microsoft.EntityFrameworkCore.Infrastructure` usings.

- [ ] **Step 4: Replace the Core tests' shared-database helper**

In `tests/Persistord.Core.Tests/UpsertTests.cs`, replace `new SharedSqliteDatabase()` with `SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated)`, and `database.CreateContext(o => new UpsertContext(o), interference)` keeps working — the signatures match by design. Delete `tests/Persistord.Core.Tests/SharedSqliteDatabase.cs`.

- [ ] **Step 5: Run every test project**

Run: `dtk dtk dotnet test Persistord.slnx`
Expected: PASS across all projects, same test count as before plus the new packages' tests.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(tests): use Persistord.Testing instead of three copied fixtures"
```

---

## Task 4: `Persistord.Managed` — entities and model

**Files:**
- Create: `src/Persistord.Core/Abstractions/ProtectedAttribute.cs`
- Create: `src/Persistord.Managed/Persistord.Managed.csproj`, `README.md`, `ManagedScope.cs`, `Entities/ManagedResource.cs`, `Entities/ManagedCategory.cs`, `Entities/ManagedChannel.cs`, `Entities/ManagedMessage.cs`, `Entities/ManagedWebhook.cs`, `Configurations/ManagedResourceConfiguration.cs`, `Configurations/ManagedCategoryConfiguration.cs`, `Configurations/ManagedChannelConfiguration.cs`, `Configurations/ManagedMessageConfiguration.cs`, `Configurations/ManagedWebhookConfiguration.cs`, `ModelBuilderExtensions.cs`
- Create: `tests/Persistord.Managed.Tests/Persistord.Managed.Tests.csproj`, `stryker-config.json`, `ManagedContext.cs`, `ManagedModelTests.cs`
- Modify: `Persistord.slnx`, `.github/workflows/CD.yml`, `.github/workflows/Mutation.yml`

**Interfaces:**
- Consumes: `IGuildScoped`, `ICreatedAt`, `IUpdatedAt` (Core plan Tasks 4–5), `SqliteTestDatabase` + `ModelAssertions` (Tasks 1–2), `ApplyGuildRoot` (Core plan Task 6).
- Produces:
  - `abstract class Persistord.Managed.Entities.ManagedResource : IGuildScoped, ICreatedAt, IUpdatedAt` with `long Id`, `ulong GuildId`, `string Scope`, `string Key`, `ulong DiscordId`, `DateTimeOffset CreatedAt`, `DateTimeOffset UpdatedAt` (all settable)
  - `sealed class ManagedCategory : ManagedResource`
  - `sealed class ManagedChannel : ManagedResource` (+ `ulong? ParentDiscordId`)
  - `sealed class ManagedMessage : ManagedResource` (+ `ulong ChannelDiscordId`, `string? ContentHash`)
  - `sealed class ManagedWebhook : ManagedResource` (+ `ulong ChannelDiscordId`, `[Protected] string Token`)
  - `static class ManagedScope` with `const string Global = ""` and `static string Normalize(string? scope)`
  - `ModelBuilder Persistord.Managed.ModelBuilderExtensions.ApplyManagedModule(this ModelBuilder modelBuilder)`
  - `sealed class Persistord.Core.Abstractions.ProtectedAttribute : Attribute`

**Design decision — `Scope` is a non-nullable `string`, not a `string?` with a converter.** The spec (§3) proposed exposing `Scope` as `string?` and persisting `""` through a value converter, to dodge SQL's "NULLs are distinct" rule in the unique index. The database shape here is exactly what the spec asked for — non-nullable, `""` for guild-wide — but the CLR property is non-nullable too, because a value converter does *not* travel through null comparisons: with a converter, a consumer's `Where(m => m.Scope == null)` translates to `IS NULL` and silently matches zero rows, which is a worse footgun than the one being avoided. Every helper accepts `string?` and normalizes `null` to `ManagedScope.Global`, so callers still get the "omit the scope" ergonomics.

**Design decision — table names are pinned.** `ToTable("ManagedCategories")` and friends. The consumer declares the `DbSet` properties, and EF would otherwise name the tables after those properties; these tables belong to the module, so their names should not move when someone renames a property.

- [ ] **Step 1: Scaffold the project and wire it up**

Create `src/Persistord.Managed/Persistord.Managed.csproj` copying the shape of `src/Persistord.Core/Persistord.Core.csproj`, with:

```xml
    <PackageId>Persistord.Managed</PackageId>
    <Description>EF Core entities and helpers for Discord resources a bot creates and owns: categories, channels, anchored messages, and webhooks.</Description>
    <PackageTags>discord;efcore;persistence;bot</PackageTags>
```

and a single `<ProjectReference Include="../Persistord.Core/Persistord.Core.csproj" />` (no `PackageReference` — EF comes transitively from Core).

Add both new projects to `Persistord.slnx`; add a `Pack NuGet Package Persistord.Managed` step to `CD.yml`; add the `Persistord.Managed.csproj` / `tests/Persistord.Managed.Tests` pair to the `Mutation.yml` matrix; create `tests/Persistord.Managed.Tests/Persistord.Managed.Tests.csproj` (copy the Core tests project, reference `src/Persistord.Managed` and `src/Persistord.Testing`) and its `stryker-config.json` with `"module": "Persistord.Managed"`.

- [ ] **Step 2: Write the failing tests**

Create `tests/Persistord.Managed.Tests/ManagedContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.Managed;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Tests;

public sealed class ManagedContext : Persistord.Core.DiscordDbContext
{
    private readonly bool _guildRoot;

    public ManagedContext(DbContextOptions<ManagedContext> options, bool guildRoot = false)
        : base(options) => _guildRoot = guildRoot;

    public ManagedContext(DbContextOptions<ManagedContext> options, TimeProvider timeProvider, bool guildRoot = false)
        : base(options, timeProvider) => _guildRoot = guildRoot;

    public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

    public DbSet<ManagedCategory> Categories => Set<ManagedCategory>();

    public DbSet<ManagedChannel> Channels => Set<ManagedChannel>();

    public DbSet<ManagedMessage> Messages => Set<ManagedMessage>();

    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();

        if (_guildRoot)
        {
            modelBuilder.ApplyGuildRoot();
        }
    }
}
```

Create `tests/Persistord.Managed.Tests/ManagedModelTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.Managed.Entities;
using Persistord.Testing;
using Xunit;

namespace Persistord.Managed.Tests;

public class ManagedModelTests
{
    [Theory]
    [InlineData(typeof(ManagedCategory))]
    [InlineData(typeof(ManagedChannel))]
    [InlineData(typeof(ManagedMessage))]
    [InlineData(typeof(ManagedWebhook))]
    public void Every_resource_is_unique_per_guild_scope_and_key(Type resource)
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext(o => new ManagedContext(o));

        var entityType = context.Model.FindEntityType(resource)!;
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.IsUnique
                     && index.Properties.Select(p => p.Name).SequenceEqual(["GuildId", "Scope", "Key"]);
    }

    [Fact]
    public void Managed_message_indexes_the_discord_id()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext(o => new ManagedContext(o));

        Assert.Contains(
            context.Model.FindEntityType(typeof(ManagedMessage))!.GetIndexes(),
            index => index.Properties.Select(p => p.Name).SequenceEqual([nameof(ManagedMessage.DiscordId)]));
    }

    [Fact]
    public void Scope_is_a_required_column()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext(o => new ManagedContext(o));

        var scope = context.Model.FindEntityType(typeof(ManagedCategory))!
            .FindProperty(nameof(ManagedCategory.Scope))!;

        Assert.False(scope.IsNullable);
        Assert.Equal(64, scope.GetMaxLength());
    }

    [Fact]
    public void Resources_cascade_from_the_guild_root()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext(o => new ManagedContext(o, guildRoot: true));

        context.AssertCascade<ManagedCategory, GuildEntity>();
        context.AssertCascade<ManagedChannel, GuildEntity>();
        context.AssertCascade<ManagedMessage, GuildEntity>();
        context.AssertCascade<ManagedWebhook, GuildEntity>();
    }

    [Fact]
    public void Tables_are_named_by_the_module_not_by_the_dbset()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext(o => new ManagedContext(o));

        Assert.Equal("ManagedCategories", context.Model.FindEntityType(typeof(ManagedCategory))!.GetTableName());
        Assert.Equal("ManagedChannels", context.Model.FindEntityType(typeof(ManagedChannel))!.GetTableName());
        Assert.Equal("ManagedMessages", context.Model.FindEntityType(typeof(ManagedMessage))!.GetTableName());
        Assert.Equal("ManagedWebhooks", context.Model.FindEntityType(typeof(ManagedWebhook))!.GetTableName());
    }

    [Fact]
    public async Task A_row_round_trips_with_a_high_bit_snowflake_and_no_scope()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        context.Messages.Add(new ManagedMessage
        {
            GuildId = ulong.MaxValue,
            Key = "dashboard",
            ChannelDiscordId = 1UL,
            DiscordId = ulong.MaxValue - 1UL,
            ContentHash = "abc",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var row = await context.Messages.SingleAsync();
        Assert.Equal(ulong.MaxValue, row.GuildId);
        Assert.Equal(ulong.MaxValue - 1UL, row.DiscordId);
        Assert.Equal(ManagedScope.Global, row.Scope);
        Assert.Equal("abc", row.ContentHash);
    }

    [Fact]
    public void ApplyManagedModule_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyManagedModule());

    [Fact]
    public void Configurations_throw_on_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ManagedCategoryConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new ManagedChannelConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new ManagedMessageConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new ManagedWebhookConfiguration().Configure(null!));
    }
}
```

Fix the unbalanced parenthesis in the first test while typing it (`SequenceEqual([...]))` closes the lambda, then the `Assert.Contains` call), and add `using Persistord.Managed.Configurations;` for the null-guard test.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Managed.Tests/Persistord.Managed.Tests.csproj`
Expected: compile error — none of the entities exist.

- [ ] **Step 4: Write the attribute, the scope helper and the entities**

Create `src/Persistord.Core/Abstractions/ProtectedAttribute.cs`:

```csharp
namespace Persistord.Core.Abstractions;

/// <summary>
/// Marks a <see cref="string"/> property as a secret that belongs encrypted at rest. The attribute
/// is inert on its own: reference <c>Persistord.Protection</c> and call
/// <c>modelBuilder.ApplyProtection(provider)</c> to install the value converter that encrypts it.
/// Without that call the property is stored as plaintext.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtectedAttribute : Attribute;
```

Create `src/Persistord.Managed/ManagedScope.cs`:

```csharp
namespace Persistord.Managed;

/// <summary>The scope partition of a managed resource.</summary>
public static class ManagedScope
{
    /// <summary>
    /// The guild-wide scope: the empty string. Not <c>null</c> — SQL unique indexes treat NULLs as
    /// distinct on SQLite and PostgreSQL alike, so a nullable scope column would happily accept two
    /// rows for the same <c>(guild, key)</c>.
    /// </summary>
    public const string Global = "";

    /// <summary>Turns a caller's optional scope into a storable one.</summary>
    /// <param name="scope">The caller's scope, or <c>null</c> for guild-wide.</param>
    /// <returns><paramref name="scope"/>, or <see cref="Global"/> when it is <c>null</c>.</returns>
    public static string Normalize(string? scope) => scope ?? Global;
}
```

Create `src/Persistord.Managed/Entities/ManagedResource.cs`:

```csharp
using Persistord.Core.Abstractions;

namespace Persistord.Managed.Entities;

/// <summary>
/// The shared shape of every Discord resource the bot creates and owns: a surrogate key, the owning
/// guild, an opaque consumer <see cref="Scope"/>, the consumer's stable <see cref="Key"/>, the
/// snowflake Discord handed back, and timestamps. Not an entity type itself — EF maps only the
/// concrete resources, each to its own table.
/// </summary>
public abstract class ManagedResource : IGuildScoped, ICreatedAt, IUpdatedAt
{
    /// <summary>
    /// Surrogate primary key. Managed resources carry one so callers have something stable and
    /// orderable to sort by: SQLite cannot <c>ORDER BY</c> a <see cref="DateTimeOffset"/> column.
    /// </summary>
    public long Id { get; set; }

    /// <summary>The owning guild snowflake id.</summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// An opaque partition inside the guild, chosen by the consumer — a game-server id, a playlist
    /// id, whatever its resources hang off. Not a foreign key, so the module never couples to a
    /// consumer table. <see cref="ManagedScope.Global"/> means guild-wide.
    /// </summary>
    public string Scope { get; set; } = ManagedScope.Global;

    /// <summary>The consumer's stable key for this resource, unique within a guild and scope.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The snowflake of the Discord object the bot created.</summary>
    public ulong DiscordId { get; set; }

    /// <summary>When the record was first written.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the record was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Create the four concrete entities:

```csharp
namespace Persistord.Managed.Entities;

/// <summary>A category the bot created, remembered by the consumer's key.</summary>
public sealed class ManagedCategory : ManagedResource;
```

```csharp
namespace Persistord.Managed.Entities;

/// <summary>A channel the bot created.</summary>
public sealed class ManagedChannel : ManagedResource
{
    /// <summary>The category or parent channel it was created under, if any.</summary>
    public ulong? ParentDiscordId { get; set; }
}
```

```csharp
namespace Persistord.Managed.Entities;

/// <summary>
/// A message the bot posted and edits in place — a dashboard, a per-item embed, a prompt whose
/// buttons must survive a restart.
/// </summary>
public sealed class ManagedMessage : ManagedResource
{
    /// <summary>The channel the message lives in.</summary>
    public ulong ChannelDiscordId { get; set; }

    /// <summary>
    /// A hash of the payload last rendered into this message, for render gating: hash the next
    /// payload, compare, and skip the edit when it matches. <c>null</c> until the consumer sets it.
    /// The module never computes it — the payload is the consumer's.
    /// </summary>
    public string? ContentHash { get; set; }
}
```

```csharp
using Persistord.Core.Abstractions;

namespace Persistord.Managed.Entities;

/// <summary>
/// A webhook the bot created. Persisting it is what stops the bot re-discovering webhooks by name
/// on every boot — a rename creates a silent duplicate.
/// </summary>
public sealed class ManagedWebhook : ManagedResource
{
    /// <summary>The channel the webhook posts to.</summary>
    public ulong ChannelDiscordId { get; set; }

    /// <summary>
    /// The webhook token. Annotated <see cref="ProtectedAttribute"/>, which is inert unless the
    /// consumer references <c>Persistord.Protection</c> and calls <c>ApplyProtection</c>:
    /// <b>without that, this token is stored in plaintext.</b>
    /// </summary>
    [Protected]
    public string Token { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Write the configurations and the module extension**

Create `src/Persistord.Managed/Configurations/ManagedResourceConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>The shape every managed resource shares.</summary>
internal static class ManagedResourceConfiguration
{
    public static void ConfigureCommon<TResource>(EntityTypeBuilder<TResource> builder, string tableName)
        where TResource : ManagedResource
    {
        builder.ToTable(tableName);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.Scope).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(64);
        builder.HasIndex(r => new
        {
            r.GuildId, r.Scope, r.Key
        }).IsUnique();
    }
}
```

Create the four configurations, each public and sealed, following the repo's existing configuration style:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Managed.Entities;

namespace Persistord.Managed.Configurations;

/// <summary>EF Core configuration for <see cref="ManagedCategory"/>.</summary>
public sealed class ManagedCategoryConfiguration : IEntityTypeConfiguration<ManagedCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ManagedCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ManagedResourceConfiguration.ConfigureCommon(builder, "ManagedCategories");
    }
}
```

`ManagedChannelConfiguration` — same, table `"ManagedChannels"`.

`ManagedMessageConfiguration` — table `"ManagedMessages"`, plus:

```csharp
        builder.Property(m => m.ContentHash).HasMaxLength(64);

        // A MessageDeleted gateway event carries only the message id, so the reconciler must be
        // able to find the record by DiscordId without scanning.
        builder.HasIndex(m => m.DiscordId);
```

`ManagedWebhookConfiguration` — table `"ManagedWebhooks"`, plus `builder.Property(w => w.Token).IsRequired();`.

Create `src/Persistord.Managed/ModelBuilderExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Managed.Configurations;

namespace Persistord.Managed;

/// <summary>Model-building extensions that wire the Managed module.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the configurations for the four bot-owned resource types. Call from
    /// <c>OnModelCreating</c>. Declare the <c>DbSet</c>s you actually use — the module maps all four
    /// either way, so an unused one costs an empty table.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyManagedModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder
            .ApplyConfiguration(new ManagedCategoryConfiguration())
            .ApplyConfiguration(new ManagedChannelConfiguration())
            .ApplyConfiguration(new ManagedMessageConfiguration())
            .ApplyConfiguration(new ManagedWebhookConfiguration());
    }
}
```

- [ ] **Step 6: Run the tests**

Run: `dtk dtk dotnet test tests/Persistord.Managed.Tests/Persistord.Managed.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(managed): add bot-owned Discord resource entities"
```

---

## Task 5: `Persistord.Managed` — store helpers

**Files:**
- Create: `src/Persistord.Managed/ManagedStoreExtensions.cs`, `src/Persistord.Managed/README.md`
- Create: `tests/Persistord.Managed.Tests/ManagedStoreTests.cs`

**Interfaces:**
- Consumes: `UpsertAsync` (Core plan Task 3), the Managed entities (Task 4).
- Produces, all extensions on `DbContext`:
  - `Task<TResource> UpsertManagedAsync<TResource>(this DbContext context, ulong guildId, string? scope, string key, ulong discordId, Action<TResource>? configure = null, CancellationToken = default) where TResource : ManagedResource, new()`
  - `Task<TResource?> FindManagedAsync<TResource>(this DbContext context, ulong guildId, string? scope, string key, CancellationToken = default) where TResource : ManagedResource`
  - `Task<int> DeleteScopeAsync(this DbContext context, ulong guildId, string? scope, CancellationToken = default)`
  - `Task<IReadOnlyList<string>> ListScopesAsync(this DbContext context, ulong guildId, CancellationToken = default)`

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Managed.Tests/ManagedStoreTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Managed.Entities;
using Persistord.Testing;
using Xunit;

namespace Persistord.Managed.Tests;

public class ManagedStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Two_upserts_of_the_same_global_key_produce_one_row()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 100UL);
        context.ChangeTracker.Clear();
        var second = await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 200UL);

        Assert.Equal(200UL, second.DiscordId);
        Assert.Equal(ManagedScope.Global, second.Scope);
        Assert.Single(await context.Channels.ToListAsync());
    }

    [Fact]
    public async Task Upsert_applies_the_configure_callback()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        var message = await context.UpsertManagedAsync<ManagedMessage>(
            1UL,
            "server-7",
            "dashboard",
            500UL,
            m =>
            {
                m.ChannelDiscordId = 42UL;
                m.ContentHash = "hash-1";
            });

        Assert.Equal(42UL, message.ChannelDiscordId);
        Assert.Equal("hash-1", message.ContentHash);
        Assert.Equal("server-7", message.Scope);
    }

    [Fact]
    public async Task The_same_key_in_two_scopes_is_two_rows()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 100UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-2", "chat", 200UL);

        Assert.Equal(2, await context.Channels.CountAsync());
    }

    [Fact]
    public async Task Find_returns_the_row_or_null()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-1", "rust", 100UL);
        context.ChangeTracker.Clear();

        Assert.NotNull(await context.FindManagedAsync<ManagedCategory>(1UL, "server-1", "rust"));
        Assert.Null(await context.FindManagedAsync<ManagedCategory>(1UL, null, "rust"));
        Assert.Null(await context.FindManagedAsync<ManagedCategory>(2UL, "server-1", "rust"));
    }

    [Fact]
    public async Task DeleteScope_removes_that_scope_across_all_four_tables_only()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-1", "rust", 1UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 2UL);
        await context.UpsertManagedAsync<ManagedMessage>(1UL, "server-1", "dash", 3UL);
        await context.UpsertManagedAsync<ManagedWebhook>(1UL, "server-1", "bridge", 4UL, w => w.Token = "t");
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-2", "chat", 5UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "log", 6UL);
        await context.UpsertManagedAsync<ManagedChannel>(2UL, "server-1", "chat", 7UL);
        context.ChangeTracker.Clear();

        var deleted = await context.DeleteScopeAsync(1UL, "server-1");

        Assert.Equal(4, deleted);
        Assert.Empty(await context.Categories.ToListAsync());
        Assert.Empty(await context.Messages.ToListAsync());
        Assert.Empty(await context.Webhooks.ToListAsync());
        Assert.Equal(3, await context.Channels.CountAsync()); // server-2, global, other guild
    }

    [Fact]
    public async Task DeleteScope_can_target_the_global_scope()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "log", 1UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 2UL);
        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.DeleteScopeAsync(1UL, null));
        Assert.Equal("server-1", (await context.Channels.SingleAsync()).Scope);
    }

    [Fact]
    public async Task ListScopes_returns_the_distinct_sorted_non_global_scopes()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-2", "chat", 1UL);
        await context.UpsertManagedAsync<ManagedMessage>(1UL, "server-2", "dash", 2UL);
        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-1", "rust", 3UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "log", 4UL);
        await context.UpsertManagedAsync<ManagedChannel>(2UL, "server-9", "chat", 5UL);
        context.ChangeTracker.Clear();

        Assert.Equal(["server-1", "server-2"], await context.ListScopesAsync(1UL));
    }

    [Fact]
    public async Task Upserts_are_stamped_by_the_timestamp_interceptor()
    {
        var clock = new FakeClock(Start);
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o, clock));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 100UL);
        clock.Now = Start.AddMinutes(5);
        var updated = await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 200UL);

        Assert.Equal(Start, updated.CreatedAt);
        Assert.Equal(Start.AddMinutes(5), updated.UpdatedAt);
    }

    [Fact]
    public async Task Helpers_guard_their_arguments()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new ManagedContext(o));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((DbContext)null!).UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 1UL));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.UpsertManagedAsync<ManagedChannel>(1UL, null, string.Empty, 1UL));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).DeleteScopeAsync(1UL, null));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).ListScopesAsync(1UL));
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dtk dotnet test tests/Persistord.Managed.Tests/Persistord.Managed.Tests.csproj --filter FullyQualifiedName~ManagedStoreTests`
Expected: compile error — the helpers do not exist.

- [ ] **Step 3: Write the helpers**

Create `src/Persistord.Managed/ManagedStoreExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.Managed.Entities;

namespace Persistord.Managed;

/// <summary>
/// Read and write helpers for bot-owned resource records. Thin wrappers over Core's
/// <c>UpsertAsync</c>, deliberately not a repository layer: the consumer keeps its own
/// <see cref="DbContext"/> and its own reconciler. Nothing here talks to Discord — "fetch the
/// message, repost it if it 404s, update the record" is the consumer's loop, and these helpers only
/// make the record side trivial.
/// </summary>
public static class ManagedStoreExtensions
{
    /// <summary>
    /// Creates or updates the record for one resource, keyed by
    /// <c>(guildId, scope, key)</c>.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="context">The context to read and write.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="scope">The consumer's partition, or <c>null</c> for guild-wide.</param>
    /// <param name="key">The consumer's stable key. Required.</param>
    /// <param name="discordId">The snowflake Discord returned for the resource.</param>
    /// <param name="configure">
    /// Sets the type-specific columns — a channel's parent, a message's channel and content hash, a
    /// webhook's token. Applied to created and existing rows alike.
    /// </param>
    /// <param name="cancellationToken">Cancels the read and the save.</param>
    /// <returns>The tracked record.</returns>
    public static Task<TResource> UpsertManagedAsync<TResource>(
        this DbContext context,
        ulong guildId,
        string? scope,
        string key,
        ulong discordId,
        Action<TResource>? configure = null,
        CancellationToken cancellationToken = default)
        where TResource : ManagedResource, new()
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var normalized = ManagedScope.Normalize(scope);

        return context.Set<TResource>().UpsertAsync(
            r => r.GuildId == guildId && r.Scope == normalized && r.Key == key,
            () => new TResource
            {
                GuildId = guildId, Scope = normalized, Key = key
            },
            r =>
            {
                r.DiscordId = discordId;
                configure?.Invoke(r);
            },
            cancellationToken);
    }

    /// <summary>Reads one record by its natural key, or <c>null</c> when there is none.</summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="context">The context to read.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="scope">The consumer's partition, or <c>null</c> for guild-wide.</param>
    /// <param name="key">The consumer's stable key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or <c>null</c>.</returns>
    public static Task<TResource?> FindManagedAsync<TResource>(
        this DbContext context,
        ulong guildId,
        string? scope,
        string key,
        CancellationToken cancellationToken = default)
        where TResource : ManagedResource
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var normalized = ManagedScope.Normalize(scope);

        return context.Set<TResource>()
            .SingleOrDefaultAsync(
                r => r.GuildId == guildId && r.Scope == normalized && r.Key == key,
                cancellationToken);
    }

    /// <summary>
    /// Deletes every managed record of one scope, in one transaction. Use it when the thing the
    /// scope stood for is gone — a game server was unpaired, a playlist was deleted.
    /// </summary>
    /// <param name="context">The context to write.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="scope">The scope to erase, or <c>null</c> for the guild-wide one.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>The number of records deleted.</returns>
    /// <remarks>
    /// The deletes run as SQL and do not update the change tracker. This removes the bot's memory of
    /// the resources, not the resources themselves: tear those down in Discord first.
    /// </remarks>
    public static async Task<int> DeleteScopeAsync(
        this DbContext context,
        ulong guildId,
        string? scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var normalized = ManagedScope.Normalize(scope);

        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var deleted = await DeleteScopeOfAsync<ManagedMessage>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);
        deleted += await DeleteScopeOfAsync<ManagedWebhook>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);
        deleted += await DeleteScopeOfAsync<ManagedChannel>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);
        deleted += await DeleteScopeOfAsync<ManagedCategory>(context, guildId, normalized, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return deleted;
    }

    /// <summary>
    /// Lists the distinct scopes that still have records in the guild, sorted, excluding the
    /// guild-wide one. For a bot that scopes by game server, this answers "which servers do I still
    /// hold resources for?".
    /// </summary>
    /// <param name="context">The context to read.</param>
    /// <param name="guildId">The owning guild.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The scopes, sorted ordinally.</returns>
    public static async Task<IReadOnlyList<string>> ListScopesAsync(
        this DbContext context,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scopes = new HashSet<string>(StringComparer.Ordinal);
        scopes.UnionWith(await ScopesOfAsync<ManagedCategory>(context, guildId, cancellationToken).ConfigureAwait(false));
        scopes.UnionWith(await ScopesOfAsync<ManagedChannel>(context, guildId, cancellationToken).ConfigureAwait(false));
        scopes.UnionWith(await ScopesOfAsync<ManagedMessage>(context, guildId, cancellationToken).ConfigureAwait(false));
        scopes.UnionWith(await ScopesOfAsync<ManagedWebhook>(context, guildId, cancellationToken).ConfigureAwait(false));

        scopes.Remove(ManagedScope.Global);

        return scopes.Order(StringComparer.Ordinal).ToList();
    }

    private static Task<int> DeleteScopeOfAsync<TResource>(
        DbContext context,
        ulong guildId,
        string scope,
        CancellationToken cancellationToken)
        where TResource : ManagedResource =>
        context.Set<TResource>()
            .Where(r => r.GuildId == guildId && r.Scope == scope)
            .ExecuteDeleteAsync(cancellationToken);

    private static async Task<List<string>> ScopesOfAsync<TResource>(
        DbContext context,
        ulong guildId,
        CancellationToken cancellationToken)
        where TResource : ManagedResource =>
        await context.Set<TResource>()
            .Where(r => r.GuildId == guildId)
            .Select(r => r.Scope)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
```

- [ ] **Step 4: Run the tests**

Run: `dtk dtk dotnet test tests/Persistord.Managed.Tests/Persistord.Managed.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Write the package README**

Create `src/Persistord.Managed/README.md`: what it is (records of what the bot created, not a mirror), the four entities as a table with their natural key, the four helpers with signatures, a short "reconcile" example (`FindManagedAsync` → fetch from Discord → repost on 404 → `UpsertManagedAsync`), a `ContentHash` render-gating paragraph, the `Scope` explanation including **why it is never null**, and a bold warning that `ManagedWebhook.Token` is plaintext unless `Persistord.Protection` is wired up. Close with a License section.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(managed): add upsert, find, delete-scope and list-scopes helpers"
```

---

## Task 6: `Persistord.Protection`

**Files:**
- Create: `src/Persistord.Protection/Persistord.Protection.csproj`, `README.md`, `ProtectionPurposes.cs`, `ProtectedStringConverter.cs`, `ProtectionModelBuilderExtensions.cs`
- Create: `tests/Persistord.Protection.Tests/Persistord.Protection.Tests.csproj`, `stryker-config.json`, `ProtectionTests.cs`
- Modify: `Directory.Packages.props`, `.github/dependabot.yml`, `Persistord.slnx`, `.github/workflows/CD.yml`, `.github/workflows/Mutation.yml`

**Interfaces:**
- Consumes: `ProtectedAttribute` (Task 4), `SqliteTestDatabase` (Task 1).
- Produces:
  - `static class ProtectionPurposes` with `const string V1 = "Persistord.Protection.v1"`
  - `sealed class ProtectedStringConverter : ValueConverter<string, string>` with `ProtectedStringConverter(IDataProtector protector)`
  - `ModelBuilder ProtectionModelBuilderExtensions.ApplyProtection(this ModelBuilder modelBuilder, IDataProtectionProvider dataProtectionProvider)`

**Design decision — one entry point, not two.** The spec offered the provider "through the `DbContext` constructor or `DbContextOptions` extension". This ships only `ApplyProtection(modelBuilder, provider)`, which the consumer calls from `OnModelCreating` with a provider held in a field (constructor-injected). A `UseDataProtection` options extension would be a second way to say the same thing, and it would still have to be read back inside `OnModelCreating`.

- [ ] **Step 1: Scaffold and wire up**

Add to the first `ItemGroup` of `Directory.Packages.props` (the shipped-floors group), with a comment matching the existing one:

```xml
    <PackageVersion Include="Microsoft.AspNetCore.DataProtection.Abstractions" Version="[10.0.0, 11.0.0)" />
```

Add it to the `ignore` list in `.github/dependabot.yml` alongside the EF Core entries, with a one-line reason: it is a published floor, not a tested version.

Create `src/Persistord.Protection/Persistord.Protection.csproj` in the usual packable shape with:

```xml
    <PackageId>Persistord.Protection</PackageId>
    <Description>Encrypts [Protected] string columns of a Persistord context at rest with ASP.NET Core Data Protection.</Description>
    <PackageTags>discord;efcore;persistence;dataprotection;encryption</PackageTags>
```

a `<PackageReference Include="Microsoft.AspNetCore.DataProtection.Abstractions" />` and a project reference to Core.

Add both projects to `Persistord.slnx`, a pack step to `CD.yml`, a matrix entry to `Mutation.yml`, and a `stryker-config.json` with `"module": "Persistord.Protection"` in the test project.

- [ ] **Step 2: Write the failing tests**

Create `tests/Persistord.Protection.Tests/ProtectionTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Testing;
using Xunit;

namespace Persistord.Protection.Tests;

public class ProtectionTests
{
    [Fact]
    public async Task A_protected_property_round_trips_through_ef()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new SecretContext(o, new ReversingProvider()));

        context.Secrets.Add(new SecretRow
        {
            Token = "super-secret", Label = "public"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var row = await context.Secrets.SingleAsync();
        Assert.Equal("super-secret", row.Token);
    }

    [Fact]
    public async Task The_stored_value_is_not_the_plaintext()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new SecretContext(o, new ReversingProvider()));

        context.Secrets.Add(new SecretRow
        {
            Token = "super-secret", Label = "public"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.Database
            .SqlQuery<string>($"SELECT \"Token\" AS \"Value\" FROM \"Secrets\"")
            .SingleAsync();

        Assert.NotEqual("super-secret", stored);
    }

    [Fact]
    public async Task An_unannotated_property_is_stored_as_is()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext(o => new SecretContext(o, new ReversingProvider()));

        context.Secrets.Add(new SecretRow
        {
            Token = "super-secret", Label = "public"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.Database
            .SqlQuery<string>($"SELECT \"Label\" AS \"Value\" FROM \"Secrets\"")
            .SingleAsync();

        Assert.Equal("public", stored);
    }

    [Fact]
    public async Task A_lost_key_ring_surfaces_as_a_cryptographic_exception()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        await using (var writer = database.CreateContext(o => new SecretContext(o, new ReversingProvider())))
        {
            writer.Secrets.Add(new SecretRow
            {
                Token = "super-secret", Label = "public"
            });
            await writer.SaveChangesAsync();
        }

        await using var reader = database.CreateContext(o => new SecretContext(o, new BrokenProvider()));
        await Assert.ThrowsAsync<CryptographicException>(() => reader.Secrets.SingleAsync());
    }

    [Fact]
    public void ApplyProtection_guards_its_arguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((ModelBuilder)null!).ApplyProtection(new ReversingProvider()));
        Assert.Throws<ArgumentNullException>(() =>
            new ModelBuilder().ApplyProtection(null!));
    }

    [Fact]
    public void The_purpose_string_is_frozen() =>
        Assert.Equal("Persistord.Protection.v1", ProtectionPurposes.V1);

    public sealed class SecretRow
    {
        public long Id { get; set; }

        [Protected]
        public string Token { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;
    }

    public sealed class SecretContext(
        DbContextOptions<SecretContext> options,
        IDataProtectionProvider dataProtectionProvider) : Persistord.Core.DiscordDbContext(options)
    {
        public DbSet<SecretRow> Secrets => Set<SecretRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SecretRow>().ToTable("Secrets");
            modelBuilder.ApplyProtection(dataProtectionProvider);
        }
    }

    /// <summary>
    /// A stand-in protector: reverses and base64s the payload. The converter only ever calls
    /// Protect/Unprotect, so a fake exercises the wiring faithfully without a key ring.
    /// </summary>
    private sealed class ReversingProvider : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext.Reverse().ToArray();

        public byte[] Unprotect(byte[] protectedData) => protectedData.Reverse().ToArray();
    }

    private sealed class BrokenProvider : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) =>
            throw new CryptographicException("The key ring is gone.");
    }
}
```

Note on the fakes: `IDataProtector` is the byte-array interface; the `string`-based `Protect`/`Unprotect` the converter calls are extension methods in `Microsoft.AspNetCore.DataProtection` (`DataProtectionCommonExtensions`), which is why the test project references the full `Microsoft.AspNetCore.DataProtection` package while the library only needs `.Abstractions`. Add `<PackageVersion Include="Microsoft.AspNetCore.DataProtection" Version="10.0.*" />`-style pin to the *test* group of `Directory.Packages.props` (pin the exact current 10.0.x, following the group's convention) and reference it from the test project.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Protection.Tests/Persistord.Protection.Tests.csproj`
Expected: compile error — `ApplyProtection` and `ProtectionPurposes` do not exist.

- [ ] **Step 4: Write the package**

Create `src/Persistord.Protection/ProtectionPurposes.cs`:

```csharp
namespace Persistord.Protection;

/// <summary>The Data Protection purpose strings Persistord derives its protectors from.</summary>
public static class ProtectionPurposes
{
    /// <summary>
    /// The purpose for every <c>[Protected]</c> column. Fixed on purpose: a protector derived from a
    /// different purpose cannot read what this one wrote, so changing this string orphans every
    /// existing ciphertext.
    /// </summary>
    public const string V1 = "Persistord.Protection.v1";
}
```

Create `src/Persistord.Protection/ProtectedStringConverter.cs`:

```csharp
using System.Linq.Expressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistord.Protection;

/// <summary>
/// Encrypts a string on the way to the database and decrypts it on the way back. A failed decrypt
/// surfaces as <see cref="System.Security.Cryptography.CryptographicException"/> while EF
/// materializes the entity; the usual cause is a key ring that was lost or rotated away.
/// </summary>
public sealed class ProtectedStringConverter : ValueConverter<string, string>
{
    /// <summary>Initializes the converter from a protector.</summary>
    /// <param name="protector">
    /// The protector to use, normally created for <see cref="ProtectionPurposes.V1"/>.
    /// </param>
    public ProtectedStringConverter(IDataProtector protector)
        : base(ToProvider(protector), FromProvider(protector))
    {
    }

    // Built in static helpers so the null check runs before the base constructor captures it.
    private static Expression<Func<string, string>> ToProvider(IDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return value => protector.Protect(value);
    }

    private static Expression<Func<string, string>> FromProvider(IDataProtector protector) =>
        value => protector.Unprotect(value);
}
```

Create `src/Persistord.Protection/ProtectionModelBuilderExtensions.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;

namespace Persistord.Protection;

/// <summary>Model-building extensions that encrypt annotated columns.</summary>
public static class ProtectionModelBuilderExtensions
{
    /// <summary>
    /// Installs a <see cref="ProtectedStringConverter"/> on every <see cref="string"/> property
    /// annotated <see cref="ProtectedAttribute"/>, anywhere in the model. Call it last in
    /// <c>OnModelCreating</c>, after the module and entity configurations that create those
    /// properties.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="dataProtectionProvider">
    /// The provider to derive the protector from. Use one instance for the whole application: EF
    /// caches the model per context type, so the first context's protector is the one baked into the
    /// cached model. Tests that need different key rings must also replace
    /// <c>IModelCacheKeyFactory</c> — <c>Persistord.Testing</c>'s fixtures already do.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyProtection(
        this ModelBuilder modelBuilder,
        IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        var converter = new ProtectedStringConverter(
            dataProtectionProvider.CreateProtector(ProtectionPurposes.V1));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(IsProtected).ToList())
            {
                property.SetValueConverter(converter);
            }
        }

        return modelBuilder;
    }

    private static bool IsProtected(IMutableProperty property) =>
        property.ClrType == typeof(string)
        && property.PropertyInfo?.GetCustomAttribute<ProtectedAttribute>() is not null;
}
```

- [ ] **Step 5: Run the tests**

Run: `dtk dotnet test tests/Persistord.Protection.Tests/Persistord.Protection.Tests.csproj`
Expected: PASS. If `SqlQuery<string>` fights the raw-SQL column alias, read the row with a `SqliteCommand` on `context.Database.GetDbConnection()` instead — the assertion is about the bytes at rest, not about the API used to read them.

- [ ] **Step 6: Write the package README**

Create `src/Persistord.Protection/README.md`: the three-line setup (reference the package, annotate with `[Protected]`, call `ApplyProtection`), the fixed purpose string, the model-cache warning about one provider per application, and a **Key ring** section: losing the key ring means losing every protected value, so persist it next to the database (`PersistKeysToFileSystem`) rather than leaving it in the default per-user folder, and a rotated-away key surfaces as `CryptographicException` on read. License section at the end.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(protection): encrypt [Protected] columns via Data Protection"
```

---

## Task 7: Package documentation

**Files:**
- Create: `docs/articles/managed-resources.md`, `docs/articles/protection.md`, `docs/articles/testing.md`
- Modify: `docs/articles/toc.yml`, `README.md`, `docs/articles/samples.md` (only if a sample was added — it was not, so leave it)

**Interfaces:**
- Consumes: everything above.
- Produces: docs that build under docfx.

- [ ] **Step 1: Write `docs/articles/managed-resources.md`**

Sections, in order:

1. **What it is** — the bot owns these Discord objects; this module remembers what it created, keyed by the consumer's own key. It is not a mirror and it never calls Discord.
2. **The four entities** — a table of `ManagedCategory` / `ManagedChannel` / `ManagedMessage` / `ManagedWebhook` with their extra columns and the shared `(GuildId, Scope, Key)` unique key.
3. **Wiring** — a `DbContext` deriving `DiscordDbContext` that declares the `DbSet`s it uses and calls `ApplyManagedModule()`, then optionally `ApplyGuildRoot()`.
4. **Scope** — an opaque consumer string, never a foreign key, never null; `ManagedScope.Global` is `""`; why (SQL treats NULLs as distinct in a unique index, so a nullable scope column accepts duplicate guild-wide rows).
5. **The reconcile loop** — the canonical snippet:

```csharp
var record = await db.FindManagedAsync<ManagedMessage>(guildId, scope, "dashboard");
var message = record is null ? null : await channel.GetMessageAsync(record.DiscordId);

if (message is null)
{
    message = await channel.SendMessageAsync(embed: embed);
}
else
{
    await message.ModifyAsync(m => m.Embed = embed);
}

await db.UpsertManagedAsync<ManagedMessage>(guildId, scope, "dashboard", message.Id, m =>
{
    m.ChannelDiscordId = channel.Id;
    m.ContentHash = hash;
});
```

6. **Render gating with `ContentHash`** — hash the payload, compare to the stored hash, skip the edit when equal; this survives a restart, a process-local cache does not.
7. **Tearing down a scope** — `DeleteScopeAsync` after the Discord-side teardown, and `ListScopesAsync` to find what is left.
8. **What this module deliberately does not do** — no channel types, no permissions, no names, no Discord calls (spec §3.3).

- [ ] **Step 2: Write `docs/articles/protection.md`**

Sections: what it protects and what it does not (column values, not the whole database); the three-step setup with a full context example that constructor-injects `IDataProtectionProvider`; the fixed purpose string; **Key ring management** — `PersistKeysToFileSystem` next to the database, back it up, losing it loses every protected value; what a failure looks like (`CryptographicException` during materialization) and how to tell a lost key from a rotated one; the one-provider-per-application model-cache note; and the plaintext warning for `ManagedWebhook.Token` when the package is absent.

- [ ] **Step 3: Write `docs/articles/testing.md`**

Sections: `SqliteTestDatabase.Private()` vs `.Shared()` with a short example each and when the shared one is required (DI scopes, two writers racing); `TestSchema.Migrate` (the default — catches model/migration drift) vs `EnsureCreated` (fast); `UniqueModelCacheKeyFactory` and why a shared cached model hides coverage and lets configuration mutants survive; the three model assertions with an example each; and a note that the package throws `InvalidOperationException` rather than depending on a test framework.

- [ ] **Step 4: Update the table of contents and the root README**

In `docs/articles/toc.yml`, add to `Guides`: `Managed Resources` → `managed-resources.md`, `Protection` → `protection.md`, `Testing` → `testing.md` (Providers, Guild lifecycle and Upsert come in Task 8).

In `README.md`, extend the `## Packages` table with `Persistord.Managed`, `Persistord.Protection` and `Persistord.Testing` — one line each, stating that they are opt-in and not part of the `Persistord` meta package, and why (the meta package stays the library-neutral mirror stack).

- [ ] **Step 5: Verify the docs build**

```bash
dotnet tool restore
dotnet docfx docs/docfx.json
```

Expected: build succeeds with no warnings about missing files or broken links. `docs/_site/` output is not committed — check `.gitignore` covers it before staging.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "docs: document the Managed, Protection and Testing packages"
```

---

## Task 8: Provider hygiene, recipes, and branch verification

**Files:**
- Create: `docs/articles/providers.md`, `docs/articles/guild-lifecycle.md`, `docs/articles/upsert.md`
- Modify: `docs/articles/toc.yml`, `docs/articles/recipes.md`, `docs/articles/getting-started.md`, `docs/articles/troubleshooting.md`

**Interfaces:**
- Consumes: everything above, plus the Core plan's `PurgeGuildAsync`, `ClearAllTablesAsync`, `UpsertAsync`, `ApplyGuildRoot`.
- Produces: the spec §8 documentation set, and a verified branch.

- [ ] **Step 1: Write `docs/articles/providers.md`**

One section per provider, with only things that actually bite:

**SQLite**
- `DateTimeOffset` cannot be used in `ORDER BY`. Order by the surrogate key instead — every `ManagedResource` has one. Show the wrong query and the right one.
- `journal_mode=WAL` and `busy_timeout`: set them on the connection string / with a `PRAGMA` on open, and why a bot with dozens of concurrent DI scopes on one file needs both.
- `EnableRetryOnFailure` does not exist for SQLite. What to do instead: `busy_timeout`, short transactions, and one writer at a time.
- Unique indexes treat NULLs as distinct, so a nullable column in a natural key is not a natural key. This is why `ManagedResource.Scope` is `""` rather than `null`.
- `ClearAllTablesAsync` needs no `PRAGMA defer_foreign_keys`: it deletes dependents first, from the model's FK graph.

**PostgreSQL**
- `NULLS NOT DISTINCT` exists since PG 15 and is the other way to solve the nullable-key problem; Persistord does not use it, so the model stays portable.
- `timestamptz` and `DateTimeOffset`: Npgsql normalizes to UTC; an offset is not round-tripped, so store UTC and treat the offset as informational.
- The `ulong` → `long` round trip is covered by `tests/Persistord.Provider.Tests`, including `ulong.MaxValue`.

- [ ] **Step 2: Write `docs/articles/guild-lifecycle.md`**

The recipe, with code:

1. On `JoinedGuild`: upsert the root row with `JoinedAt`, clear `LeftAt`.

```csharp
await db.Guilds.UpsertAsync(
    g => g.Id == guild.Id,
    () => new GuildEntity { Id = guild.Id },
    g =>
    {
        g.Name = guild.Name;
        g.JoinedAt ??= clock.GetUtcNow();
        g.LeftAt = null;
    });
```

2. On `LeftGuild`: two policies — soft mark (`LeftAt = now`, keep the rows for a re-invite, optionally hide them with `ApplyGuildRoot(filterLeftGuilds: true)`), or hard purge (`PurgeGuildAsync`). Say plainly that without either, rows outlive the guild forever.
3. Purge behind a per-guild lock, because Discord can deliver `LeftGuild` twice and a reconciler may be mid-flight:

```csharp
using (await _guildLocks.AcquireAsync(guildId, cancellationToken))
{
    await db.PurgeGuildAsync(guildId, cancellationToken);
}
```

4. Order of operations: tear down Discord resources first (they are not the database's to delete), then `DeleteScopeAsync`/`PurgeGuildAsync`. `PurgeGuildAsync` deletes records, never Discord objects.
5. What `cascade: true` buys and what it costs: the guild row becomes a prerequisite for every scoped insert.

- [ ] **Step 3: Write `docs/articles/upsert.md`**

The recipe: the signature; the unique index that makes the race recovery work; the create/update split and why `update` also runs for a created row; `UpsertIfChangedAsync` for the dirty-check short-circuit and its interaction with `UpdatedAt`; what it deliberately does not do (no bulk, no conflict resolution, no `ON CONFLICT` generation, no multi-row merge); and a worked before/after showing a hand-written find-then-add-or-update with its `catch (DbUpdateException)` recovery block collapsing into one call.

- [ ] **Step 4: Update the toc and the existing articles**

- `docs/articles/toc.yml`: add `Providers`, `Guild Lifecycle` and `Upsert` under `Guides`.
- `docs/articles/recipes.md`: add a "Remember a channel the bot created" recipe (three lines with `UpsertManagedAsync`) and a "Purge a guild" one-liner, each linking to the full article.
- `docs/articles/getting-started.md` "Next steps": link the new articles.
- `docs/articles/troubleshooting.md`: add three entries — *"My unique index accepts duplicates"* (nullable column in the key; use `""`), *"`ORDER BY` on a `DateTimeOffset` fails on SQLite"* (order by the surrogate key), and *"`CryptographicException` when reading a token"* (key ring lost or rotated; link `protection.md`).

- [ ] **Step 5: Full verification**

```bash
dtk dotnet build Persistord.slnx --configuration Release
dtk dotnet test Persistord.slnx --configuration Release --no-build
dotnet tool restore
dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder" --no-build --verbosity=ERROR
git diff --exit-code
dotnet docfx docs/docfx.json
```

Expected: build with zero warnings, all tests pass (Postgres tests may skip without Docker), no formatting diff, docs build clean.

- [ ] **Step 6: Confirm the packages actually pack**

```bash
for project in Persistord.Testing Persistord.Managed Persistord.Protection; do
  dtk dotnet pack "src/$project/$project.csproj" --configuration Release -p:Version=1.0.0-beta4
done
ls src/*/bin/Release/*.nupkg
```

Expected: three `.nupkg` files plus their `.snupkg` symbol packages. Confirm `Persistord.Testing.1.0.0-beta4.nupkg` declares `Microsoft.EntityFrameworkCore.Sqlite [10.0.0, 11.0.0)` — the `VersionOverride` from Task 1 — and not the pinned `10.0.11`:

```bash
unzip -p src/Persistord.Testing/bin/Release/Persistord.Testing.1.0.0-beta4.nupkg Persistord.Testing.nuspec | grep -A5 dependencies
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "docs: add provider hygiene, guild lifecycle and upsert guides"
```

---

## Self-Review Notes

- **Spec §3.4 acceptance** — "two `UpsertManagedAsync` calls with the same `(guildId, null, key)` produce one row" is `ManagedStoreTests.Two_upserts_of_the_same_global_key_produce_one_row`; "`DeleteScopeAsync` removes … and nothing from other scopes or the global scope" is `DeleteScope_removes_that_scope_across_all_four_tables_only`.
- **Spec §6** — the converter, the attribute-driven convention, the fixed purpose string, the `CryptographicException` surface and the key-ring guidance are Task 6; `ManagedWebhook.Token` is annotated in Task 4 and the plaintext caveat is bold in two READMEs and one article.
- **Spec §7** — `SqliteTestDatabase.Private`/`Shared`, both schema modes, the public `UniqueModelCacheKeyFactory` and the three assertions are Tasks 1–2; Task 3 deletes the repo's three duplicated factories, which is the concrete complaint §7.1 makes.
- **Spec §8** — `ClearAllTablesAsync` ships in the Core plan (Task 7 there); the Providers article, the guild-lifecycle recipe, the upsert recipe and the managed-resources recipe are Tasks 7–8 here.
- **Deliberate deviations, each recorded at the task that makes it:** `ProtectedAttribute` lives in Core, not in `Persistord.Protection`, because `Persistord.Managed` must be able to annotate a token without taking the dependency (Task 4); `ManagedResource.Scope` is a non-nullable `string` with helper-level normalization instead of a `string?` behind a value converter (Task 4); `ApplyProtection(modelBuilder, provider)` is the only entry point, with no `UseDataProtection` options extension (Task 6); the `TestSchema` enum is named `TestSchema` rather than `Schema` to leave that name free for the property (Task 1).
- **Not in scope, by the spec's own §9:** `Persistord.Messages`, `Persistord.History` and the Discord.Net adapter are untouched, and no new sample app is added — the new articles carry the examples instead.
