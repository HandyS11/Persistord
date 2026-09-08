using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.Core.Configurations;
using Xunit;

namespace Persistord.Core.Tests;

/// <summary>Pins the <c>ArgumentNullException.ThrowIfNull</c> guards on the core
/// configurations, the model-builder extension, and the context overrides.</summary>
public class CoreNullGuardTests
{
    [Fact]
    public void ApplyCoreGraph_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyCoreGraph());

    [Fact]
    public void ApplyGuildRoot_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyGuildRoot());

    [Fact]
    public void ApplyCoreConfiguration_forwarder_throws_on_null() =>
#pragma warning disable CS0618 // Guarding the obsolete forwarder is the point of this test.
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyCoreConfiguration());
#pragma warning restore CS0618

    [Fact]
    public void GuildConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new GuildEntityConfiguration().Configure(null!));

    [Fact]
    public void UserConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new UserEntityConfiguration().Configure(null!));

    [Fact]
    public void RoleConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new RoleEntityConfiguration().Configure(null!));

    [Fact]
    public void ChannelConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new ChannelEntityConfiguration().Configure(null!));

    [Fact]
    public void MemberConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new MemberEntityConfiguration().Configure(null!));

    [Fact]
    public void OnModelCreating_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new ProbeContext().ProbeModel(null));

    [Fact]
    public void ConfigureConventions_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new ProbeContext().ProbeConventions(null));

    [Fact]
    public void OnConfiguring_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new ProbeContext().ProbeConfiguring(null));

    /// <summary>Concrete context that exposes the protected overrides for null-guard testing.</summary>
    private sealed class ProbeContext()
        : DiscordGraphDbContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite("DataSource=:memory:").Options)
    {
        public void ProbeModel(ModelBuilder? modelBuilder) => OnModelCreating(modelBuilder!);

        public void ProbeConventions(ModelConfigurationBuilder? configurationBuilder) =>
            ConfigureConventions(configurationBuilder!);

        public void ProbeConfiguring(DbContextOptionsBuilder? optionsBuilder) => OnConfiguring(optionsBuilder!);
    }
}
