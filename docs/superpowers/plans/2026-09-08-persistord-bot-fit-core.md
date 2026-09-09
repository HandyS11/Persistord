# Persistord Bot-Fit — Core (beta3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `Persistord.Core` into a library a bot can use without mirroring Discord: conventions-only base context, opt-in skeleton graph, snowflake-key convention, guild tenant root with cascade and purge, natural-key upsert, and timestamp stamping.

**Architecture:** `DiscordDbContext` keeps only `ConfigureConventions` (the `ulong`→`long` conversion, a snowflake-key convention and a guild-scope index convention); the five skeleton entities move behind `ApplyCoreGraph()` and a new `DiscordGraphDbContext` subclass. Bot-owned state gets three provider-agnostic primitives that are plain extension methods, not an engine: `UpsertAsync` on `DbSet<T>`, `PurgeGuildAsync`/`ClearAllTablesAsync` on `DbContext`, and a `TimestampInterceptor` driven by `TimeProvider`. Entities opt into guild scoping with the `IGuildScoped` marker interface.

**Tech Stack:** .NET 10, EF Core 10 (floor `[10.0.0, 11.0.0)`), xunit 2.9, SQLite for tests, Npgsql for the provider test leg.

**Spec:** `docs/superpowers/specs/2026-09-08-persistord-bot-fit-design.md` (§1, §2, §4, §5, and the `ClearAllTablesAsync` half of §8)

**Scope note:** This plan is the spec's sequencing steps 1–3 — the breaking release. Steps 4–7 (`Persistord.Testing`, `Persistord.Managed`, `Persistord.Protection`, docs articles) are in the sibling plan `2026-09-08-persistord-bot-fit-packages.md` and must be executed after this one.

---

## Global Constraints

Copied from the repo's build configuration. Every task inherits these.

- **Target framework** `net10.0`. `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest` (`Directory.Build.props`).
- **`TreatWarningsAsErrors=true`** with `AnalysisLevel=latest-all` and the NetAnalyzers, Roslynator, SonarAnalyzer and VS-Threading analyzer packs. A warning fails the build.
- **`GenerateDocumentationFile=true`** for `src/**`: every public and protected member needs an XML doc comment. `CS1591` is silenced only under `tests/**`, `samples/**` and `**/Migrations/*.cs`.
- **`CA2007` (ConfigureAwait)** is `none` only for `tests/**`, `samples/**`. In `src/**` **every** awaited task must have `.ConfigureAwait(false)`.
- **EF Core version floor is frozen** at `[10.0.0, 11.0.0)` (`Directory.Packages.props`). Do not raise it and do not use an EF API introduced after 10.0.0.
- **Central Package Management**: versions live in `Directory.Packages.props`; `<PackageReference>` elements carry no `Version`.
- **Style** (`.editorconfig`): file-scoped namespaces, 4-space indent, 120-column limit, `csharp_new_line_before_open_brace = all` (opening brace on its own line, including after `if`/`foreach`/`try`), object initializers wrapped as the existing files show.
- **Null guards**: public API keeps `ArgumentNullException.ThrowIfNull(...)` on reference parameters even though `CA1062` is off — the repo pins them with dedicated null-guard tests.
- **Never call an `[Obsolete]` member from repo code** — warnings are errors. Tests that must call one wrap the call in `#pragma warning disable CS0618` / `restore`.
- **Build/test commands** in this plan use `dtk dotnet …` (DotnetTokenKiller): a drop-in wrapper that filters `dotnet` output. Plain `dotnet …` behaves identically and is what CI runs.
- **Commit style**: conventional commits (`feat:`, `test:`, `docs:`, `refactor:`, `chore:`).
- **Formatting gate**: CI runs `dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder" --no-build` then `git diff --exit-code`. Task 8 runs it once for the whole branch.

---

## File Structure

**Created (source):**

- `src/Persistord.Core/DiscordGraphDbContext.cs` — opt-in skeleton context: the five `DbSet`s + `ApplyCoreGraph()`
- `src/Persistord.Core/Conventions/SnowflakeKeyConvention.cs` — `ulong` key parts are caller-supplied
- `src/Persistord.Core/Conventions/GuildScopeConvention.cs` — `GuildId` index for `IGuildScoped` entities
- `src/Persistord.Core/Abstractions/IGuildScoped.cs` — guild tenancy marker
- `src/Persistord.Core/Abstractions/ICreatedAt.cs`, `IUpdatedAt.cs` — timestamp markers
- `src/Persistord.Core/Interception/TimestampInterceptor.cs` — stamps `Added`/`Modified` entries from a `TimeProvider`
- `src/Persistord.Core/UpsertExtensions.cs` + `UpsertResult.cs` — natural-key upsert on `DbSet<T>`
- `src/Persistord.Core/GuildRootExtensions.cs` — `ApplyGuildRoot(cascade, filterLeftGuilds)`
- `src/Persistord.Core/GuildPurgeExtensions.cs` — `PurgeGuildAsync`
- `src/Persistord.Core/DatabaseMaintenanceExtensions.cs` — `ClearAllTablesAsync`
- `src/Persistord.Core/Internal/ModelDeleteOrder.cs` — dependents-before-principals ordering from the EF model

**Modified (source):**

- `src/Persistord.Core/DiscordDbContext.cs` — conventions only; `base.ConfigureConventions`; `TimeProvider` constructor
- `src/Persistord.Core/ModelBuilderExtensions.cs` — `ApplyCoreGraph()` + `[Obsolete]` `ApplyCoreConfiguration()` forwarder
- `src/Persistord.Core/Entities/GuildEntity.cs` — optional `Name`/`OwnerId`, new `JoinedAt`/`LeftAt`
- `src/Persistord.Core/Configurations/GuildEntityConfiguration.cs` — drop `Name.IsRequired()`
- `src/Persistord.Core/README.md`, `README.md`, `docs/articles/core-graph.md`, `docs/articles/getting-started.md`, `docs/articles/snowflake-conversion.md`

**Modified (tests + samples), because the skeleton `DbSet`s moved:**

- `tests/Persistord.Core.Tests/TestContext.cs`, `SqliteFixture.cs`, `CoreNullGuardTests.cs`, `EntityDefaultsTests.cs`
- `tests/Persistord.Provider.Tests/PostgresRoundTripTests.cs` (`PgContext`)
- `samples/Persistord.Sample/MyBotContext.cs`, `samples/Persistord.Sample.CoreGraph/CoreGraphContext.cs`, `samples/Persistord.Sample.DiscordNet/AdapterContext.cs`
- `samples/Persistord.Sample/Migrations/**` — one new migration for the `GuildEntity` shape change

**Created (tests):** `ConventionsOnlyContext.cs`, `GraphSplitTests.cs`, `SnowflakeKeyConventionTests.cs`, `UpsertTests.cs`, `TimestampTests.cs`, `GuildScopeConventionTests.cs`, `GuildRootTests.cs`, `GuildPurgeTests.cs`, `ClearAllTablesTests.cs`, plus the `SharedSqliteDatabase` and `TestTimeProvider` helpers — all under `tests/Persistord.Core.Tests/`.

**Deliberately unchanged:** `Persistord.Messages`, `Persistord.History`, `Persistord.Adapters.DiscordNet`, the `Persistord` meta package, and the `ValueGeneratedNever()` calls in the five skeleton configurations (they must keep working for a consumer who calls `ApplyCoreGraph()` on a plain `DbContext` that never runs our conventions).

---

## Task 1: Split conventions from the skeleton graph

**Files:**
- Create: `src/Persistord.Core/DiscordGraphDbContext.cs`
- Modify: `src/Persistord.Core/DiscordDbContext.cs`, `src/Persistord.Core/ModelBuilderExtensions.cs`
- Modify: `tests/Persistord.Core.Tests/TestContext.cs`, `tests/Persistord.Core.Tests/SqliteFixture.cs`, `tests/Persistord.Core.Tests/CoreNullGuardTests.cs`
- Modify: `tests/Persistord.Provider.Tests/PostgresRoundTripTests.cs`, `samples/Persistord.Sample/MyBotContext.cs`, `samples/Persistord.Sample.CoreGraph/CoreGraphContext.cs`, `samples/Persistord.Sample.DiscordNet/AdapterContext.cs`
- Modify: `README.md`, `docs/articles/core-graph.md`, `docs/articles/getting-started.md`, `src/Persistord.Core/README.md`
- Create: `tests/Persistord.Core.Tests/ConventionsOnlyContext.cs`, `tests/Persistord.Core.Tests/GraphSplitTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Persistord.Core.DiscordGraphDbContext` (abstract, `protected DiscordGraphDbContext(DbContextOptions options)`); `ModelBuilder ModelBuilderExtensions.ApplyCoreGraph(this ModelBuilder)`; `SqliteFixture.Create<TContext>(Func<DbContextOptions<TContext>, TContext> factory, bool createSchema = true)` returning `(SqliteConnection Connection, TContext Context)`; `ConventionsOnlyContext` (a `DiscordDbContext` with no `DbSet`s).

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Core.Tests/ConventionsOnlyContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>A context that takes the conventions and maps nothing at all.</summary>
public sealed class ConventionsOnlyContext(DbContextOptions<ConventionsOnlyContext> options)
    : Persistord.Core.DiscordDbContext(options);
