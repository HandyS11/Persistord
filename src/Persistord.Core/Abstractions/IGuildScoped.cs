namespace Persistord.Core.Abstractions;

/// <summary>
/// A row that belongs to exactly one guild. Marking an entity with this interface opts it into
/// the guild-scope index convention, the cascading foreign key <c>ApplyGuildRoot</c> wires, and
/// <c>PurgeGuildAsync</c>. All three resolve <see cref="GuildId"/> by name off the mapped CLR type
/// rather than through this interface, so the only requirement is that EF can map it: a public
/// getter, plus whatever setter EF needs to materialize the row — a private setter or a backing
/// field is enough. Nothing in Persistord writes the property, which is why the interface stays
/// read-only: an entity that guards its own invariants can implement it without exposing a setter.
/// </summary>
public interface IGuildScoped
{
    /// <summary>The owning guild snowflake id.</summary>
    ulong GuildId { get; }
}
