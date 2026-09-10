using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;

namespace Persistord.Adapters.DSharpPlus.Tests;

/// <summary>
/// Verifies that <c>ToMessageEntity</c> coalesces null Discord string fields to
/// <see cref="string.Empty"/> and that a reaction with no emoji at all maps to an
/// empty emoji string. These pin the <c>?? string.Empty</c> fallbacks in
/// <c>ToMessageEntity</c> and <c>MapEmbed</c>, and the <c>null</c> arm of
/// <c>FormatEmoji</c>, against mutation.
/// </summary>
public class NullFallbackTests
{
    [Fact]
    public void ToMessageEntity_coalesces_null_attachment_strings_to_empty()
    {
        const string attachment = """[{"id":"900","filename":null,"url":null}]""";

        var mapped = Assert.Single(MakeMessage(attachments: attachment).ToMessageEntity().Attachments);

        Assert.Equal(string.Empty, mapped.FileName);
        Assert.Equal(string.Empty, mapped.Url);
    }

    [Fact]
    public void ToMessageEntity_formats_a_reaction_with_no_emoji_as_empty_string()
    {
        const string reaction = """[{"count":1,"emoji":null}]""";

        var mapped = Assert.Single(MakeMessage(reactions: reaction).ToMessageEntity().Reactions);

        Assert.Equal(string.Empty, mapped.Emoji);
    }

    [Fact]
    public void ToMessageEntity_coalesces_null_unicode_emote_name_to_empty()
    {
        const string reaction = """[{"count":1,"emoji":{"id":null,"name":null}}]""";

        var mapped = Assert.Single(MakeMessage(reactions: reaction).ToMessageEntity().Reactions);

        Assert.Equal(string.Empty, mapped.Emoji);
    }

    [Fact]
    public void ToMessageEntity_coalesces_null_embed_field_strings_to_empty()
    {
        const string embed = """[{"fields":[{"name":null,"value":null,"inline":false}]}]""";

        var mapped = Assert.Single(MakeMessage(embeds: embed).ToMessageEntity().Embeds);

        var field = Assert.Single(mapped.Fields);
        Assert.Equal(string.Empty, field.Name);
        Assert.Equal(string.Empty, field.Value);
    }
}
