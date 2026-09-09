# Protection

`Persistord.Protection` encrypts `[Protected]` string **columns** of a context at
rest, using [ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction).
It replaces hand-rolled `protector.Protect(...)` calls scattered across every write
path — miss one, and that value silently lands in the database as plaintext — with
one model-wide declaration.

## What it protects, and what it does not

It protects the value of an annotated `string` property, nothing more. It does not
encrypt the database file, the connection, backups, or any other column. A row with
one `[Protected]` column and nine plain ones has only that one column's value
encrypted; the rest is exactly as readable as it would be without this package.

The `[Protected]` attribute lives in `Persistord.Core.Abstractions`, so a package
like `Persistord.Managed` can annotate a column (`ManagedWebhook.Token`) without
depending on `Persistord.Protection` at all. It is honoured in four places: on the
property itself, on a base class the entity inherits from, on an interface member
the entity implements, and on a `string` property nested inside an EF complex type
(`ComplexProperty`) — the walk recurses into every complex type reachable from the
entity, so a `[Protected]` member behind one is found exactly like one declared
directly on the entity. It is a silent no-op on a non-`string` property — there is
no error or warning, the annotation simply changes nothing.

**Every write is non-deterministic ciphertext.** `IDataProtector.Protect`
randomises its output, so protecting the same plaintext twice produces two
different stored values. This has consequences beyond "you can't read it in a
database tool":

- A `[Protected]` column cannot be queried by equality — `WHERE Token = @value`
  never matches a row encrypted from `@value`, because the stored ciphertext isn't
  a function of the plaintext alone.
- It cannot usefully back a unique index for the same reason: two writes of the
  same logical value produce different bytes, so the index enforces uniqueness of
  ciphertext, not of the secret.
- The ciphertext is substantially longer than the plaintext — over 130 characters
  for a short secret — which overflows any `HasMaxLength` sized for the plaintext.
  Size the column for ciphertext, not for the value it represents.

## Setup

### 1. Reference the package and annotate the secret

```csharp
public sealed class Webhook
{
    [Protected]
    public string Token { get; set; } = string.Empty;
}
```

### 2. Register Data Protection with a persisted key ring

Do this once, in your host's startup — see [Key ring](#key-ring) below for why the
default location is the wrong choice for a bot.

```csharp
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/my-bot/keys"));
```

### 3. Register `ProtectedStringConvention` from `ConfigureConventions`

This is the recommended entry point. Constructor-inject `IDataProtectionProvider`
into your context and add the convention alongside Persistord's own:

```csharp
public sealed class MyBotContext(
    DbContextOptions<MyBotContext> options,
    IDataProtectionProvider dataProtectionProvider) : DiscordDbContext(options)
{
    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new ProtectedStringConvention(dataProtectionProvider));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();
    }
}
```

Because `IModelFinalizingConvention` runs at model finalization — after every
module and entity configuration has had a chance to add properties — it cannot be
called too early the way the alternative below can, and it covers an entity type
registered after the point an `ApplyProtection` call would already have run.

### The explicit alternative: `ApplyProtection`, called last

`Persistord.Protection` also ships `ApplyProtection`, for call sites that
configure protection inline in `OnModelCreating` rather than through a
convention. Call it last, after every module and entity configuration that
creates the annotated properties — it walks the model as already built, so a
property configured afterwards is not seen:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyManagedModule();
    modelBuilder.ApplyProtection(dataProtectionProvider); // last
}
```

Both routes share the same property walk, so both cover a `[Protected]` member
nested inside an EF complex type identically. They do **not** agree on precedence
against a competing explicit configuration — see [Precedence](#precedence) below.

## Precedence

`ProtectedStringConvention` applies its converter at EF's `DataAnnotation`
configuration-source precedence (`fromDataAnnotation: true`), because `[Protected]`
genuinely is a data annotation. That beats the plain `Convention` precedence a
convention gets by default, but it still loses to an **explicit fluent
`HasConversion(...)`** the consumer configures on the same property in
`OnModelCreating` — `Explicit` outranks `DataAnnotation` in EF's own precedence
order. In that one case, the property keeps the consumer's own converter, and
`Persistord.Protection` does not encrypt it. No error, no warning.

`ApplyProtection` does not have this gap. It writes through the raw mutable
property setter, which is not precedence-aware: called last, it unconditionally
overwrites whatever converter — explicit or otherwise — was configured on the
property before it. If a `[Protected]` property must be protected even when it
also carries its own custom conversion, use `ApplyProtection`, called after that
custom conversion is configured, rather than the convention.

```csharp
public sealed class Widget
{
    [Protected]
    public string Token { get; set; } = string.Empty;
}

