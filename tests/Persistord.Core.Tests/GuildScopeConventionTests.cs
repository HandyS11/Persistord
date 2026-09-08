using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;
using Xunit;

namespace Persistord.Core.Tests;

public class GuildScopeConventionTests
{
    private static IModel BuildModel()
    {
        var (database, context) = SqliteFixture.Create<ScopeProbeContext>(
            o => new ScopeProbeContext(o), createSchema: false);
        using (database)
        using (context)
        {
            return context.Model;
        }
    }

    private static IReadOnlyList<string[]> IndexesOf(Type entity) =>
    [
        .. BuildModel().FindEntityType(entity)!
            .GetIndexes()
            .Select(i => i.Properties.Select(p => p.Name).ToArray())
    ];

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

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class ScopedRow : IGuildScoped
    {
        public long Id { get; set; }

        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class GuildFirstKeyRow : IGuildScoped
    {
        public string Key { get; set; } = string.Empty;
        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class GuildLastKeyRow : IGuildScoped
    {
        public string Key { get; set; } = string.Empty;

        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class AlreadyIndexedRow : IGuildScoped
    {
        public long Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public ulong GuildId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class UnmarkedRow
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
