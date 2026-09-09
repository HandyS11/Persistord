using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core;
using Persistord.Core.Abstractions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Testing.Tests;

public class ModelAssertionsTests
{
    private static AssertionContext Context(SqliteTestDatabase database) =>
        database.CreateContext<AssertionContext>(o => new AssertionContext(o));

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
    public void AssertCascade_fails_when_the_relationship_does_not_cascade()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertCascade<RestrictedRow, GuildEntity>());

        Assert.Contains(nameof(DeleteBehavior.Restrict), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertCascade_fails_when_two_foreign_keys_target_the_same_principal()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertCascade<AmbiguousChildRow, ParentRow>());

        Assert.Contains(nameof(AmbiguousChildRow.AParentId), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AmbiguousChildRow.ZParentId), exception.Message, StringComparison.Ordinal);
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
    public void AssertSnowflakeKey_fails_when_the_key_is_store_generated()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            context.AssertSnowflakeKey<StoreGeneratedRow>());

        Assert.Contains(nameof(ValueGenerated.OnAdd), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertSnowflakeKey_fails_when_the_key_has_no_provider_conversion()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<PlainContext>(o => new PlainContext(o));

        var exception = Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<PlainRow>());

        Assert.Contains("provider type: none", exception.Message, StringComparison.Ordinal);
        Assert.Contains("DiscordDbContext", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assertions_fail_loudly_for_an_unmapped_type()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = Context(database);

        Assert.Throws<InvalidOperationException>(() => context.AssertSnowflakeKey<NotMapped>());
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
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class AmbiguousChildRow
    {
        public long Id { get; set; }

        public long AParentId { get; set; }

        public long ZParentId { get; set; }
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
                b.HasOne<ParentRow>().WithMany().HasForeignKey(r => r.ZParentId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<StoreGeneratedRow>().Property(r => r.Id).ValueGeneratedOnAdd();
            modelBuilder.ApplyGuildRoot();
        }
    }
}
