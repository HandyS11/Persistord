# Persistord.Testing

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Testing.svg?label=Persistord.Testing)](https://www.nuget.org/packages/Persistord.Testing)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Testing.svg)](https://www.nuget.org/packages/Persistord.Testing)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Test-only helpers for [Persistord](https://github.com/HandyS11/Persistord)-based
`DbContext`s: an in-memory SQLite fixture and the `IModelCacheKeyFactory` it needs to
give every test a fresh model. It is safe — and intended — to reference from test
projects only; it ships no runtime dependency your bot needs in production.

## `SqliteTestDatabase.Private` — the default

`Private` opens one SQLite connection to `DataSource=:memory:` and hands that same
connection object to every context you create from it, because `:memory:` is a
*different* database per connection — without sharing the connection, a second
context would see an empty database.

```csharp
await using var database = SqliteTestDatabase.Private();
await using var context = database.CreateContext<MyContext>(o => new MyContext(o));

context.Widgets.Add(new Widget { GuildId = 1UL, Key = "a" });
await context.SaveChangesAsync();
```

## `SqliteTestDatabase.Shared` — independent connections, same data

`Private` breaks down the moment your code under test opens its own connection —
a DI scope, a background writer, two contexts racing on the same row. `Shared` uses
SQLite's shared-cache mode instead: it hands out a connection *string*, so every
caller opens its own connection, and all of them see the same in-memory database.

```csharp
await using var database = SqliteTestDatabase.Shared();

await using (var writer = database.CreateContext<MyContext>(o => new MyContext(o)))
{
    writer.Widgets.Add(new Widget { GuildId = 1UL, Key = "a" });
    await writer.SaveChangesAsync();
}

// A second, independent connection — built from the connection string, not the
// connection object — still sees the row the first one wrote. This only works
// because `writer`, above, came from `CreateContext` and already built the schema:
// `Options` itself never calls Migrate/EnsureCreated, so a `Shared` database needs
// at least one `CreateContext` call before any `Options`-only reader can query it.
await using var reader = new MyContext(database.Options<MyContext>());
```

## `TestSchema.Migrate` vs `TestSchema.EnsureCreated`

`Migrate` (the default) applies your context's committed migrations, so a model that
has drifted from what is actually checked in fails in the test instead of in
production. `EnsureCreated` builds the schema straight from the current model
instead — faster, but blind to migration drift. Pass `TestSchema.EnsureCreated` to
`Private`/`Shared` when a test does not care about migrations and speed matters more.

## Overriding the base setup: `configure`

`Options` and `CreateContext` each have an overload that takes an
`Action<DbContextOptionsBuilder<TContext>> configure`, applied *after* the base
setup (the connection, the model cache key factory, the interceptors) so it can
override anything — most importantly, to pin a context-wide
`QueryTrackingBehavior`. This matters for regression tests that must reproduce a bug
that only showed up under `QueryTrackingBehavior.NoTracking`:

```csharp
await using var context = database.CreateContext<MyContext>(
    o => new MyContext(o),
    configure: builder => builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```

There are two overloads rather than one optional `configure` parameter, because an
optional parameter cannot precede a `params` array without call-site ambiguity.

## Model assertions

`ModelAssertions` are three `DbContext` extension methods about the *shape* of the model,
so a schema test is one line instead of a seed-mutate-assert round trip against the
database. Every failure throws a plain `InvalidOperationException` naming the entity and
what was expected — the package takes no dependency on any test framework, so these work
from xunit, NUnit and MSTest alike.

```csharp
// A unique index over exactly these properties, in order.
context.AssertUniqueIndex<Membership>(nameof(Membership.GuildId), nameof(Membership.UserId));

// The child cascades from the parent: the one-line replacement for an
// insert-parent, insert-child, delete-parent, assert-empty test.
context.AssertCascade<Membership, GuildEntity>();

// The entity's primary key is a caller-supplied ulong, never store-generated, and
// stored as a long — the shape ApplyGuildRoot and DiscordDbContext expect.
context.AssertSnowflakeKey<GuildEntity>();
```

## `UniqueModelCacheKeyFactory`

EF Core caches the compiled model per context type by default, so the model is
normally built once for the whole test run. `SqliteTestDatabase` replaces that cache
key factory with `UniqueModelCacheKeyFactory`, which returns a new key on every call,
so **every context you create gets a freshly built model**. Without it:

- Per-test coverage of your entity configurations collapses to whichever test
  happens to run first and builds the model.
- Configuration mutants (a Stryker mutation that changes `HasIndex` or
  `IsRequired`, say) can survive, because the mutated configuration never actually
  ran for the tests that would have caught it.
- Two contexts configured differently in the same run could silently share one
  cached model.

You do not need to reference `UniqueModelCacheKeyFactory` yourself —
`SqliteTestDatabase` installs it on every context it builds.

## License

MIT
