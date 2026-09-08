namespace Persistord.Managed;

/// <summary>The scope partition of a managed resource.</summary>
public static class ManagedScope
{
    /// <summary>
    /// The guild-wide scope: the empty string. Not <c>null</c> — SQL unique indexes treat NULLs as
    /// distinct on SQLite and PostgreSQL alike, so a nullable scope column would happily accept two
    /// rows for the same <c>(guild, key)</c>.
    /// </summary>
    public const string Global = "";

    /// <summary>Turns a caller's optional scope into a storable one.</summary>
    /// <param name="scope">The caller's scope, or <c>null</c> for guild-wide.</param>
    /// <returns><paramref name="scope"/>, or <see cref="Global"/> when it is <c>null</c>.</returns>
    public static string Normalize(string? scope) => scope ?? Global;
}