```

Create `tests/Persistord.Core.Tests/GraphSplitTests.cs`:

```csharp
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GraphSplitTests
{
    [Fact]
    public void Conventions_only_context_maps_no_entity_types()
    {
        var (connection, context) = SqliteFixture.Create(o => new ConventionsOnlyContext(o));
        using (connection)
        using (context)
        {
            Assert.Empty(context.Model.GetEntityTypes());
        }
    }

    [Fact]
    public void Graph_context_maps_the_five_skeleton_types()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            var mapped = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

            Assert.Equal(5, mapped.Count);
            Assert.Contains(typeof(GuildEntity), mapped);
            Assert.Contains(typeof(ChannelEntity), mapped);
            Assert.Contains(typeof(UserEntity), mapped);
            Assert.Contains(typeof(MemberEntity), mapped);
            Assert.Contains(typeof(RoleEntity), mapped);
        }
    }

    [Fact]
    public void Obsolete_ApplyCoreConfiguration_still_applies_the_graph()
    {
        var (connection, context) = SqliteFixture.Create(o => new LegacyGraphContext(o));
        using (connection)
        using (context)
        {
            Assert.Equal(5, context.Model.GetEntityTypes().Count());
        }
    }

    /// <summary>Pins the one-release forwarder a beta2 consumer's OnModelCreating still calls.</summary>
    private sealed class LegacyGraphContext(DbContextOptions<LegacyGraphContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
#pragma warning disable CS0618 // Testing the obsolete forwarder is the point of this context.
            modelBuilder.ApplyCoreConfiguration();
#pragma warning restore CS0618
        }
    }
}
```

Add the `using Microsoft.EntityFrameworkCore;` that `LegacyGraphContext` needs at the top of the file.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: compile error — `SqliteFixture.Create` has no generic overload, `ApplyCoreGraph` does not exist.

- [ ] **Step 3: Make `SqliteFixture` generic**

Replace `tests/Persistord.Core.Tests/SqliteFixture.cs` with:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Core.Tests;

/// <summary>Creates a context backed by a fresh open in-memory SQLite connection.</summary>
public static class SqliteFixture
{
    public static (SqliteConnection Connection, TestContext Context) Create() =>
        Create(options => new TestContext(options));

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
```

- [ ] **Step 4: Move the skeleton out of the base context**

Replace the body of `src/Persistord.Core/DiscordDbContext.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Conversions;

namespace Persistord.Core;

/// <summary>
/// Base EF Core context that applies Persistord's global conventions and nothing else: the
/// bit-faithful <see cref="ulong"/>-to-<see cref="long"/> conversion for every unsigned
/// 64-bit property. It maps no entity types, so a bot that owns Discord resources rather
/// than mirroring them pays for no tables. Derive <see cref="DiscordGraphDbContext"/>
/// instead to get the guild/channel/user/member/role skeleton.
/// </summary>
/// <remarks>Initializes the context with the given options.</remarks>
/// <param name="options">The context options supplied by the consumer.</param>
public abstract class DiscordDbContext(DbContextOptions options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<ulong>().HaveConversion<UlongToLongConverter>();
        configurationBuilder.Properties<ulong?>().HaveConversion<NullableUlongToLongConverter>();
    }
}
```

Create `src/Persistord.Core/DiscordGraphDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>
/// <see cref="DiscordDbContext"/> plus the opt-in Discord skeleton graph: guilds, channels,
/// users, members and roles. Derive this when the context mirrors Discord objects; derive
/// <see cref="DiscordDbContext"/> when it only needs the conventions.
/// </summary>
/// <remarks>Initializes the context with the given options.</remarks>
/// <param name="options">The context options supplied by the consumer.</param>
public abstract class DiscordGraphDbContext(DbContextOptions options) : DiscordDbContext(options)
{
    /// <summary>Persisted guilds.</summary>
    public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

    /// <summary>Persisted channels.</summary>
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();

    /// <summary>Persisted users.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Persisted guild members.</summary>
    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    /// <summary>Persisted roles.</summary>
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyCoreGraph();
    }
}
```

Replace the extension in `src/Persistord.Core/ModelBuilderExtensions.cs`:

```csharp
    /// <summary>
    /// Applies the configurations for all skeleton entities (guild, channel, user, member,
    /// role). Call from <c>OnModelCreating</c>, or derive <see cref="DiscordGraphDbContext"/>
    /// which calls it for you.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyCoreGraph(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder
            .ApplyConfiguration(new GuildEntityConfiguration())
            .ApplyConfiguration(new ChannelEntityConfiguration())
            .ApplyConfiguration(new UserEntityConfiguration())
            .ApplyConfiguration(new MemberEntityConfiguration())
            .ApplyConfiguration(new RoleEntityConfiguration());
    }

    /// <summary>Renamed to <see cref="ApplyCoreGraph"/>.</summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    [Obsolete("Renamed to ApplyCoreGraph. This forwarder is kept for one release and will be removed.")]
    public static ModelBuilder ApplyCoreConfiguration(this ModelBuilder modelBuilder) =>
        modelBuilder.ApplyCoreGraph();
```

- [ ] **Step 5: Point the repo's own graph consumers at the new base class**

Change the base class from `DiscordDbContext` to `DiscordGraphDbContext` in exactly the contexts that touch skeleton `DbSet`s:

- `tests/Persistord.Core.Tests/TestContext.cs` → `: Persistord.Core.DiscordGraphDbContext(options);`
- `tests/Persistord.Provider.Tests/PostgresRoundTripTests.cs` (`PgContext`) → `: Persistord.Core.DiscordGraphDbContext(options)`
- `samples/Persistord.Sample/MyBotContext.cs` → `: DiscordGraphDbContext(options)`
- `samples/Persistord.Sample.CoreGraph/CoreGraphContext.cs` → `: DiscordGraphDbContext(options)`
- `samples/Persistord.Sample.DiscordNet/AdapterContext.cs` → `: DiscordGraphDbContext(options)`

Leave `samples/Persistord.Sample.Messages/MessagesContext.cs`, `samples/Persistord.Sample.History/HistoryContext.cs`, `tests/Persistord.Messages.Tests/TestContext.cs` and `tests/Persistord.History.Tests/TestContext.cs` on `DiscordDbContext`: none of them touches a skeleton table, and after this change their models correctly contain only their module's tables. The Messages and History modules have no foreign key into the skeleton (verified: their configurations only reference their own entities), so nothing breaks.

Update the `CoreGraphContext` doc comment to say the graph now comes from `DiscordGraphDbContext`.

- [ ] **Step 6: Move the null-guard probe onto the graph context**

In `tests/Persistord.Core.Tests/CoreNullGuardTests.cs`:

- rename the first test to `ApplyCoreGraph_throws_on_null` and call `((ModelBuilder)null!).ApplyCoreGraph()`;
- add a forwarder guard test:

```csharp
    [Fact]
    public void ApplyCoreConfiguration_forwarder_throws_on_null() =>
#pragma warning disable CS0618 // Guarding the obsolete forwarder is the point of this test.
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyCoreConfiguration());
#pragma warning restore CS0618
```

- change `ProbeContext` to derive from `DiscordGraphDbContext` so `ProbeModel(null)` still exercises an `OnModelCreating` override:

```csharp
    private sealed class ProbeContext()
        : DiscordGraphDbContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite("DataSource=:memory:").Options)
```

- [ ] **Step 7: Run the tests**

Run: `dtk dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS, including the three new `GraphSplitTests`.

Then the whole solution: `dtk dtk dotnet build Persistord.slnx` — expected: no errors, no warnings (the `[Obsolete]` forwarder must not be called anywhere outside the two `#pragma`-wrapped spots).

- [ ] **Step 8: Document the split**

- `docs/articles/core-graph.md`: rewrite the `## DiscordDbContext` section into two — `DiscordDbContext` (conventions only, maps nothing) and `DiscordGraphDbContext` (adds the five `DbSet`s). Add a short "Upgrading from 1.0.0-beta2" block: *"`DiscordDbContext` no longer maps the skeleton. If you use `Guilds`/`Channels`/`Users`/`Members`/`Roles`, change your base class to `DiscordGraphDbContext`; if you never did, you now get zero tables and no migration entries. `ApplyCoreConfiguration()` is renamed `ApplyCoreGraph()`; the old name forwards for one release."*
- `docs/articles/getting-started.md` "1. Derive a context": show `DiscordDbContext` for the conventions-only case first, then `DiscordGraphDbContext` for the mirror case.
- `README.md` "Quick start → 1. Derive a context": same two-way split, kept short.
- `src/Persistord.Core/README.md`: update the opening description and the API list to name both contexts and `ApplyCoreGraph()`.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(core)!: split conventions from the skeleton graph

DiscordDbContext now applies only the snowflake conventions and maps no
entity types. The five skeleton entities move to DiscordGraphDbContext and
ApplyCoreGraph(); ApplyCoreConfiguration() forwards for one release."
```

---

## Task 2: Snowflake-key convention

**Files:**
- Create: `src/Persistord.Core/Conventions/SnowflakeKeyConvention.cs`
- Modify: `src/Persistord.Core/DiscordDbContext.cs`
- Create: `tests/Persistord.Core.Tests/SnowflakeKeyConventionTests.cs`
- Modify: `docs/articles/snowflake-conversion.md`

**Interfaces:**
- Consumes: `DiscordDbContext.ConfigureConventions` from Task 1.
- Produces: `Persistord.Core.Conventions.SnowflakeKeyConvention` (public, `IModelFinalizingConvention`), registered automatically by `DiscordDbContext`.

- [ ] **Step 1: Write the failing test**

Create `tests/Persistord.Core.Tests/SnowflakeKeyConventionTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Persistord.Core.Tests;

public class SnowflakeKeyConventionTests
{
    private static IModel BuildModel()
    {
        var (connection, context) = SqliteFixture.Create(o => new ConventionProbeContext(o), createSchema: false);
        using (connection)
        using (context)
        {
            return context.Model;
        }
    }

    private static ValueGenerated ValueGeneratedFor(Type entity, string property) =>
        BuildModel().FindEntityType(entity)!.FindProperty(property)!.ValueGenerated;

    [Fact]
    public void Consumer_ulong_key_is_caller_supplied_without_any_fluent_call() =>
        Assert.Equal(ValueGenerated.Never, ValueGeneratedFor(typeof(SnowflakeKeyed), nameof(SnowflakeKeyed.Id)));

