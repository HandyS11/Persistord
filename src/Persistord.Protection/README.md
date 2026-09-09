# Persistord.Protection

<div align="center">

[![NuGet](https://img.shields.io/nuget/v/Persistord.Protection.svg?label=Persistord.Protection)](https://www.nuget.org/packages/Persistord.Protection)
[![Downloads](https://img.shields.io/nuget/dt/Persistord.Protection.svg)](https://www.nuget.org/packages/Persistord.Protection)

[← Persistord docs](https://github.com/HandyS11/Persistord#readme) ·
[Documentation site](https://handys11.github.io/Persistord/)

</div>

Encrypts `[Protected]` string columns of a
[Persistord](https://github.com/HandyS11/Persistord) context at rest, using
[ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction).
It replaces hand-rolled calls to `protector.Protect(...)` scattered across
every write path — miss one, and that value silently lands in the database as
plaintext — with a single model-wide declaration: annotate the property, call
one extension method, and every write to it goes through the same converter.

## Setup

1. Reference this package.
2. Annotate the secret with `[Protected]`
   (`Persistord.Core.Abstractions.ProtectedAttribute`):

   ```csharp
   public sealed class Webhook
   {
       [Protected]
       public string Token { get; set; } = string.Empty;
   }
   ```
3. Register `ProtectedStringConvention` from `ConfigureConventions` — the
   recommended entry point. It runs at model finalization, after every module
   and entity configuration has had a chance to add properties, so unlike the
   `ApplyProtection` alternative below it cannot be called too early:

   ```csharp
   public sealed class MyBotContext(
       DbContextOptions<MyBotContext> options,
       IDataProtectionProvider dataProtectionProvider) : DiscordDbContext(options)
   {
       public DbSet<Webhook> Webhooks => Set<Webhook>();

       protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
       {
           base.ConfigureConventions(configurationBuilder);
           configurationBuilder.Conventions.Add(_ => new ProtectedStringConvention(dataProtectionProvider));
       }

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           base.OnModelCreating(modelBuilder);
           modelBuilder.Entity<Webhook>();
       }
   }
   ```

   The explicit alternative, `ApplyProtection`, is still shipped for call
   sites that configure protection inline in `OnModelCreating`. Call it last
   there, after the module and entity configurations that create the
   annotated properties — it walks the model as already built, so a property
   configured afterwards is not seen:

   ```csharp
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       base.OnModelCreating(modelBuilder);
       modelBuilder.Entity<Webhook>();
       modelBuilder.ApplyProtection(dataProtectionProvider); // last
   }
   ```

   Both routes cover a `[Protected]` property nested inside an EF complex
   type (`ComplexProperty`), not only one declared directly on the entity.

**`[Protected]` is inert without this package.** `Persistord.Managed`'s
`ManagedWebhook.Token`, for example, is annotated `[Protected]`, but without a
reference to `Persistord.Protection` and either registering
`ProtectedStringConvention` or calling `ApplyProtection`, it is stored as
plaintext. The attribute alone changes nothing.

**Every write is non-deterministic ciphertext.** `IDataProtector.Protect`
randomises its output, so the same plaintext produces a different stored value
every time it is written. A `[Protected]` column cannot be queried by
equality, cannot back a useful unique index, and its ciphertext is
substantially longer than the plaintext (over 130 characters for a short
secret) — account for that in any `HasMaxLength` you set on the property.

## The purpose string is fixed

Every protector this package creates is derived from `ProtectionPurposes.V1`
(`"Persistord.Protection.v1"`). This is deliberate: a protector derived from a
different purpose cannot decrypt what this one wrote, so the string can never
change without orphaning every ciphertext already in the database.

## One provider per application

EF Core caches the compiled model per context type. The protector baked into
that cached model is the one derived from whichever `IDataProtectionProvider`
built it first, so **the first `IDataProtectionProvider` supplied to a given
context type is the one every subsequent instance of that context type
uses**, for the lifetime of the process — passing a different provider on a
later call has no effect. The supported arrangement is one
`IDataProtectionProvider` per application, registered as a singleton in DI and
constructor-injected into the context.

Tests that need different key rings per instance are the exception: they must
also replace EF's `IModelCacheKeyFactory` so each configuration gets its own
cache entry. `Persistord.Testing`'s fixtures (`SqliteTestDatabase`) already do
this for you.

## Key ring

Data Protection encrypts with keys held in a *key ring*. This package does
not configure where that key ring lives — that is
[`IDataProtectionBuilder`](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
configuration, done once when you register Data Protection in your host.
Persistord ships only `Microsoft.AspNetCore.DataProtection.Abstractions`; the
implementation package (`Microsoft.AspNetCore.DataProtection`) is your
dependency to add and keep patched.

- **Losing the key ring means losing every protected value.** There is no
  recovery: without the keys that encrypted a value, `Unprotect` cannot
  produce it back.
- The default key ring location is a per-user profile folder, which does not
  travel with your database and is easy to lose on redeploy, container
  recreation, or a new host. Persist it explicitly, next to the database, with
  [`PersistKeysToFileSystem`](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview#persistkeystofilesystem):

  ```csharp
  services.AddDataProtection()
      .PersistKeysToFileSystem(new DirectoryInfo("/var/my-bot/keys"));
  ```

- **Rotation alone does not break decryption.** Data Protection keeps every
  key it has ever used in the ring, and each one keeps decrypting values it
  encrypted, even after a newer key becomes the default for new writes. A
  read only fails when the specific key that encrypted a value is actually
  gone from the ring — deleted or explicitly revoked — which surfaces as
  `System.Security.Cryptography.CryptographicException` while EF materializes
  the entity. See [Protection](https://handys11.github.io/Persistord/articles/protection.html#what-a-failure-looks-like)
  on the documentation site for the deleted-versus-revoked distinction and
  what recovers.

## License

MIT
