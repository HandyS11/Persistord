using Microsoft.EntityFrameworkCore;
using Persistord.Core;

namespace Persistord.Sample.CoreGraph;

/// <summary>
/// Minimal context for the core-graph sample. It wires no optional module — the
/// base <see cref="DiscordDbContext"/> already exposes Guilds, Channels, Users,
/// Members and Roles, and applies the global snowflake conversion.
/// </summary>
public sealed class CoreGraphContext(DbContextOptions<CoreGraphContext> options)
    : DiscordDbContext(options);
