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
    public void ToMessageEntity_treats_explicitly_null_collections_as_empty()
    {
        var entity = WithUnsetCollections(MakeMessage()).ToMessageEntity();

        Assert.Empty(entity.Attachments);
        Assert.Empty(entity.Reactions);
        Assert.Empty(entity.Embeds);
    }

    [Fact]
    public void ToMessageEntity_leaves_missing_embed_footer_and_author_urls_null()
    {
        const string embed = """[{"footer":{"text":"a footer"},"author":{"name":"an author"}}]""";

        var mapped = Assert.Single(MakeMessage(embeds: embed).ToMessageEntity().Embeds);

        Assert.Equal("a footer", mapped.Footer!.Text);
        Assert.Null(mapped.Footer.IconUrl);
        Assert.Equal("an author", mapped.Author!.Name);
        Assert.Null(mapped.Author.Url);
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
