using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Persistord.Core;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Testing.Tests;

public class ModelAssertionsTests
{
    private static AssertionContext Context(SqliteTestDatabase database) =>
        database.CreateContext<AssertionContext>(o => new AssertionContext(o));

    // The assertions carry no test-framework dependency, so a failure's message is the whole report a
    // consumer gets: the failure tests below pin each message verbatim.

    [Fact]
    public void AssertUniqueIndex_passes_on_a_matching_index()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        context.AssertUniqueIndex<ScopedRow>(nameof(ScopedRow.GuildId), nameof(ScopedRow.Key));
    }

    [Fact]
    public void AssertUniqueIndex_names_the_entity_the_columns_and_every_index_found_when_it_fails()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertUniqueIndex<ScopedRow>(nameof(ScopedRow.Key), nameof(ScopedRow.GuildId)));

        Assert.Equal(
            "ScopedRow has no unique index over (Key, GuildId). Indexes found: (Label), unique (GuildId, Key).",
            exception.Message);
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
    public void AssertUniqueIndex_reports_none_for_an_entity_without_indexes()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertUniqueIndex<UnrelatedRow>(nameof(UnrelatedRow.Id)));

        Assert.Equal("UnrelatedRow has no unique index over (Id). Indexes found: none.", exception.Message);
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

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertCascade<UnrelatedRow, GuildEntity>());

        Assert.Equal("UnrelatedRow has no foreign key to GuildEntity.", exception.Message);
    }

    [Fact]
    public void AssertCascade_fails_when_the_relationship_does_not_cascade()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertCascade<RestrictedRow, GuildEntity>());

        Assert.Equal("RestrictedRow -> GuildEntity deletes with Restrict, not Cascade.", exception.Message);
    }

    [Fact]
    public void AssertCascade_fails_when_two_foreign_keys_target_the_same_principal()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertCascade<AmbiguousChildRow, ParentRow>());

        Assert.Equal(
            "AmbiguousChildRow has 2 foreign keys to ParentRow, so it is ambiguous which one to check: "
            + "(AParentId) -> Cascade, (ZParentId, ZParentCode) -> Restrict. A type with two relationships to the same "
            + "principal needs a more specific assertion than AssertCascade.",
            exception.Message);
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

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertSnowflakeKey<CompositeSurrogateRow>());

        Assert.Equal(
            "CompositeSurrogateRow's primary key has no ulong property: (TenantId, Number).",
            exception.Message);
    }

    [Fact]
    public void AssertSnowflakeKey_fails_on_a_keyless_entity()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<KeylessRow>());

        Assert.Equal("KeylessRow has no primary key.", exception.Message);
    }

    [Fact]
    public void AssertSnowflakeKey_fails_when_the_key_is_store_generated()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertSnowflakeKey<StoreGeneratedRow>());

        Assert.Equal(
            "StoreGeneratedRow.Id is OnAdd, not Never: EF would treat it as an identity column.",
            exception.Message);
    }

    [Fact]
    public void AssertSnowflakeKey_fails_when_the_key_has_no_provider_conversion()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<PlainContext>(o => new PlainContext(o));

        var exception = Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<PlainRow>());

        Assert.Equal(
            "PlainRow.Id is not stored as a long (provider type: none). Is the context a DiscordDbContext?",
            exception.Message);
    }

    [Fact]
    public void AssertSnowflakeKey_names_the_provider_type_a_key_is_stored_as()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertSnowflakeKey<StringStoredRow>());

        Assert.Equal(
            "StringStoredRow.Id is not stored as a long (provider type: String). Is the context a DiscordDbContext?",
            exception.Message);
    }

    [Fact]
    public void Assertions_fail_loudly_for_an_unmapped_type()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<NotMapped>());

        Assert.Equal("NotMapped is not part of the model.", exception.Message);
    }

    [Fact]
    public void Assertions_guard_their_arguments()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        Assert.Throws<ArgumentNullException>(
            "context",
            () => ((DbContext)null!).AssertUniqueIndex<ScopedRow>(nameof(ScopedRow.Key)));
        Assert.Throws<ArgumentNullException>("propertyNames", () => context.AssertUniqueIndex<ScopedRow>(null!));
        Assert.Throws<ArgumentNullException>("context",
            () => ((DbContext)null!).AssertCascade<ScopedRow, GuildEntity>());
        Assert.Throws<ArgumentNullException>("context", () => ((DbContext)null!).AssertSnowflakeKey<GuildEntity>());
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class ScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class UnrelatedRow
    {
        public long Id { get; set; }
    }

    [SuppressMessage(
        "Performance",
        "CA1812",
        Justification = "Deliberately excluded from the model, to exercise the unmapped-type failure path.")]
    internal sealed class NotMapped
    {
        public long Id { get; set; }
    }

    /// <summary>
    /// Not <see cref="IGuildScoped"/>: <c>ApplyGuildRoot</c> only forces
    /// <see cref="DeleteBehavior.Cascade"/> on <see cref="IGuildScoped"/> entities' own
    /// <c>GuildId</c> foreign key, so this type's explicit <see cref="DeleteBehavior.Restrict"/>
    /// configuration below survives it.
    /// </summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class RestrictedRow
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class ParentRow
    {
        public long Id { get; set; }

        public string Code { get; set; } = string.Empty;
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class AmbiguousChildRow
    {
        public long Id { get; set; }

        public long AParentId { get; set; }

        public long ZParentId { get; set; }

        public string ZParentCode { get; set; } = string.Empty;
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class StoreGeneratedRow
    {
        public ulong Id { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class PlainRow
    {
        public ulong Id { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class CompositeSurrogateRow
    {
        public long TenantId { get; set; }

        public long Number { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class StringStoredRow
    {
        public ulong Id { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class KeylessRow
    {
        public ulong Value { get; set; }
    }

    private sealed class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PlainRow>().Property(r => r.Id).ValueGeneratedNever();
        }
    }

    private sealed class AssertionContext(DbContextOptions<AssertionContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScopedRow>().HasIndex(r => new
            {
                r.GuildId, r.Key
            }).IsUnique();
            modelBuilder.Entity<ScopedRow>().HasIndex(r => r.Label);
            modelBuilder.Entity<UnrelatedRow>();
            modelBuilder.Entity<RestrictedRow>()
                .HasOne<GuildEntity>()
                .WithMany()
                .HasForeignKey(r => r.GuildId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AmbiguousChildRow>(b =>
            {
                b.HasOne<ParentRow>().WithMany().HasForeignKey(r => r.AParentId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne<ParentRow>()
                    .WithMany()
                    .HasForeignKey(r => new
                    {
                        r.ZParentId, r.ZParentCode
                    })
                    .HasPrincipalKey(p => new
                    {
                        p.Id, p.Code
                    })
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<StoreGeneratedRow>().Property(r => r.Id).ValueGeneratedOnAdd();
            modelBuilder.Entity<CompositeSurrogateRow>().HasKey(r => new
            {
                r.TenantId, r.Number
            });
            modelBuilder.Entity<StringStoredRow>().Property(r => r.Id)
                .HasConversion(new NumberToStringConverter<ulong>());
            modelBuilder.Entity<KeylessRow>().HasNoKey();
            modelBuilder.ApplyGuildRoot();
        }
    }
}
