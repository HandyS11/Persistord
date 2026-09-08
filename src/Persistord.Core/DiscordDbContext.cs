using Microsoft.EntityFrameworkCore;
using Persistord.Core.Conventions;
using Persistord.Core.Conversions;

namespace Persistord.Core;

/// <summary>
/// Base EF Core context that applies Persistord's global conventions and nothing else: the
/// bit-faithful <see cref="ulong"/>-to-<see cref="long"/> conversion for every unsigned
/// 64-bit property. It maps no entity types, so a bot that owns Discord resources rather
/// than mirroring them pays for no tables. Derive <see cref="DiscordGraphDbContext"/>
/// instead to get the guild/channel/user/member/role skeleton.
/// </summary>
/// <remarks>Initializes the context with the given options.</remarks>
/// <param name="options">The context options supplied by the consumer.</param>
public abstract class DiscordDbContext(DbContextOptions options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<ulong>().HaveConversion<UlongToLongConverter>();
        configurationBuilder.Properties<ulong?>().HaveConversion<NullableUlongToLongConverter>();
        configurationBuilder.Conventions.Add(_ => new SnowflakeKeyConvention());
    }
}