// If OnModelCreating also configures:
modelBuilder.Entity<Widget>().Property(w => w.Token).HasConversion(myOwnConverter);

// ...then registering ProtectedStringConvention leaves Token on myOwnConverter,
// unprotected — Explicit beats the convention's DataAnnotation precedence.
// ApplyProtection(dataProtectionProvider), called after the line above, overwrites
// myOwnConverter with the protecting one instead.
```

This is a narrow case — a `[Protected]` property that also needs its own custom
conversion — and neither route tries to merge the two converters together.

## The purpose string is fixed

Every protector this package creates — whether through `ProtectedStringConvention`
or `ApplyProtection` — is derived from `ProtectionPurposes.V1`
(`"Persistord.Protection.v1"`). This cannot change without orphaning every
ciphertext already in the database: a protector derived from a different purpose
cannot decrypt what this one wrote.

## Key ring

Data Protection encrypts with keys held in a *key ring*. This package does not
configure where that key ring lives — that is
[`IDataProtectionBuilder`](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
configuration, done once when you register Data Protection, as in step 2 above.
Persistord itself references only `Microsoft.AspNetCore.DataProtection.Abstractions`
— the implementation package, `Microsoft.AspNetCore.DataProtection`, is a
dependency you add yourself in step 2, and keeping it patched is on you too.

**Losing the key ring means losing every protected value.** There is no recovery
path: without the key that encrypted a value, `Unprotect` cannot produce it back.
The default key ring location is a per-user profile folder, which does not travel
with your database and is easy to lose on redeploy, container recreation, or a
new host. **Persist it explicitly, next to the database**, with
`PersistKeysToFileSystem`, and back that folder up on the same schedule as the
database itself — a database backup without its matching key-ring backup is a
database full of ciphertext you cannot read.

### What a failure looks like

Rotation alone does not break decryption: Data Protection keeps every key it has
ever used in the ring, and each one keeps decrypting values it encrypted, even
after a newer key becomes the default for new writes. A read only fails when the
specific key that encrypted a value is actually gone from the ring — deleted, or
explicitly revoked. That failure surfaces as
`System.Security.Cryptography.CryptographicException`, thrown **while EF
materializes the entity**, not at the point you call `SaveChangesAsync` or
`Protect`.

`CryptographicException` alone does not tell you whether the key was deleted or
revoked, and the two do not recover the same way. `ProtectedStringConverter` calls
plain `IDataProtector.Unprotect`, not `IPersistedDataProtector.DangerousUnprotect`,
so it never bypasses revocation:

- **Deleted.** If the key file itself is gone, restoring it from a backup of the
  key ring folder — taken before the deletion — fixes the read: the key is no
  longer missing, and `Unprotect` succeeds again.
- **Revoked.** A revoked key throws `CryptographicException` even when its file is
  present and intact in the ring; revocation is enforced regardless of whether the
  key file exists, and restoring the file changes nothing. Reading data under a
  revoked key requires `IPersistedDataProtector.DangerousUnprotect(...,
  ignoreRevocationErrors: true, ...)`, which this package does not call and does
  not expose. There is no supported way to recover a revoked key's data through
  `Persistord.Protection` today — treat a deliberate revocation as permanent for
  this package's purposes.

## One provider per application

EF Core caches the compiled model per context type. Whichever route builds that
model first — the convention or `ApplyProtection` — bakes the protector it was
given into the cached model, so **the first `IDataProtectionProvider` supplied to
a given context type is the one every subsequent instance of that context type
uses**, for the lifetime of the process — passing a different provider on a later
call has no effect. Register one `IDataProtectionProvider` per application, as a
singleton, and constructor-inject it into the context.

Tests that need a different key ring per instance must also replace EF's
`IModelCacheKeyFactory`, so each configuration gets its own cache entry —
`Persistord.Testing`'s `SqliteTestDatabase` already does this for every context it
builds (see [Testing](testing.md)).

## The plaintext warning

`Persistord.Managed`'s `ManagedWebhook.Token` is annotated `[Protected]`, but the
attribute alone changes nothing. **Without a reference to `Persistord.Protection`
and either registering `ProtectedStringConvention` or calling `ApplyProtection`,
`ManagedWebhook.Token` is stored in plaintext.** See
[Managed Resources](managed-resources.md) for the entity shape.

## See also

- [Managed Resources](managed-resources.md) — the entity whose `Token` column this
  package protects.
- [Testing](testing.md) — the `IModelCacheKeyFactory` replacement that lets tests
  use a different key ring per context instance.
