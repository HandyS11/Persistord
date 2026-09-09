using Microsoft.EntityFrameworkCore;

namespace Persistord.Core.Tests;

/// <summary>A context that takes the conventions and maps nothing at all.</summary>
public sealed class ConventionsOnlyContext(DbContextOptions<ConventionsOnlyContext> options)
    : Persistord.Core.DiscordDbContext(options);
