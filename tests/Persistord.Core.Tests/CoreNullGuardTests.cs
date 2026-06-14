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
    public void ApplyCoreConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyCoreConfiguration());

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

    /// <summary>Concrete context that exposes the protected overrides for null-guard testing.</summary>
    private sealed class ProbeContext()
        : DiscordDbContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite("DataSource=:memory:").Options)
    {
        public void ProbeModel(ModelBuilder? modelBuilder) => OnModelCreating(modelBuilder!);

        public void ProbeConventions(ModelConfigurationBuilder? configurationBuilder) =>
            ConfigureConventions(configurationBuilder!);
    }
}
