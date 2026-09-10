using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace Persistord.Adapters.DSharpPlus.Tests;

/// <summary>
/// Builders for real DSharpPlus model instances.
/// <para>
/// Every DSharpPlus constructor and property setter is <c>internal</c>, and the
/// properties are non-virtual, so the entities can be neither constructed nor mocked
/// directly. They can, however, be deserialized: DSharpPlus annotates its internal
/// setters (and the internal backing fields behind its read-only collections) with
/// Newtonsoft <c>[JsonProperty]</c>, so Newtonsoft populates them when allowed to use
/// the internal parameterless constructor. The inputs below are therefore real Discord
/// gateway payloads.
/// </para>
/// </summary>
internal static class DSharpPlusFakes
{
    private static readonly JsonSerializerSettings Settings =
        new() { ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };

    internal static T Make<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings)!;

    internal static DiscordChannel MakeChannel(
        int type = 0,
        ulong id = 111UL,
        ulong? guildId = 222UL,
        string name = "general",
        ulong? parentId = null)
    {
        var guildPart = guildId is { } gid ? $"\"guild_id\":\"{gid}\"," : string.Empty;
        var parentPart = parentId is { } pid ? $",\"parent_id\":\"{pid}\"" : string.Empty;

        return Make<DiscordChannel>(
            $"{{\"id\":\"{id}\",{guildPart}\"name\":\"{name}\",\"type\":{type}{parentPart}}}");
    }
}
