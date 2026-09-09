# Testing

`Persistord.Testing` ships an in-memory SQLite fixture and the model assertions
built on it, so a schema test is one line instead of a seed-mutate-assert round
trip against the database. Reference it from test projects only — it ships no
runtime dependency your bot needs in production.

## `SqliteTestDatabase.Private` vs `.Shared`

`Private()` opens one SQLite connection to `DataSource=:memory:` and hands that
same connection object to every context you create from it. `:memory:` is a
*different* database per connection, so without sharing the connection, a second
context would see an empty database.

```csharp
await using var database = SqliteTestDatabase.Private();
await using var context = database.CreateContext<MyBotContext>(o => new MyBotContext(o));

context.Categories.Add(new ManagedCategory { GuildId = 1UL, Key = "events" });
await context.SaveChangesAsync();
```

`Shared(name?)` uses SQLite's shared-cache mode instead: it hands out a
connection *string*, so every caller opens its own connection, and all of them see
the same in-memory database. Reach for `Shared` the moment your code under test
opens its own connection — a DI scope, a background writer, two contexts racing on
the same row. `Private` cannot model that: it has exactly one connection.

```csharp
await using var database = SqliteTestDatabase.Shared();

await using (var writer = database.CreateContext<MyBotContext>(o => new MyBotContext(o)))
{
    writer.Categories.Add(new ManagedCategory { GuildId = 1UL, Key = "events" });
    await writer.SaveChangesAsync();
}

// A second, independent connection, built from the connection string rather than
// a shared connection object — this only works because `writer` above came from
// `CreateContext` and already built the schema.
await using var reader = new MyBotContext(database.Options<MyBotContext>());
```

**`Options<TContext>()` never builds the schema — only `CreateContext<TContext>()`
does.** Calling `Options` alone and querying against it before any `CreateContext`
call has run against the same database fails with a raw
`SqliteException: no such table`.

## `TestSchema.Migrate` vs `TestSchema.EnsureCreated`

`Migrate` is the default: it applies your context's committed migrations, so a
model that has drifted from what is actually checked in fails in the test instead
of in production. `EnsureCreated` builds the schema straight from the current
model instead — faster, but blind to migration drift. Pass
`TestSchema.EnsureCreated` to `Private`/`Shared` when a test does not care about
migrations and speed matters more than drift detection:

```csharp
await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
```

## `UniqueModelCacheKeyFactory`

EF Core caches the compiled model per context type by default, so normally the
model is built once for the whole test run. `SqliteTestDatabase` installs
`UniqueModelCacheKeyFactory` on every context it builds, which returns a fresh key
on every call, so **every context gets a freshly built model**. Without it:

- Per-test coverage of your entity configurations collapses to whichever test
  happens to run first and builds the model — later tests exercise a
  cached model, not their own configuration.
- A configuration mutant (a mutation testing tool flipping `HasIndex` or
  `IsRequired`, say) can survive, because the mutated configuration never actually
  ran for the tests that would have caught it.
- Two contexts configured differently in the same run could silently share one
  cached model.

You do not need to reference `UniqueModelCacheKeyFactory` directly —
`SqliteTestDatabase` installs it for you.

## Model assertions

`ModelAssertions` are three `DbContext` extension methods about the *shape* of the
model. Every failure throws a plain `InvalidOperationException` naming the entity
and what was expected — **the package takes no dependency on any test framework**,
so these work from xunit, NUnit, and MSTest alike.

### `AssertUniqueIndex<T>(params string[] columns)`

Asserts a unique index over exactly these properties, in order:

```csharp
context.AssertUniqueIndex<ManagedChannel>(
    nameof(ManagedChannel.GuildId), nameof(ManagedChannel.Scope), nameof(ManagedChannel.Key));
```

### `AssertCascade<TChild, TParent>()`

Asserts the child has exactly one foreign key to the parent and that it deletes
with `DeleteBehavior.Cascade` — the one-line replacement for an
insert-parent, insert-child, delete-parent, assert-empty test:

```csharp
context.AssertCascade<ManagedChannel, GuildEntity>();
```

It **throws on ambiguity**: a child with two foreign keys to the same principal is
not something this assertion will guess at by picking whichever one EF happens to
return first. A type with two relationships to the same principal needs a more
specific, hand-written assertion instead.

### `AssertSnowflakeKey<T>()`

Asserts the entity's primary key is a caller-supplied `ulong` (or `ulong?`),
`ValueGenerated.Never`, and stored as a `long` — the shape `DiscordDbContext`'s
snowflake convention and `ApplyGuildRoot` expect:

```csharp
context.AssertSnowflakeKey<GuildEntity>();
```

## See also

- [Managed Resources](managed-resources.md) — a worked model to test with the
  assertions above.
- [Protection](protection.md) — why a test that needs its own key ring per
  instance relies on the same `IModelCacheKeyFactory` replacement this package
  installs.
