using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>Minimal concrete context over the core skeleton for tests.</summary>
public sealed class TestContext(DbContextOptions<TestContext> options)
    : Persistord.Core.DiscordDbContext(options);