    [Fact]
    public void Long_key_keeps_store_generation() =>
        Assert.Equal(ValueGenerated.OnAdd, ValueGeneratedFor(typeof(SurrogateKeyed), nameof(SurrogateKeyed.Id)));

    [Fact]
    public void Ulong_part_of_a_composite_key_is_caller_supplied() =>
        Assert.Equal(
            ValueGenerated.Never,
            ValueGeneratedFor(typeof(CompositeKeyed), nameof(CompositeKeyed.GuildId)));

    [Fact]
    public void Non_key_ulong_property_is_left_alone() =>
        Assert.Equal(
            ValueGenerated.Never,
            ValueGeneratedFor(typeof(SurrogateKeyed), nameof(SurrogateKeyed.OwnerId)));

    [Fact]
    public void Explicit_configuration_wins_over_the_convention() =>
        Assert.Equal(
            ValueGenerated.OnAdd,
            ValueGeneratedFor(typeof(ExplicitlyGenerated), nameof(ExplicitlyGenerated.Id)));

    public sealed class SnowflakeKeyed
    {
        public ulong Id { get; set; }
    }

    public sealed class SurrogateKeyed
    {
        public long Id { get; set; }

        public ulong OwnerId { get; set; }
    }

    public sealed class CompositeKeyed
    {
        public ulong GuildId { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    public sealed class ExplicitlyGenerated
    {
        public ulong Id { get; set; }
    }

    private sealed class ConventionProbeContext(DbContextOptions<ConventionProbeContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SnowflakeKeyed>();
            modelBuilder.Entity<SurrogateKeyed>();
            modelBuilder.Entity<CompositeKeyed>().HasKey(e => new
            {
                e.GuildId, e.Key
            });
            modelBuilder.Entity<ExplicitlyGenerated>().Property(e => e.Id).ValueGeneratedOnAdd();
        }
    }
}
```

Note on `Non_key_ulong_property_is_left_alone`: a non-key scalar has `ValueGenerated.Never` by EF's own default, so this test documents that the convention does not *change* non-key properties rather than asserting the convention did something.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~SnowflakeKeyConventionTests`
Expected: FAIL — `Consumer_ulong_key_is_caller_supplied_without_any_fluent_call` reports `OnAdd` instead of `Never`.

- [ ] **Step 3: Write the convention**

Create `src/Persistord.Core/Conventions/SnowflakeKeyConvention.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Persistord.Core.Conventions;

/// <summary>
/// Marks every <see cref="ulong"/> or <see cref="Nullable{T}"/> property that is part of a
/// primary key as caller-supplied (<see cref="ValueGenerated.Never"/>). An unsigned 64-bit key
/// is a value the consumer already owns — a Discord snowflake, a Steam64 id, a Rust entity id —
/// never a store-generated identity column. Explicit fluent configuration still wins, because
/// the convention writes at convention precedence.
/// </summary>
public sealed class SnowflakeKeyConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                continue;
            }

            foreach (var property in primaryKey.Properties)
            {
                if (property.ClrType == typeof(ulong) || property.ClrType == typeof(ulong?))
                {
                    property.Builder.ValueGenerated(ValueGenerated.Never);
                }
            }
        }
    }
}
```

Register it in `src/Persistord.Core/DiscordDbContext.cs`, at the end of `ConfigureConventions`:

```csharp
        configurationBuilder.Conventions.Add(_ => new SnowflakeKeyConvention());
```

with `using Persistord.Core.Conventions;` added at the top.

- [ ] **Step 4: Run the tests**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS. `CoreConfigurationTests.Snowflake_key_is_caller_supplied_not_store_generated` must still pass — the skeleton configurations keep their explicit `ValueGeneratedNever()` so `ApplyCoreGraph()` also works on a plain `DbContext` that never runs our conventions.

- [ ] **Step 5: Document it**

In `docs/articles/snowflake-conversion.md`, under `## Global registration`, add a `### Keys` subsection: the conversion covers values, and a separate convention marks `ulong` key parts `ValueGeneratedNever()` so EF never treats them as identity columns — including on the consumer's own entities. Add one sentence to `## Storage note`: the convention is "all unsigned 64-bit", not "Discord snowflakes"; Steam64 ids and any other `ulong` go through it, which is intended.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): mark ulong key parts as caller-supplied by convention"
```

---

## Task 3: Natural-key upsert

**Files:**
- Create: `src/Persistord.Core/UpsertExtensions.cs`, `src/Persistord.Core/UpsertResult.cs`
- Create: `tests/Persistord.Core.Tests/UpsertTests.cs`, `tests/Persistord.Core.Tests/SharedSqliteDatabase.cs`

**Interfaces:**
- Consumes: `DiscordDbContext` (Task 1).
- Produces:
  - `Task<TEntity> UpsertExtensions.UpsertAsync<TEntity>(this DbSet<TEntity>, Expression<Func<TEntity, bool>> naturalKey, Func<TEntity> create, Action<TEntity> update, CancellationToken = default) where TEntity : class`
  - `Task<UpsertResult<TEntity>> UpsertExtensions.UpsertIfChangedAsync<TEntity>(…same parameters…)`
  - `readonly record struct UpsertResult<TEntity>(TEntity Entity, bool Changed) where TEntity : class`
  - test helper `SharedSqliteDatabase` with `CreateContext<TContext>(Func<DbContextOptions<TContext>, TContext>, params IInterceptor[])` and `ConnectionString`.

**Design note (deviation from the spec, deliberate):** the spec asked for "an `UpsertAsync` overload returning `bool changed`". Two overloads differing only in the mutation delegate (`Action<T>` vs `Func<T, bool>`) are ambiguous at the call site whenever the mutation is a `bool` assignment (`row => row.IsActive = true` binds to both), so the changed-reporting variant gets its own name, `UpsertIfChangedAsync`, and keeps the same `Action<T>` delegate. `Changed` is read from the change tracker, not from the caller, so it is honest even when the caller assigns identical values.

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Core.Tests/SharedSqliteDatabase.cs`:

```csharp
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

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();
}
```

Create `tests/Persistord.Core.Tests/UpsertTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Persistord.Core.Tests;

public class UpsertTests
{
    private static WidgetEntity NewWidget(ulong guildId, string key) => new()
    {
        GuildId = guildId, Key = key
    };

    [Fact]
    public async Task Upsert_inserts_when_the_natural_key_is_missing()
    {
        var (connection, context) = SqliteFixture.Create(o => new UpsertContext(o));
        using (connection)
        await using (context)
        {
            var row = await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);

            Assert.Equal(42UL, row.DiscordId);
            Assert.Single(await context.Widgets.ToListAsync());
        }
    }

    [Fact]
    public async Task Upsert_updates_the_existing_row_instead_of_inserting_a_second()
    {
        var (connection, context) = SqliteFixture.Create(o => new UpsertContext(o));
        using (connection)
        await using (context)
        {
            await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);
            context.ChangeTracker.Clear();

            var row = await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 43UL);

            Assert.Equal(43UL, row.DiscordId);
            Assert.Single(await context.Widgets.ToListAsync());
        }
    }

    [Fact]
    public async Task UpsertIfChanged_reports_no_change_and_writes_nothing_when_values_match()
    {
        var counter = new SaveCountingInterceptor();
        var (connection, context) = SqliteFixture.Create(o => new UpsertContext(o));
        using (connection)
        await using (context)
        {
            await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);
            context.ChangeTracker.Clear();
        }

        var (secondConnection, secondContext) = SqliteFixture.Create(o => new UpsertContext(o));
        using (secondConnection)
        await using (secondContext)
        {
            // Fresh database for the second half: assert on the interceptor's count only.
            secondContext.Widgets.Add(NewWidget(1UL, "dash"));
            secondContext.Widgets.Local.Single().DiscordId = 42UL;
            await secondContext.SaveChangesAsync();
            secondContext.ChangeTracker.Clear();

            var result = await secondContext.Widgets.UpsertIfChangedAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);

            Assert.False(result.Changed);
            Assert.Equal(42UL, result.Entity.DiscordId);
        }

        Assert.Equal(0, counter.Saves);
    }

    [Fact]
    public async Task UpsertIfChanged_reports_a_change_when_a_value_differs()
    {
        var (connection, context) = SqliteFixture.Create(o => new UpsertContext(o));
        using (connection)
        await using (context)
        {
            await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);
            context.ChangeTracker.Clear();

            var result = await context.Widgets.UpsertIfChangedAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 99UL);

            Assert.True(result.Changed);
            Assert.Equal(99UL, result.Entity.DiscordId);
        }
    }

    [Fact]
    public async Task Upsert_recovers_when_another_writer_wins_the_insert_race()
    {
        await using var database = new SharedSqliteDatabase();
        await using (var schema = database.CreateContext(o => new UpsertContext(o)))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        var interference = new InterferingInsertInterceptor(database);
        await using var context = database.CreateContext(o => new UpsertContext(o), interference);

        var row = await context.Widgets.UpsertAsync(
            w => w.GuildId == 1UL && w.Key == "dash",
            () => NewWidget(1UL, "dash"),
            w => w.DiscordId = 99UL);

        Assert.Equal(99UL, row.DiscordId);

        await using var verify = database.CreateContext(o => new UpsertContext(o));
        var all = await verify.Widgets.ToListAsync();
        Assert.Single(all);
        Assert.Equal(99UL, all[0].DiscordId);
    }

    [Fact]
    public async Task Upsert_rethrows_when_the_failure_is_not_a_lost_race()
    {
        var (connection, context) = SqliteFixture.Create(o => new UpsertContext(o));
        using (connection)
        await using (context)
        {
            // Key is required in the store; a null value fails the insert and no row exists to
            // recover onto, so the original DbUpdateException must surface.
            await Assert.ThrowsAsync<DbUpdateException>(() => context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.Key = null!));
        }
    }

    [Fact]
    public async Task Upsert_guards_its_arguments()
    {
        var (connection, context) = SqliteFixture.Create(o => new UpsertContext(o));
        using (connection)
        await using (context)
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                context.Widgets.UpsertAsync(null!, () => NewWidget(1UL, "d"), _ => { }));
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                context.Widgets.UpsertAsync(w => w.Key == "d", null!, _ => { }));
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                context.Widgets.UpsertAsync(w => w.Key == "d", () => NewWidget(1UL, "d"), null!));
        }
    }

    private sealed class SaveCountingInterceptor : SaveChangesInterceptor
    {
        public int Saves { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Saves++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Inserts the same natural key from a second context while the first one saves.</summary>
    private sealed class InterferingInsertInterceptor(SharedSqliteDatabase database) : SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;
                await using var other = database.CreateContext(o => new UpsertContext(o));
                other.Widgets.Add(new WidgetEntity
                {
                    GuildId = 1UL, Key = "dash", DiscordId = 7UL
                });
                await other.SaveChangesAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

public sealed class WidgetEntity
{
    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public string Key { get; set; } = string.Empty;

    public ulong DiscordId { get; set; }
}

public sealed class UpsertContext(DbContextOptions<UpsertContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<WidgetEntity> Widgets => Set<WidgetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<WidgetEntity>().Property(w => w.Key).IsRequired();
        modelBuilder.Entity<WidgetEntity>().HasIndex(w => new
        {
            w.GuildId, w.Key
        }).IsUnique();
    }
}
```

Simplify `UpsertIfChanged_reports_no_change_and_writes_nothing_when_values_match` while writing it: the first half (a throwaway database) adds nothing — keep only the second half, and pass `counter` into the context via `SqliteFixture`… `SqliteFixture` does not take interceptors, so instead seed the row with `context.Widgets.Add(...)`/`SaveChangesAsync`, clear the tracker, and assert only `result.Changed == false` plus that `context.ChangeTracker.Entries().All(e => e.State == EntityState.Unchanged)` after the call. Drop `SaveCountingInterceptor` if it ends up unused.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~UpsertTests`
Expected: compile error — `UpsertAsync`/`UpsertIfChangedAsync` do not exist.

- [ ] **Step 3: Write the implementation**

Create `src/Persistord.Core/UpsertResult.cs`:

```csharp
namespace Persistord.Core;

/// <summary>The outcome of an upsert.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="Entity">The tracked row, freshly created or already present.</param>
/// <param name="Changed">
/// True when the call inserted or updated a row; false when the row was already up to date and
/// no write was issued — which also means no <c>UpdatedAt</c> stamp was applied.
/// </param>
public readonly record struct UpsertResult<TEntity>(TEntity Entity, bool Changed)
    where TEntity : class;
```

Create `src/Persistord.Core/UpsertExtensions.cs`:

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Persistord.Core;

/// <summary>
/// Natural-key upsert for rows the bot owns. This is a narrow helper, not a conflict-resolution
/// engine: read by natural key, create or mutate, save, and recover exactly once from a lost
/// insert race.
/// </summary>
public static class UpsertExtensions
{
    /// <summary>
    /// Creates or updates the single row matching <paramref name="naturalKey"/> and returns it.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="set">The set to read and write.</param>
    /// <param name="naturalKey">
    /// A predicate that matches at most one row. Back it with a unique index: that index is what
    /// turns a concurrent duplicate insert into the <see cref="DbUpdateException"/> this method
    /// recovers from.
    /// </param>
    /// <param name="create">Builds the row when none matches. The natural-key values belong here.</param>
    /// <param name="update">
    /// Applies the mutation. Called for a freshly created row too, so the caller writes the
    /// mutation once.
    /// </param>
    /// <param name="cancellationToken">Cancels the read and the save.</param>
    /// <returns>The tracked row.</returns>
    public static async Task<TEntity> UpsertAsync<TEntity>(
        this DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> naturalKey,
        Func<TEntity> create,
        Action<TEntity> update,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var result = await set
            .UpsertIfChangedAsync(naturalKey, create, update, cancellationToken)
            .ConfigureAwait(false);
        return result.Entity;
    }

    /// <summary>
    /// Same as <see cref="UpsertAsync{TEntity}"/>, but also reports whether the call actually
    /// wrote. Use it to skip the work that follows a no-op write.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="set">The set to read and write.</param>
    /// <param name="naturalKey">A predicate that matches at most one row.</param>
    /// <param name="create">Builds the row when none matches.</param>
    /// <param name="update">Applies the mutation, to created and existing rows alike.</param>
    /// <param name="cancellationToken">Cancels the read and the save.</param>
    /// <returns>The tracked row and whether it changed.</returns>
    public static async Task<UpsertResult<TEntity>> UpsertIfChangedAsync<TEntity>(
        this DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> naturalKey,
        Func<TEntity> create,
        Action<TEntity> update,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(naturalKey);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(update);

        var context = set.GetService<ICurrentDbContext>().Context;

        var existing = await set.SingleOrDefaultAsync(naturalKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await ApplyAsync(context, existing, update, cancellationToken).ConfigureAwait(false);
        }

        var created = create();
        update(created);
        set.Add(created);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new UpsertResult<TEntity>(created, true);
        }
        catch (DbUpdateException)
        {
            // Lost the insert race: another writer created the same natural key between our read
            // and our insert. Drop our copy, re-read the winner once, and apply the mutation to
            // it. If there is still no winner the failure was not a race and must surface.
            context.Entry(created).State = EntityState.Detached;

            var winner = await set.SingleOrDefaultAsync(naturalKey, cancellationToken).ConfigureAwait(false);
            if (winner is null)
            {
                throw;
            }

            return await ApplyAsync(context, winner, update, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<UpsertResult<TEntity>> ApplyAsync<TEntity>(
        DbContext context,
        TEntity row,
        Action<TEntity> update,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        update(row);

        // DbContext.Entry runs change detection for this entity, so the state below reflects the
        // mutation. Assigning identical values leaves the row Unchanged, which is the whole
        // dirty-check: no write, and no UpdatedAt stamp from the timestamp interceptor.
        if (context.Entry(row).State is not EntityState.Modified)
        {
            return new UpsertResult<TEntity>(row, false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new UpsertResult<TEntity>(row, true);
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~UpsertTests`
Expected: PASS, all seven.

If `set.GetService<ICurrentDbContext>()` does not compile, the accessor lives in `Microsoft.EntityFrameworkCore.Infrastructure` (`AccessorExtensions.GetService`) — check the using is present before changing the approach.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add natural-key UpsertAsync with lost-race recovery"
```

---

## Task 4: Timestamps

**Files:**
- Create: `src/Persistord.Core/Abstractions/ICreatedAt.cs`, `src/Persistord.Core/Abstractions/IUpdatedAt.cs`, `src/Persistord.Core/Interception/TimestampInterceptor.cs`
- Modify: `src/Persistord.Core/DiscordDbContext.cs`, `src/Persistord.Core/DiscordGraphDbContext.cs`
- Create: `tests/Persistord.Core.Tests/TimestampTests.cs`, `tests/Persistord.Core.Tests/TestTimeProvider.cs`

**Interfaces:**
- Consumes: `DiscordDbContext` (Task 1), `UpsertIfChangedAsync` (Task 3, for the no-op-does-not-stamp test).
- Produces: `Persistord.Core.Abstractions.ICreatedAt { DateTimeOffset CreatedAt { get; set; } }`, `IUpdatedAt { DateTimeOffset UpdatedAt { get; set; } }`, `Persistord.Core.Interception.TimestampInterceptor(TimeProvider? timeProvider = null)`, and `protected DiscordDbContext(DbContextOptions options, TimeProvider timeProvider)` / `protected DiscordGraphDbContext(DbContextOptions options, TimeProvider timeProvider)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Core.Tests/TestTimeProvider.cs`:

```csharp
namespace Persistord.Core.Tests;

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
```

Create `tests/Persistord.Core.Tests/TimestampTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Interception;
using Xunit;

namespace Persistord.Core.Tests;

public class TimestampTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Insert_stamps_created_and_updated()
    {
        var clock = new TestTimeProvider(Start);
        var (connection, context) = SqliteFixture.Create(o => new StampContext(o, clock));
        using (connection)
        await using (context)
        {
            context.Notes.Add(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.Equal(Start, note.CreatedAt);
            Assert.Equal(Start, note.UpdatedAt);
        }
    }

