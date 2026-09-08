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
depending on `Persistord.Protection` at all. It is honoured in three places: on the
property itself, on a base class the entity inherits from, and on an interface
member the entity implements. It is a silent no-op on a non-`string` property —
there is no error or warning, the annotation simply changes nothing.

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

### 3. Call `ApplyProtection` last in `OnModelCreating`

Constructor-inject `IDataProtectionProvider` into your context and call
`ApplyProtection` after every module and entity configuration that creates the
annotated properties — it walks the model as already built, so a property
configured afterwards is not seen.

```csharp
public sealed class MyBotContext(
    DbContextOptions<MyBotContext> options,
    IDataProtectionProvider dataProtectionProvider) : DiscordDbContext(options)
{
    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();
        modelBuilder.ApplyProtection(dataProtectionProvider); // last
    }
}
```

## The purpose string is fixed

Every protector `ApplyProtection` creates is derived from `ProtectionPurposes.V1`
(`"Persistord.Protection.v1"`). This cannot change without orphaning every
ciphertext already in the database: a protector derived from a different purpose
cannot decrypt what this one wrote.

## Key ring

Data Protection encrypts with keys held in a *key ring*. This package does not
configure where that key ring lives — that is
[`IDataProtectionBuilder`](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
configuration, done once when you register Data Protection, as in step 2 above.

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

`CryptographicException` alone does not tell you whether the key is truly lost or
just missing from *this* key ring. The distinguishing step is operational: check
whether the key ring folder — or a backup of it taken before whatever changed —
still holds the key that encrypted the failing rows. If it does, even marked
revoked, restoring that file fixes the read. If it does not exist anywhere, the
value cannot be recovered.

## One provider per application

EF Core caches the compiled model per context type. `ApplyProtection` bakes the
protector it was given into that cached model, so **the first
`IDataProtectionProvider` supplied to a given context type is the one every
subsequent instance of that context type uses**, for the lifetime of the process —
passing a different provider on a later call has no effect. Register one
`IDataProtectionProvider` per application, as a singleton, and constructor-inject
it into the context.

Tests that need a different key ring per instance must also replace EF's
`IModelCacheKeyFactory`, so each configuration gets its own cache entry —
`Persistord.Testing`'s `SqliteTestDatabase` already does this for every context it
builds (see [Testing](testing.md)).

## The plaintext warning

`Persistord.Managed`'s `ManagedWebhook.Token` is annotated `[Protected]`, but the
attribute alone changes nothing. **Without a reference to `Persistord.Protection`
and a call to `ApplyProtection`, `ManagedWebhook.Token` is stored in plaintext.**
See [Managed Resources](managed-resources.md) for the entity shape.

## See also

- [Managed Resources](managed-resources.md) — the entity whose `Token` column this
  package protects.
- [Testing](testing.md) — the `IModelCacheKeyFactory` replacement that lets tests
  use a different key ring per context instance.
