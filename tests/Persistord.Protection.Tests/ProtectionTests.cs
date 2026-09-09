using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;
using Persistord.Testing;
using Xunit;

namespace Persistord.Protection.Tests;

public class ProtectionTests
{
    [Fact]
    public async Task A_protected_property_round_trips_through_ef()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider()));

        await context.Secrets.AddAsync(new SecretRow
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
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider()));

        await context.Secrets.AddAsync(new SecretRow
        {
            Token = "super-secret", Label = "public"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await ReadColumnAsync(context, "Secrets", "Token");

        Assert.NotEqual("super-secret", stored);
    }

    [Fact]
    public async Task A_property_declared_only_on_an_implemented_interface_is_protected()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider()));

        await context.InterfaceSecrets.AddAsync(new InterfaceSecretRow
        {
            Token = "super-secret"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await ReadColumnAsync(context, "InterfaceSecrets", "Token");
        Assert.NotEqual("super-secret", stored);

        var row = await context.InterfaceSecrets.SingleAsync();
        Assert.Equal("super-secret", row.Token);
    }

    [Fact]
    public async Task An_unrelated_same_named_property_that_does_not_implement_the_interface_is_left_alone()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider()));

        await context.UnrelatedTokens.AddAsync(new UnrelatedTokenRow
        {
            Token = "super-secret"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await ReadColumnAsync(context, "UnrelatedTokens", "Token");

        Assert.Equal("super-secret", stored);
    }

    [Fact]
    public async Task An_unannotated_property_is_stored_as_is()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider()));

        await context.Secrets.AddAsync(new SecretRow
        {
            Token = "super-secret", Label = "public"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await ReadColumnAsync(context, "Secrets", "Label");

        Assert.Equal("public", stored);
    }

    [Fact]
    public async Task A_protected_property_nested_inside_a_complex_type_is_not_stored_as_plaintext()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider()));

        await context.ComplexSecrets.AddAsync(new ComplexSecretRow
        {
            Credentials = new Credentials
            {
                Token = "complex-secret-value"
            }
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // entityType.GetProperties() does not enumerate complex-type members, so before the fix
        // this column was written as-is: ApplyProtection never saw the property to install a
        // converter on it. The column name is resolved from the model rather than hard-coded,
        // since EF's default complex-type flattening ("Credentials_Token") is a naming detail
        // this test does not need to assert on.
        var columnName = context.Model
                             .FindEntityType(typeof(ComplexSecretRow))!
                             .FindComplexProperty(nameof(ComplexSecretRow.Credentials))!
                             .ComplexType.FindProperty(nameof(Credentials.Token))!
                             .GetColumnName(StoreObjectIdentifier.Table("ComplexSecrets"))
                         ?? throw new InvalidOperationException("Credentials.Token has no column mapping.");

        var stored = await ReadColumnAsync(context, "ComplexSecrets", columnName);

        Assert.NotEqual("complex-secret-value", stored);

        var row = await context.ComplexSecrets.SingleAsync();
        Assert.Equal("complex-secret-value", row.Credentials.Token);
    }

    [Fact]
    public async Task The_convention_encrypts_a_protected_property()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<ConventionSecretContext>(o =>
                new ConventionSecretContext(o, new ReversingProvider()));

        await context.Secrets.AddAsync(new SecretRow
        {
            Token = "super-secret", Label = "public"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await ReadColumnAsync(context, "Secrets", "Token");
        Assert.NotEqual("super-secret", stored);

        var row = await context.Secrets.SingleAsync();
        Assert.Equal("super-secret", row.Token);
    }

    [Fact]
    public async Task The_convention_covers_an_entity_type_registered_after_the_point_ApplyProtection_ran()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<LateRegistrationContext>(o =>
                new LateRegistrationContext(o, new ReversingProvider()));

        // LateRegistrationContext.OnModelCreating calls ApplyProtection before registering
        // LateSecretRow, so the explicit route never sees this property. Only the convention,
        // registered from ConfigureConventions and run at model finalization, still catches it.
        await context.LateSecrets.AddAsync(new LateSecretRow
        {
            Token = "super-secret"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await ReadColumnAsync(context, "LateSecrets", "Token");
        Assert.NotEqual("super-secret", stored);

        var row = await context.LateSecrets.SingleAsync();
        Assert.Equal("super-secret", row.Token);
    }

    [Fact]
    public async Task A_lost_key_ring_surfaces_as_a_cryptographic_exception()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);

        await using (var writer =
                     database.CreateContext<SecretContext>(o => new SecretContext(o, new ReversingProvider())))
        {
            await writer.Secrets.AddAsync(new SecretRow
            {
                Token = "super-secret", Label = "public"
            });
            await writer.SaveChangesAsync();
        }

        await using var reader = database.CreateContext<SecretContext>(o => new SecretContext(o, new BrokenProvider()));
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

    /// <summary>
    /// Reads a single column's stored bytes with a raw ADO command: the assertion is about what
    /// landed on disk, not about the API used to read it back, and <c>Database.SqlQuery{string}</c>
    /// does not reliably map a bare column alias for a scalar result in this EF Core version.
    /// </summary>
    [SuppressMessage(
        "Security",
        "CA2100",
        Justification = "table and column are always literal names passed by tests below, never external input.")]
    private static async Task<string?> ReadColumnAsync(DbContext context, string table, string column)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"{column}\" FROM \"{table}\"";
        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>
    /// A stand-in protector: reverses the payload. The converter only ever calls
    /// Protect/Unprotect, so a fake exercises the wiring faithfully without a key ring.
    /// </summary>
    private sealed class ReversingProvider : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => [.. plaintext.Reverse()];

        public byte[] Unprotect(byte[] protectedData) => [.. protectedData.Reverse()];
    }

    private sealed class BrokenProvider : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) =>
            throw new CryptographicException("The key ring is gone.");
    }
}

