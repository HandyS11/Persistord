using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>Minimal concrete context over the core skeleton for tests.</summary>
public sealed class TestContext : Persistord.Core.DiscordDbContext
{
    public TestContext(DbContextOptions<TestContext> options)
        : base(options)
    {
    }
}