    [Fact]
    public async Task Update_moves_only_updated_at()
    {
        var clock = new TestTimeProvider(Start);
        var (connection, context) = SqliteFixture.Create(o => new StampContext(o, clock));
        using (connection)
        await using (context)
        {
            context.Notes.Add(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            clock.Now = Start.AddMinutes(5);
            context.Notes.Local.Single().Text = "changed";
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.Equal(Start, note.CreatedAt);
            Assert.Equal(Start.AddMinutes(5), note.UpdatedAt);
        }
    }

    [Fact]
    public async Task Caller_supplied_created_at_is_preserved()
    {
        var clock = new TestTimeProvider(Start);
        var (connection, context) = SqliteFixture.Create(o => new StampContext(o, clock));
        using (connection)
        await using (context)
        {
            var backfilled = Start.AddYears(-1);
            context.Notes.Add(new NoteEntity
            {
                Text = "imported", CreatedAt = backfilled
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.Equal(backfilled, note.CreatedAt);
            Assert.Equal(Start, note.UpdatedAt);
        }
    }

    [Fact]
    public async Task A_no_op_upsert_leaves_updated_at_alone()
    {
        var clock = new TestTimeProvider(Start);
        var (connection, context) = SqliteFixture.Create(o => new StampContext(o, clock));
        using (connection)
        await using (context)
        {
            await context.Notes.UpsertAsync(n => n.Text == "hello", () => new NoteEntity(), n => n.Text = "hello");
            context.ChangeTracker.Clear();

            clock.Now = Start.AddMinutes(5);
            var result = await context.Notes.UpsertIfChangedAsync(
                n => n.Text == "hello",
                () => new NoteEntity(),
                n => n.Text = "hello");

            Assert.False(result.Changed);
            Assert.Equal(Start, (await context.Notes.SingleAsync()).UpdatedAt);
        }
    }

    [Fact]
    public async Task Interceptor_defaults_to_the_system_clock()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var (connection, context) = SqliteFixture.Create(o => new StampContext(o, timeProvider: null));
        using (connection)
        await using (context)
        {
            context.Notes.Add(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.InRange(note.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
        }
    }

    [Fact]
    public void Interceptor_guards_its_time_provider() =>
        Assert.Throws<ArgumentNullException>(() => new TimestampInterceptor(null!));
}

public sealed class NoteEntity : ICreatedAt, IUpdatedAt
{
    public long Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StampContext : Persistord.Core.DiscordDbContext
{
    public StampContext(DbContextOptions<StampContext> options, TimeProvider? timeProvider)
        : base(options, timeProvider ?? TimeProvider.System)
    {
    }

    public DbSet<NoteEntity> Notes => Set<NoteEntity>();
}
```

Note: `Interceptor_guards_its_time_provider` pins that an explicit `null` throws; the parameterless/`default` path uses `TimeProvider.System`. Write `TimestampInterceptor`'s constructor as `TimestampInterceptor(TimeProvider? timeProvider = null)` where `null` **passed explicitly as `null!` from a non-nullable caller** is indistinguishable — so instead give it two constructors: a parameterless one that uses `TimeProvider.System`, and one taking a non-optional `TimeProvider` that guards. Adjust the test to match the two-constructor shape.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~TimestampTests`
Expected: compile error — `ICreatedAt`, `IUpdatedAt`, `TimestampInterceptor` and the two-argument base constructor do not exist.

- [ ] **Step 3: Write the interfaces and the interceptor**

Create `src/Persistord.Core/Abstractions/ICreatedAt.cs`:

```csharp
namespace Persistord.Core.Abstractions;

/// <summary>An entity whose insert time Persistord stamps.</summary>
public interface ICreatedAt
{
    /// <summary>
    /// When the row was first written. <c>TimestampInterceptor</c> fills it on insert unless the
    /// caller already set it, so a backfilled row keeps its real creation time.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }
}
```

Create `src/Persistord.Core/Abstractions/IUpdatedAt.cs`:

```csharp
namespace Persistord.Core.Abstractions;

/// <summary>An entity whose last-write time Persistord stamps.</summary>
public interface IUpdatedAt
{
    /// <summary>
    /// When the row was last written. <c>TimestampInterceptor</c> fills it on insert and on every
    /// update. A save that changes nothing does not touch it.
    /// </summary>
    DateTimeOffset UpdatedAt { get; set; }
}
```

Create `src/Persistord.Core/Interception/TimestampInterceptor.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistord.Core.Abstractions;

namespace Persistord.Core.Interception;

/// <summary>
/// Stamps <see cref="ICreatedAt"/> and <see cref="IUpdatedAt"/> entities as they are saved, from
/// a <see cref="TimeProvider"/>. Register it with
/// <c>options.AddInterceptors(new TimestampInterceptor(timeProvider))</c>, or pass the provider to
/// <see cref="DiscordDbContext"/>'s two-argument constructor and let the context register it.
/// </summary>
public sealed class TimestampInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the interceptor with <see cref="TimeProvider.System"/>.</summary>
    public TimestampInterceptor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes the interceptor with an explicit clock.</summary>
    /// <param name="timeProvider">The clock to stamp from. Tests inject a fake.</param>
    public TimestampInterceptor(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        // Entries() runs change detection first, so Added/Modified below are final.
        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is ICreatedAt created && created.CreatedAt == default)
                    {
                        created.CreatedAt = now;
                    }

                    if (entry.Entity is IUpdatedAt inserted)
                    {
                        inserted.UpdatedAt = now;
                    }

                    break;

                case EntityState.Modified:
                    if (entry.Entity is IUpdatedAt modified)
                    {
                        modified.UpdatedAt = now;
                    }

                    break;

                default:
                    break;
            }
        }
    }
}
```

- [ ] **Step 4: Add the constructor overloads**

Rewrite `src/Persistord.Core/DiscordDbContext.cs` from a primary constructor to two explicit ones:

```csharp
public abstract class DiscordDbContext : DbContext
{
    private readonly TimeProvider? _timeProvider;

    /// <summary>Initializes the context with the given options.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    protected DiscordDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes the context with the given options and registers a
    /// <see cref="TimestampInterceptor"/> driven by <paramref name="timeProvider"/>, so
    /// <see cref="ICreatedAt"/> and <see cref="IUpdatedAt"/> entities stamp themselves.
    /// </summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    /// <param name="timeProvider">The clock to stamp from.</param>
    protected DiscordDbContext(DbContextOptions options, TimeProvider timeProvider)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        base.OnConfiguring(optionsBuilder);

        if (_timeProvider is not null)
        {
            optionsBuilder.AddInterceptors(new TimestampInterceptor(_timeProvider));
        }
    }

    // ConfigureConventions unchanged from Tasks 1 and 2.
}
```

Give `DiscordGraphDbContext` the same pair (it currently uses a primary constructor):

```csharp
public abstract class DiscordGraphDbContext : DiscordDbContext
{
    /// <summary>Initializes the context with the given options.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    protected DiscordGraphDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>Initializes the context with the given options and a clock for timestamp stamping.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    /// <param name="timeProvider">The clock to stamp from.</param>
    protected DiscordGraphDbContext(DbContextOptions options, TimeProvider timeProvider)
        : base(options, timeProvider)
    {
    }

    // DbSets and OnModelCreating unchanged from Task 1.
}
```

Add a null-guard test for the new `OnConfiguring` override in `CoreNullGuardTests` (expose it through `ProbeContext` the same way `ProbeModel` exposes `OnModelCreating`).

- [ ] **Step 5: Run the tests**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): stamp ICreatedAt/IUpdatedAt from a TimeProvider"
```

---

## Task 5: `IGuildScoped` and the guild-scope index convention

**Files:**
- Create: `src/Persistord.Core/Abstractions/IGuildScoped.cs`, `src/Persistord.Core/Conventions/GuildScopeConvention.cs`
- Modify: `src/Persistord.Core/DiscordDbContext.cs` (register the convention)
- Create: `tests/Persistord.Core.Tests/GuildScopeConventionTests.cs`

**Interfaces:**
- Consumes: `DiscordDbContext.ConfigureConventions` (Task 1).
- Produces: `Persistord.Core.Abstractions.IGuildScoped { ulong GuildId { get; } }` and `Persistord.Core.Conventions.GuildScopeConvention`.

- [ ] **Step 1: Write the failing test**

Create `tests/Persistord.Core.Tests/GuildScopeConventionTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildScopeConventionTests
{
    private static IModel BuildModel()
    {
        var (connection, context) = SqliteFixture.Create(o => new ScopeProbeContext(o), createSchema: false);
        using (connection)
        using (context)
        {
            return context.Model;
        }
    }

    private static IReadOnlyList<string[]> IndexesOf(Type entity) =>
        BuildModel().FindEntityType(entity)!
            .GetIndexes()
            .Select(i => i.Properties.Select(p => p.Name).ToArray())
            .ToList();

    [Fact]
    public void Scoped_entity_gets_a_guild_id_index() =>
        Assert.Contains(IndexesOf(typeof(ScopedRow)), columns => columns.SequenceEqual([nameof(ScopedRow.GuildId)]));

    [Fact]
    public void Composite_key_starting_with_guild_id_needs_no_extra_index() =>
        Assert.Empty(IndexesOf(typeof(GuildFirstKeyRow)));

    [Fact]
    public void Composite_key_not_starting_with_guild_id_gets_one() =>
        Assert.Contains(
            IndexesOf(typeof(GuildLastKeyRow)),
            columns => columns.SequenceEqual([nameof(GuildLastKeyRow.GuildId)]));

    [Fact]
    public void An_existing_leading_guild_id_index_is_not_duplicated() =>
        Assert.Single(IndexesOf(typeof(AlreadyIndexedRow)));

    [Fact]
    public void An_unmarked_entity_with_a_guild_id_is_left_alone() =>
        Assert.Empty(IndexesOf(typeof(UnmarkedRow)));

    public sealed class ScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    public sealed class GuildFirstKeyRow : IGuildScoped
    {
        public ulong GuildId { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    public sealed class GuildLastKeyRow : IGuildScoped
    {
        public string Key { get; set; } = string.Empty;

        public ulong GuildId { get; set; }
    }

    public sealed class AlreadyIndexedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    public sealed class UnmarkedRow
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    private sealed class ScopeProbeContext(DbContextOptions<ScopeProbeContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScopedRow>();
            modelBuilder.Entity<GuildFirstKeyRow>().HasKey(e => new
            {
                e.GuildId, e.Key
            });
            modelBuilder.Entity<GuildLastKeyRow>().HasKey(e => new
            {
                e.Key, e.GuildId
            });
            modelBuilder.Entity<AlreadyIndexedRow>().HasIndex(e => new
            {
                e.GuildId, e.Key
            });
            modelBuilder.Entity<UnmarkedRow>();
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~GuildScopeConventionTests`
Expected: compile error — `IGuildScoped` does not exist.

- [ ] **Step 3: Write the interface and the convention**

Create `src/Persistord.Core/Abstractions/IGuildScoped.cs`:

```csharp
namespace Persistord.Core.Abstractions;

/// <summary>
/// A row that belongs to exactly one guild. Marking an entity with this interface opts it into
/// the guild-scope index convention, the cascading foreign key <c>ApplyGuildRoot</c> wires, and
/// <c>PurgeGuildAsync</c>. Implement <see cref="GuildId"/> as a public, settable property: the
/// helpers read it by name off the mapped CLR type.
/// </summary>
public interface IGuildScoped
{
    /// <summary>The owning guild snowflake id.</summary>
    ulong GuildId { get; }
}
```

Create `src/Persistord.Core/Conventions/GuildScopeConvention.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Persistord.Core.Abstractions;

namespace Persistord.Core.Conventions;

/// <summary>
/// Gives every <see cref="IGuildScoped"/> entity an index on <c>GuildId</c>, which is the column
/// every tenant-scoped query filters on. Skipped when the primary key or an existing index
/// already leads with <c>GuildId</c> — that index already serves the same lookups.
/// </summary>
public sealed class GuildScopeConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IGuildScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var guildId = entityType.FindProperty(nameof(IGuildScoped.GuildId));
            if (guildId is null || LeadsAnIndex(entityType, guildId.Name))
            {
                continue;
            }

            entityType.Builder.HasIndex(new[]
            {
                guildId.Name
            });
        }
    }

    private static bool LeadsAnIndex(IConventionEntityType entityType, string guildId)
    {
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is not null && string.Equals(primaryKey.Properties[0].Name, guildId, StringComparison.Ordinal))
        {
            return true;
        }

        return entityType.GetIndexes()
            .Any(index => string.Equals(index.Properties[0].Name, guildId, StringComparison.Ordinal));
    }
}
```

Register it in `ConfigureConventions`, after `SnowflakeKeyConvention`:

```csharp
        configurationBuilder.Conventions.Add(_ => new GuildScopeConvention());
```

- [ ] **Step 4: Run the tests**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS. `CoreConfigurationTests.GuildId_is_indexed` still passes — `ChannelEntity` and `RoleEntity` keep their explicit indexes and are not `IGuildScoped` (the skeleton stays out of the tenant model so an existing consumer's migrations do not move).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): index GuildId on IGuildScoped entities by convention"
```

---

## Task 6: Light guild root

**Files:**
- Modify: `src/Persistord.Core/Entities/GuildEntity.cs`, `src/Persistord.Core/Configurations/GuildEntityConfiguration.cs`
- Create: `src/Persistord.Core/GuildRootExtensions.cs`
- Modify: `tests/Persistord.Core.Tests/EntityDefaultsTests.cs`, `tests/Persistord.Core.Tests/CoreNullGuardTests.cs`
- Create: `tests/Persistord.Core.Tests/GuildRootTests.cs`
- Create: `samples/Persistord.Sample/Migrations/<timestamp>_GuildLifecycle.cs` (+ `.Designer.cs`), modify `samples/Persistord.Sample/Migrations/MyBotContextModelSnapshot.cs`
- Modify: `docs/articles/core-graph.md`

**Interfaces:**
- Consumes: `IGuildScoped` (Task 5), `ApplyCoreGraph` (Task 1).
- Produces: `ModelBuilder GuildRootExtensions.ApplyGuildRoot(this ModelBuilder modelBuilder, bool cascade = true, bool filterLeftGuilds = false)`; `GuildEntity` with `string? Name`, `ulong? OwnerId`, `DateTimeOffset? JoinedAt`, `DateTimeOffset? LeftAt`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Core.Tests/GuildRootTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildRootTests
{
    [Fact]
    public void Guild_name_and_owner_are_optional()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            var guild = context.Model.FindEntityType(typeof(GuildEntity))!;
            Assert.True(guild.FindProperty(nameof(GuildEntity.Name))!.IsNullable);
            Assert.True(guild.FindProperty(nameof(GuildEntity.OwnerId))!.IsNullable);
            Assert.True(guild.FindProperty(nameof(GuildEntity.JoinedAt))!.IsNullable);
            Assert.True(guild.FindProperty(nameof(GuildEntity.LeftAt))!.IsNullable);
        }
    }

    [Fact]
    public async Task An_id_only_guild_row_persists()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        await using (context)
        {
            context.Guilds.Add(new GuildEntity
            {
                Id = 7UL
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var guild = await context.Guilds.SingleAsync();
            Assert.Null(guild.Name);
            Assert.Null(guild.OwnerId);
        }
    }

    [Fact]
    public void ApplyGuildRoot_adds_a_cascading_fk_to_every_scoped_entity()
    {
        var (connection, context) = SqliteFixture.Create(o => new RootContext(o, cascade: true));
        using (connection)
        using (context)
        {
            var scoped = context.Model.FindEntityType(typeof(ScopedRow))!;
            var fk = Assert.Single(scoped.GetForeignKeys());

            Assert.Equal(typeof(GuildEntity), fk.PrincipalEntityType.ClrType);
            Assert.Equal(nameof(ScopedRow.GuildId), Assert.Single(fk.Properties).Name);
            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void ApplyGuildRoot_without_cascade_registers_the_root_and_no_fk()
    {
        var (connection, context) = SqliteFixture.Create(o => new RootContext(o, cascade: false));
        using (connection)
        using (context)
        {
            Assert.NotNull(context.Model.FindEntityType(typeof(GuildEntity)));
            Assert.Empty(context.Model.FindEntityType(typeof(ScopedRow))!.GetForeignKeys());
        }
    }

    [Fact]
    public async Task Deleting_a_guild_deletes_its_scoped_rows_in_the_database()
    {
        var (connection, context) = SqliteFixture.Create(o => new RootContext(o, cascade: true));
        using (connection)
        await using (context)
        {
            context.Guilds.Add(new GuildEntity
            {
                Id = 1UL
            });
            context.Scoped.AddRange(
                new ScopedRow
                {
                    GuildId = 1UL, Label = "a"
                },
                new ScopedRow
                {
                    GuildId = 1UL, Label = "b"
                });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            // ExecuteDelete goes straight to SQL, so this proves the database-level cascade
            // rather than EF's client-side fix-up of tracked dependents.
            await context.Guilds.Where(g => g.Id == 1UL).ExecuteDeleteAsync();

            Assert.Empty(await context.Scoped.ToListAsync());
        }
    }

    [Fact]
    public async Task The_left_guild_filter_hides_departed_guilds()
    {
        var (connection, context) = SqliteFixture.Create(o => new RootContext(o, cascade: true, filterLeftGuilds: true));
        using (connection)
        await using (context)
        {
            context.Guilds.AddRange(
                new GuildEntity
                {
                    Id = 1UL
                },
                new GuildEntity
                {
                    Id = 2UL, LeftAt = DateTimeOffset.UtcNow
                });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            Assert.Equal(1UL, (await context.Guilds.SingleAsync()).Id);
            Assert.Equal(2, await context.Guilds.IgnoreQueryFilters().CountAsync());
        }
    }

    public sealed class ScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    public sealed class RootContext(
        DbContextOptions<RootContext> options,
        bool cascade = true,
        bool filterLeftGuilds = false) : Persistord.Core.DiscordDbContext(options)
    {
        public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

        public DbSet<ScopedRow> Scoped => Set<ScopedRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyGuildRoot(cascade, filterLeftGuilds);
        }
    }
}
```

Also update `tests/Persistord.Core.Tests/EntityDefaultsTests.cs`: replace `GuildEntity_name_defaults_to_empty` with

```csharp
    [Fact]
    public void GuildEntity_name_defaults_to_null() => Assert.Null(new GuildEntity().Name);
```

and add `ApplyGuildRoot_throws_on_null` to `CoreNullGuardTests`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~GuildRootTests`
Expected: compile error — `GuildEntity.JoinedAt` and `ApplyGuildRoot` do not exist.

- [ ] **Step 3: Reshape `GuildEntity`**

Replace `src/Persistord.Core/Entities/GuildEntity.cs`:

```csharp
namespace Persistord.Core.Entities;

/// <summary>
/// A Discord guild (server), and the tenant root every <c>IGuildScoped</c> row hangs off.
/// Everything but the id is optional: a bot that owns Discord resources rather than mirroring
/// them stores an id and the lifecycle stamps and nothing else.
/// </summary>
public class GuildEntity
{
    /// <summary>The guild snowflake id (primary key).</summary>
    public ulong Id { get; set; }

    /// <summary>The guild name, when the consumer mirrors it.</summary>
    public string? Name { get; set; }

    /// <summary>The snowflake id of the guild owner, when the consumer mirrors it.</summary>
    public ulong? OwnerId { get; set; }

    /// <summary>When the bot joined the guild, when the consumer records it.</summary>
    public DateTimeOffset? JoinedAt { get; set; }

    /// <summary>
    /// When the bot left, or was removed from, the guild. <c>null</c> means the bot is still in
    /// it. The consumer decides whether a non-null value is a soft mark for a retention policy or
    /// the trigger for <c>PurgeGuildAsync</c>.
    /// </summary>
    public DateTimeOffset? LeftAt { get; set; }
}
```

Drop the `builder.Property(g => g.Name).IsRequired();` line from `GuildEntityConfiguration` (keep `HasKey` and `ValueGeneratedNever`).

- [ ] **Step 4: Write `ApplyGuildRoot`**

Create `src/Persistord.Core/GuildRootExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Configurations;
using Persistord.Core.Entities;

namespace Persistord.Core;

/// <summary>Model-building extensions that make <see cref="GuildEntity"/> the tenant root.</summary>
public static class GuildRootExtensions
{
    /// <summary>
    /// Registers <see cref="GuildEntity"/> as the guild root and, when <paramref name="cascade"/>
    /// is true, adds a cascading foreign key from every <see cref="IGuildScoped"/> entity's
    /// <c>GuildId</c> to it. Call it last in <c>OnModelCreating</c>: it wires the entity types the
    /// model knows about at that point.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="cascade">
    /// True (default) to add the foreign key with <see cref="DeleteBehavior.Cascade"/>, so deleting
    /// a guild row deletes everything scoped to it. This makes the guild row a prerequisite: insert
    /// it before any scoped row. False registers the root and leaves scoped entities untouched, which
    /// is the right choice when scoped rows may outlive their guild row.
    /// </param>
    /// <param name="filterLeftGuilds">
    /// True to add a global query filter that hides guilds with a non-null
    /// <see cref="GuildEntity.LeftAt"/>. Off by default. The filter applies to the root only —
    /// per-entity tenant filtering is not possible without ambient state, and the consumer resolves
    /// its guild from the interaction anyway.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyGuildRoot(
        this ModelBuilder modelBuilder,
        bool cascade = true,
        bool filterLeftGuilds = false)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new GuildEntityConfiguration());

        if (filterLeftGuilds)
        {
            modelBuilder.Entity<GuildEntity>().HasQueryFilter(g => g.LeftAt == null);
        }

        if (!cascade)
        {
            return modelBuilder;
        }

        var scoped = modelBuilder.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && typeof(IGuildScoped).IsAssignableFrom(e.ClrType))
            .Select(e => e.ClrType)
            .ToList();

        foreach (var clrType in scoped)
        {
            modelBuilder.Entity(clrType)
                .HasOne(typeof(GuildEntity))
                .WithMany()
                .HasForeignKey(nameof(IGuildScoped.GuildId))
                .OnDelete(DeleteBehavior.Cascade);
        }

        return modelBuilder;
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dtk dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS.

Then `dtk dtk dotnet build Persistord.slnx` — the samples set `Name = "Showcase Guild"` etc., which still compiles against `string?`.

- [ ] **Step 6: Regenerate the sample migration**

The sample's `Guilds` table changes shape (nullable `Name`, nullable `OwnerId`, two new columns), so the committed migration chain must move with it:

```bash
dotnet tool restore
dotnet ef migrations add GuildLifecycle \
  --project samples/Persistord.Sample/Persistord.Sample.csproj \
  --startup-project samples/Persistord.Sample/Persistord.Sample.csproj
dtk dotnet build Persistord.slnx
```

Expected: a new `samples/Persistord.Sample/Migrations/<timestamp>_GuildLifecycle.cs` plus its `.Designer.cs`, and an updated `MyBotContextModelSnapshot.cs`. Inspect the generated `Up` — on SQLite, relaxing a column to nullable is a table rebuild, which EF emits as create/copy/drop/rename. That is expected; do not hand-edit it.

- [ ] **Step 7: Document the root**

In `docs/articles/core-graph.md` `### GuildEntity`: show the new shape, say that `Name`/`OwnerId` are now optional, and explain `JoinedAt`/`LeftAt` plus `ApplyGuildRoot(cascade, filterLeftGuilds)` in three or four sentences. State plainly that this is a breaking change from `1.0.0-beta2`: a consumer with a `Guilds` table needs a migration, and code reading `guild.Name` now gets a `string?`.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(core)!: make GuildEntity a light tenant root with lifecycle columns

Name and OwnerId become optional, JoinedAt/LeftAt are added, and
ApplyGuildRoot wires a cascading FK from every IGuildScoped entity."
```

---

## Task 7: `PurgeGuildAsync` and `ClearAllTablesAsync`

**Files:**
- Create: `src/Persistord.Core/Internal/ModelDeleteOrder.cs`, `src/Persistord.Core/GuildPurgeExtensions.cs`, `src/Persistord.Core/DatabaseMaintenanceExtensions.cs`
- Create: `tests/Persistord.Core.Tests/GuildPurgeTests.cs`, `tests/Persistord.Core.Tests/ClearAllTablesTests.cs`
- Modify: `src/Persistord.Core/README.md`

**Interfaces:**
- Consumes: `IGuildScoped` (Task 5), `ApplyGuildRoot` (Task 6).
- Produces: `Task<int> GuildPurgeExtensions.PurgeGuildAsync(this DbContext context, ulong guildId, CancellationToken = default)` and `Task<int> DatabaseMaintenanceExtensions.ClearAllTablesAsync(this DbContext context, CancellationToken = default)`, both returning the number of rows deleted.

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Core.Tests/GuildPurgeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildPurgeTests
{
    private static async Task SeedAsync(PurgeContext context)
    {
        context.Guilds.AddRange(
            new GuildEntity
            {
                Id = 1UL
            },
            new GuildEntity
            {
                Id = 2UL
            });
        context.Parents.AddRange(
            new ScopedParent
            {
                Id = 10, GuildId = 1UL
            },
            new ScopedParent
            {
                Id = 20, GuildId = 2UL
            });
        context.Children.AddRange(
            new ScopedChild
            {
                GuildId = 1UL, ParentId = 10
            },
            new ScopedChild
            {
                GuildId = 2UL, ParentId = 20
            });
        context.Notes.AddRange(
            new ScopedNote
            {
                GuildId = 1UL
            },
            new ScopedNote
            {
                GuildId = 2UL
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Purge_removes_every_scoped_row_of_one_guild_and_the_guild_itself()
    {
        var (connection, context) = SqliteFixture.Create(o => new PurgeContext(o));
        using (connection)
        await using (context)
        {
            await SeedAsync(context);

            var deleted = await context.PurgeGuildAsync(1UL);

            Assert.Equal(4, deleted); // parent + child + note + guild row
            Assert.Empty(await context.Children.Where(c => c.GuildId == 1UL).ToListAsync());
            Assert.Empty(await context.Parents.Where(p => p.GuildId == 1UL).ToListAsync());
            Assert.Empty(await context.Notes.Where(n => n.GuildId == 1UL).ToListAsync());
            Assert.Empty(await context.Guilds.Where(g => g.Id == 1UL).ToListAsync());
        }
    }

    [Fact]
    public async Task Purge_leaves_other_guilds_alone()
    {
        var (connection, context) = SqliteFixture.Create(o => new PurgeContext(o));
        using (connection)
        await using (context)
        {
            await SeedAsync(context);

            await context.PurgeGuildAsync(1UL);

            Assert.Single(await context.Parents.ToListAsync());
            Assert.Single(await context.Children.ToListAsync());
            Assert.Single(await context.Notes.ToListAsync());
            Assert.Single(await context.Guilds.ToListAsync());
        }
    }

    [Fact]
    public async Task Purge_deletes_dependents_before_principals()
    {
        // ScopedChild -> ScopedParent is Restrict, so deleting the parent first would throw.
        var (connection, context) = SqliteFixture.Create(o => new PurgeContext(o));
        using (connection)
        await using (context)
        {
            await SeedAsync(context);

            var exception = await Record.ExceptionAsync(() => context.PurgeGuildAsync(1UL));

            Assert.Null(exception);
        }
    }

    [Fact]
    public async Task Purge_reaches_a_guild_hidden_by_the_left_filter()
    {
        var (connection, context) = SqliteFixture.Create(o => new PurgeContext(o, filterLeftGuilds: true));
        using (connection)
        await using (context)
        {
            context.Guilds.Add(new GuildEntity
            {
                Id = 1UL, LeftAt = DateTimeOffset.UtcNow
            });
            context.Notes.Add(new ScopedNote
            {
                GuildId = 1UL
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var deleted = await context.PurgeGuildAsync(1UL);

            Assert.Equal(2, deleted);
            Assert.Empty(await context.Guilds.IgnoreQueryFilters().ToListAsync());
        }
    }

    [Fact]
    public async Task Purge_joins_an_ambient_transaction_so_a_rollback_undoes_it()
    {
        var (connection, context) = SqliteFixture.Create(o => new PurgeContext(o));
        using (connection)
        await using (context)
        {
            await SeedAsync(context);

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                await context.PurgeGuildAsync(1UL);
                await transaction.RollbackAsync();
            }

            Assert.Equal(2, await context.Guilds.CountAsync());
            Assert.Equal(2, await context.Notes.CountAsync());
        }
    }

    [Fact]
    public async Task Purge_guards_its_context() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).PurgeGuildAsync(1UL));

    public sealed class ScopedParent : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    public sealed class ScopedChild : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }

        public long ParentId { get; set; }
    }

    public sealed class ScopedNote : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    public sealed class PurgeContext(DbContextOptions<PurgeContext> options, bool filterLeftGuilds = false)
        : Persistord.Core.DiscordDbContext(options)
    {
        public DbSet<GuildEntity> Guilds => Set<GuildEntity>();

        public DbSet<ScopedParent> Parents => Set<ScopedParent>();

        public DbSet<ScopedChild> Children => Set<ScopedChild>();

        public DbSet<ScopedNote> Notes => Set<ScopedNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScopedParent>().Property(p => p.Id).ValueGeneratedNever();
            modelBuilder.Entity<ScopedChild>()
                .HasOne<ScopedParent>()
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // cascade: false keeps the scoped tables free of a guild FK, which is the harder
            // case the purge helper exists for: nothing in the database deletes these rows.
            modelBuilder.ApplyGuildRoot(cascade: false, filterLeftGuilds);
        }
    }
}
```

Create `tests/Persistord.Core.Tests/ClearAllTablesTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class ClearAllTablesTests
{
    [Fact]
    public async Task Clear_empties_every_table_dependents_first()
    {
        var (connection, context) = SqliteFixture.Create(o => new GuildPurgeTests.PurgeContext(o));
        using (connection)
        await using (context)
        {
            context.Guilds.Add(new GuildEntity
            {
                Id = 1UL
            });
            context.Parents.Add(new GuildPurgeTests.ScopedParent
            {
                Id = 10, GuildId = 1UL
            });
            context.Children.Add(new GuildPurgeTests.ScopedChild
            {
                GuildId = 1UL, ParentId = 10
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var deleted = await context.ClearAllTablesAsync();

            Assert.Equal(3, deleted);
            Assert.Empty(await context.Guilds.ToListAsync());
            Assert.Empty(await context.Parents.ToListAsync());
            Assert.Empty(await context.Children.ToListAsync());
        }
    }

    [Fact]
    public async Task Clear_on_an_empty_database_deletes_nothing()
    {
        var (connection, context) = SqliteFixture.Create(o => new GuildPurgeTests.PurgeContext(o));
        using (connection)
        await using (context)
        {
            Assert.Equal(0, await context.ClearAllTablesAsync());
        }
    }

    [Fact]
    public async Task Clear_guards_its_context() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).ClearAllTablesAsync());
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj --filter FullyQualifiedName~Purge`
Expected: compile error — `PurgeGuildAsync` and `ClearAllTablesAsync` do not exist.

- [ ] **Step 3: Write the delete-order helper**

Create `src/Persistord.Core/Internal/ModelDeleteOrder.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Metadata;

namespace Persistord.Core.Internal;

/// <summary>
/// Orders entity types so every dependent comes before the principal it points at, which is what
/// lets plain <c>DELETE</c> statements run without tripping a restricted foreign key. The order
/// comes from the EF model, so it holds on every relational provider — no pragma, no
/// provider-specific deferral. Self-references and reference cycles are skipped; the rest of the
/// order stays deterministic.
/// </summary>
internal static class ModelDeleteOrder
{
    public static IReadOnlyList<IEntityType> Compute(IModel model, Func<IEntityType, bool> include)
    {
        var candidates = model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindPrimaryKey() is not null && include(e))
            .ToList();

        var selected = new HashSet<IEntityType>(candidates);
        var visited = new HashSet<IEntityType>();
        var ordered = new List<IEntityType>(candidates.Count);

        foreach (var entityType in candidates)
        {
            Visit(entityType, selected, visited, ordered);
        }

        return ordered;
    }

    private static void Visit(
        IEntityType entityType,
        HashSet<IEntityType> selected,
        HashSet<IEntityType> visited,
        List<IEntityType> ordered)
    {
        if (!visited.Add(entityType))
        {
            return;
        }

        foreach (var reference in entityType.GetReferencingForeignKeys())
        {
            var dependent = reference.DeclaringEntityType;
            if (dependent != entityType && selected.Contains(dependent))
            {
                Visit(dependent, selected, visited, ordered);
            }
        }

        ordered.Add(entityType);
    }
}
```

- [ ] **Step 4: Write the two extensions**

Create `src/Persistord.Core/GuildPurgeExtensions.cs`:

```csharp
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Persistord.Core.Internal;

namespace Persistord.Core;

/// <summary>Guild teardown helpers.</summary>
public static class GuildPurgeExtensions
{
    private static readonly MethodInfo DeleteScopedMethod = typeof(GuildPurgeExtensions)
        .GetMethod(nameof(DeleteScopedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Deletes every <see cref="IGuildScoped"/> row belonging to <paramref name="guildId"/>, then
    /// the <see cref="GuildEntity"/> row itself when that type is part of the model. Dependents go
    /// before principals, everything runs in one transaction — an ambient transaction is joined
    /// rather than nested — and global query filters are ignored, so a guild already marked
    /// <see cref="GuildEntity.LeftAt"/> is still purged.
    /// </summary>
    /// <param name="context">The context whose model and connection to use.</param>
    /// <param name="guildId">The guild to erase.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// The deletes are executed as SQL and do not update the change tracker. Call this from a fresh
    /// context, or clear the tracker afterwards.
    /// </remarks>
    public static async Task<int> PurgeGuildAsync(
        this DbContext context,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = ModelDeleteOrder.Compute(
            context.Model,
            e => typeof(IGuildScoped).IsAssignableFrom(e.ClrType));

        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var deleted = 0;
        foreach (var entityType in order)
        {
            deleted += await ((Task<int>)DeleteScopedMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, [context, guildId, cancellationToken])!)
                .ConfigureAwait(false);
        }

        if (context.Model.FindEntityType(typeof(GuildEntity)) is not null)
        {
            deleted += await context.Set<GuildEntity>()
                .IgnoreQueryFilters()
                .Where(g => g.Id == guildId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return deleted;
    }

    private static Task<int> DeleteScopedAsync<TEntity>(
        DbContext context,
        ulong guildId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Built by hand rather than written as `e => e.GuildId == guildId`: GuildId is declared on
        // IGuildScoped, and EF translates member access against the mapped CLR type.
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(
                Expression.Property(parameter, nameof(IGuildScoped.GuildId)),
                Expression.Constant(guildId)),
            parameter);

        return context.Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(predicate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
```

Create `src/Persistord.Core/DatabaseMaintenanceExtensions.cs`:

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Persistord.Core.Internal;

namespace Persistord.Core;

/// <summary>Whole-database helpers for tests and local tooling.</summary>
public static class DatabaseMaintenanceExtensions
{
    private static readonly MethodInfo DeleteAllMethod = typeof(DatabaseMaintenanceExtensions)
        .GetMethod(nameof(DeleteAllAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Deletes every row of every mapped entity type, dependents before principals, in one
    /// transaction. Plain <c>DELETE</c> on every relational provider: no <c>PRAGMA</c>, no
    /// <c>TRUNCATE</c>, no provider branch.
    /// </summary>
    /// <param name="context">The context whose model and connection to use.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// The deletes are executed as SQL and do not update the change tracker. Intended for test
    /// teardown and "reset my local database" tooling, not for production code paths.
    /// </remarks>
    public static async Task<int> ClearAllTablesAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = ModelDeleteOrder.Compute(context.Model, _ => true);

        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var deleted = 0;
        foreach (var entityType in order)
        {
            deleted += await ((Task<int>)DeleteAllMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, [context, cancellationToken])!)
                .ConfigureAwait(false);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return deleted;
    }

    private static Task<int> DeleteAllAsync<TEntity>(DbContext context, CancellationToken cancellationToken)
        where TEntity : class =>
        context.Set<TEntity>().IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
}
```

- [ ] **Step 5: Run the tests**

Run: `dtk dotnet test tests/Persistord.Core.Tests/Persistord.Core.Tests.csproj`
Expected: PASS.

If `Purge_removes_every_scoped_row_of_one_guild_and_the_guild_itself` reports a different count than 4, read the actual number before changing the implementation — the assertion counts three scoped rows plus the guild row.

- [ ] **Step 6: Document the new Core surface**

In `src/Persistord.Core/README.md`, add a "What's in the box" list covering: `DiscordDbContext` (conventions), `DiscordGraphDbContext` (skeleton), `ApplyCoreGraph`, `ApplyGuildRoot`, `IGuildScoped`, `ICreatedAt`/`IUpdatedAt` + `TimestampInterceptor`, `UpsertAsync`/`UpsertIfChangedAsync`, `PurgeGuildAsync`, `ClearAllTablesAsync`. One line each, with the signature.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): add PurgeGuildAsync and ClearAllTablesAsync"
```

---

## Task 8: Branch verification

**Files:** no new files; fixes anywhere they are needed.

**Interfaces:**
- Consumes: everything above.
- Produces: a branch that passes the same gates CI runs.

- [ ] **Step 1: Full build**

Run: `dtk dotnet build Persistord.slnx --configuration Release`
Expected: `Build succeeded`, zero warnings. Warnings are errors here, so any output is a failure to fix.

- [ ] **Step 2: Full test run**

Run: `dtk dotnet test Persistord.slnx --configuration Release --no-build`
Expected: all tests pass. The Postgres provider tests skip when Docker is unavailable — a skip is not a failure, but confirm they were *skipped* and not *failed*.

- [ ] **Step 3: Formatting gate**

```bash
dotnet tool restore
dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder" --no-build --verbosity=ERROR
git diff --exit-code
```

Expected: no diff. If there is one, it is the formatter's — stage it.

- [ ] **Step 4: Confirm the skeleton really is opt-in**

```bash
dtk dotnet run --project samples/Persistord.Sample.CoreGraph
```

Expected: the sample prints the snowflake round-trip and channel counts as before — the graph still works through `DiscordGraphDbContext`.

- [ ] **Step 5: Commit anything the previous steps changed**

```bash
git add -A
git commit -m "chore: formatting and verification fixes for the bot-fit core work"
```

---

## Self-Review Notes

- **Spec §1.3 acceptance** — "a context deriving from `DiscordDbContext` with no calls produces a model with zero entity types" is `GraphSplitTests.Conventions_only_context_maps_no_entity_types`; "a consumer `ulong` key has `ValueGenerated == Never` without any fluent call" is `SnowflakeKeyConventionTests.Consumer_ulong_key_is_caller_supplied_without_any_fluent_call`.
- **Spec §2.3 acceptance** — cascade is `GuildRootTests.Deleting_a_guild_deletes_its_scoped_rows_in_the_database`; purge-without-FK in one transaction is `GuildPurgeTests` (its `PurgeContext` deliberately uses `cascade: false`, and the ambient-transaction test proves the transaction); the composite-key index case is `GuildScopeConventionTests.Composite_key_not_starting_with_guild_id_gets_one`.
- **Spec §4.3 acceptance** — the interceptor-injected interfering write is `UpsertTests.Upsert_recovers_when_another_writer_wins_the_insert_race`.
- **Spec §5** — `TimestampInterceptor` covers the `TimeProvider` default, the constructor overload, and the no-op case. The SQLite `ORDER BY DateTimeOffset` caveat is documentation, and belongs to the Providers article in the sibling plan.
- **Deliberate deviations, both recorded in the tasks that make them:** `UpsertIfChangedAsync` instead of an ambiguous `UpsertAsync` overload (Task 3); the five skeleton entities are *not* marked `IGuildScoped`, so an existing graph consumer's migrations do not move (Task 5) — a graph consumer who wants cascade can call `ApplyGuildRoot` and add the marker to their own types.
- **Left to the sibling plan:** `Persistord.Testing`, `Persistord.Managed`, `Persistord.Protection`, the Providers/Guild-lifecycle/Upsert/Managed articles, and CD/Mutation wiring for the new packages.