public sealed class SecretRow
{
    public long Id { get; set; }

    [Protected] public string Token { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

/// <summary>An interface that centralises the <see cref="ProtectedAttribute"/> annotation.</summary>
public interface ISecretHolder
{
    /// <summary>The secret. Annotated here instead of on every implementer.</summary>
    [Protected]
    string Token { get; set; }
}

public sealed class InterfaceSecretRow : ISecretHolder
{
    public long Id { get; set; }

    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// A same-named <c>Token</c> property that does not implement <see cref="ISecretHolder"/>: it must
/// not be swept up by a name match with the interface member above.
/// </summary>
public sealed class UnrelatedTokenRow
{
    public long Id { get; set; }

    public string Token { get; set; } = string.Empty;
}

/// <summary>An EF complex type nested inside <see cref="ComplexSecretRow"/>.</summary>
public sealed class Credentials
{
    [Protected] public string Token { get; set; } = string.Empty;
}

/// <summary>
/// An entity whose <see cref="ProtectedAttribute"/> property sits behind <c>ComplexProperty</c>
/// rather than directly on the entity — <c>entityType.GetProperties()</c> does not enumerate it.
/// </summary>
public sealed class ComplexSecretRow
{
    public long Id { get; set; }

    public Credentials Credentials { get; set; } = new();
}

public sealed class SecretContext(
    DbContextOptions<SecretContext> options,
    IDataProtectionProvider dataProtectionProvider) : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<SecretRow> Secrets => Set<SecretRow>();

    public DbSet<InterfaceSecretRow> InterfaceSecrets => Set<InterfaceSecretRow>();

    public DbSet<UnrelatedTokenRow> UnrelatedTokens => Set<UnrelatedTokenRow>();

    public DbSet<ComplexSecretRow> ComplexSecrets => Set<ComplexSecretRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SecretRow>().ToTable("Secrets");
        modelBuilder.Entity<InterfaceSecretRow>().ToTable("InterfaceSecrets");
        modelBuilder.Entity<UnrelatedTokenRow>().ToTable("UnrelatedTokens");
        modelBuilder.Entity<ComplexSecretRow>(builder =>
        {
            builder.ToTable("ComplexSecrets");
            builder.ComplexProperty(r => r.Credentials);
        });
        modelBuilder.ApplyProtection(dataProtectionProvider);
    }
}

/// <summary>A context wired through <see cref="ProtectedStringConvention"/> instead of <c>ApplyProtection</c>.</summary>
public sealed class ConventionSecretContext(
    DbContextOptions<ConventionSecretContext> options,
    IDataProtectionProvider dataProtectionProvider) : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<SecretRow> Secrets => Set<SecretRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new ProtectedStringConvention(dataProtectionProvider));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SecretRow>().ToTable("Secrets");
    }
}

public sealed class LateSecretRow
{
    public long Id { get; set; }

    [Protected] public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Calls <c>ApplyProtection</c> before registering <see cref="LateSecretRow"/>, so the explicit
/// route never sees it — only <see cref="ProtectedStringConvention"/>, registered from
/// <see cref="ConfigureConventions"/> and run at model finalization, still can.
/// </summary>
public sealed class LateRegistrationContext(
    DbContextOptions<LateRegistrationContext> options,
    IDataProtectionProvider dataProtectionProvider) : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<LateSecretRow> LateSecrets => Set<LateSecretRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new ProtectedStringConvention(dataProtectionProvider));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyProtection(dataProtectionProvider);
        modelBuilder.Entity<LateSecretRow>().ToTable("LateSecrets");
    }
}
