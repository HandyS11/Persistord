using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
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
            modelBuilder.ApplyGuildRoot();
        }
    }
}
