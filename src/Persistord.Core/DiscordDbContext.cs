using Microsoft.EntityFrameworkCore;
using Persistord.Core.Conventions;
using Persistord.Core.Conversions;
using Persistord.Core.Interception;

namespace Persistord.Core;

/// <summary>
/// Base EF Core context that applies Persistord's global conventions: the bit-faithful
/// <see cref="ulong"/>-to-<see cref="long"/> conversion for every unsigned 64-bit property, the
/// snowflake primary-key convention, and, when constructed with a <see cref="TimeProvider"/>, a
/// <see cref="TimestampInterceptor"/> that stamps timestamped entities. It maps no entity types,
/// so a bot that owns Discord resources rather than mirroring them pays for no tables. Derive
/// <see cref="DiscordGraphDbContext"/> instead to get the guild/channel/user/member/role skeleton.
/// </summary>
public abstract class DiscordDbContext : DbContext
{
    private readonly TimeProvider? _timeProvider;

    /// <summary>Initializes the context with the given options.</summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    protected DiscordDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes the context with the given options and registers a
    /// <see cref="TimestampInterceptor"/> driven by <paramref name="timeProvider"/>, so
    /// <see cref="Abstractions.ICreatedAt"/> and <see cref="Abstractions.IUpdatedAt"/> entities
    /// stamp themselves.
    /// </summary>
    /// <param name="options">The context options supplied by the consumer.</param>
    /// <param name="timeProvider">The clock to stamp from.</param>
    protected DiscordDbContext(DbContextOptions options, TimeProvider timeProvider)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        base.OnConfiguring(optionsBuilder);

        if (_timeProvider is not null)
        {
            optionsBuilder.AddInterceptors(new TimestampInterceptor(_timeProvider));
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<ulong>().HaveConversion<UlongToLongConverter>();
        configurationBuilder.Properties<ulong?>().HaveConversion<NullableUlongToLongConverter>();
        configurationBuilder.Conventions.Add(_ => new SnowflakeKeyConvention());
        configurationBuilder.Conventions.Add(_ => new GuildScopeConvention());
    }
}
