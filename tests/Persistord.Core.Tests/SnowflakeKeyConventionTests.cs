using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Persistord.Core.Tests;

public class SnowflakeKeyConventionTests
{
    private static IModel BuildModel()
    {
        var (connection, context) = SqliteFixture.Create<ConventionProbeContext>(
            o => new ConventionProbeContext(o), createSchema: false);
        using (connection)
        using (context)
        {
            return context.Model;
        }
    }

    private static ValueGenerated ValueGeneratedFor(Type entity, string property) =>
        BuildModel().FindEntityType(entity)!.FindProperty(property)!.ValueGenerated;

    [Fact]
    public void Consumer_ulong_key_is_caller_supplied_without_any_fluent_call() =>
        Assert.Equal(ValueGenerated.Never, ValueGeneratedFor(typeof(SnowflakeKeyed), nameof(SnowflakeKeyed.Id)));

    [Fact]
    public void Long_key_keeps_store_generation() =>
        Assert.Equal(ValueGenerated.OnAdd, ValueGeneratedFor(typeof(SurrogateKeyed), nameof(SurrogateKeyed.Id)));

    [Fact]
    public void Ulong_part_of_a_composite_key_is_caller_supplied() =>
        Assert.Equal(
            ValueGenerated.Never,
            ValueGeneratedFor(typeof(CompositeKeyed), nameof(CompositeKeyed.GuildId)));

    [Fact]
    public void Non_key_ulong_property_is_left_alone() =>
        Assert.Equal(
            ValueGenerated.Never,
            ValueGeneratedFor(typeof(SurrogateKeyed), nameof(SurrogateKeyed.OwnerId)));

    [Fact]
    public void Explicit_configuration_wins_over_the_convention() =>
        Assert.Equal(
            ValueGenerated.OnAdd,
            ValueGeneratedFor(typeof(ExplicitlyGenerated), nameof(ExplicitlyGenerated.Id)));

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class SnowflakeKeyed
    {
        public ulong Id { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class SurrogateKeyed
    {
        public long Id { get; set; }

        public ulong OwnerId { get; set; }
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class CompositeKeyed
    {
        public ulong GuildId { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by EF Core via ModelBuilder.Entity<T>().")]
    internal sealed class ExplicitlyGenerated
    {
        public ulong Id { get; set; }
    }

    private sealed class ConventionProbeContext(DbContextOptions<ConventionProbeContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SnowflakeKeyed>();
            modelBuilder.Entity<SurrogateKeyed>();
            modelBuilder.Entity<CompositeKeyed>().HasKey(e => new
            {
                e.GuildId, e.Key
            });
            modelBuilder.Entity<ExplicitlyGenerated>().Property(e => e.Id).ValueGeneratedOnAdd();
        }
    }
}
