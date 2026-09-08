namespace Persistord.Core.Abstractions;

/// <summary>
/// A row that belongs to exactly one guild. Marking an entity with this interface opts it into
/// the guild-scope index convention, the cascading foreign key <c>ApplyGuildRoot</c> wires, and
/// <c>PurgeGuildAsync</c>. Implement <see cref="GuildId"/> as a public, settable property: the
/// helpers read it by name off the mapped CLR type.
/// </summary>
public interface IGuildScoped
{
    /// <summary>The owning guild snowflake id.</summary>
    ulong GuildId { get; }
}
