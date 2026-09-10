using System.Globalization;
using Persistord.History.Entities;
using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class MessageMappingTests
{
    [Fact]
    public void ToMessageEntity_maps_the_scalar_fields()
    {
        var entity = MakeMessage(
            id: 555UL,
            channelId: 111UL,
            authorId: 789UL,
            content: "hello",
            editedAt: "2026-01-02T03:04:05+00:00").ToMessageEntity();

        Assert.Equal(555UL, entity.Id);
        Assert.Equal(111UL, entity.ChannelId);
        Assert.Equal(789UL, entity.AuthorId);
        Assert.Equal("hello", entity.Content);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-02T03:04:05+00:00", CultureInfo.InvariantCulture),
            entity.EditedAt);
    }

    [Fact]
    public void ToMessageEntity_leaves_soft_delete_state_alone()
    {
        var entity = MakeMessage().ToMessageEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAt);
    }

    [Fact]
    public void ToMessageEntity_maps_attachments()
    {
        var entity = MakeMessage(attachments: OneAttachment).ToMessageEntity();

        var attachment = Assert.Single(entity.Attachments);
        Assert.Equal(900UL, attachment.Id);
        Assert.Equal("shot.png", attachment.FileName);
        Assert.Equal("https://cdn.example/shot.png", attachment.Url);
        Assert.Equal(0UL, attachment.MessageId); // EF fills this from the navigation on save
    }

    [Fact]
    public void ToMessageEntity_formats_unicode_and_custom_reaction_emoji_differently()
    {
        var entity = MakeMessage(reactions: UnicodeAndCustomReactions).ToMessageEntity();

        Assert.Collection(
            entity.Reactions,
            r =>
            {
                Assert.Equal("thumbsup", r.Emoji);
                Assert.Equal(3, r.Count);
            },
            r =>
            {
                Assert.Equal("blob:12345", r.Emoji);
                Assert.Equal(1, r.Count);
            });
    }

    [Fact]
    public void ToMessageEntity_leaves_reaction_surrogate_keys_for_EF()
    {
        var entity = MakeMessage(reactions: UnicodeAndCustomReactions).ToMessageEntity();

        Assert.All(entity.Reactions, r => Assert.Equal(0L, r.Id));
    }

    [Fact]
    public void ToMessageEntity_maps_a_full_embed()
    {
        var entity = MakeMessage(embeds: FullEmbed).ToMessageEntity();

        var embed = Assert.Single(entity.Embeds);
        Assert.Equal("a title", embed.Title);
        Assert.Equal("a description", embed.Description);
        Assert.Equal(1122867, embed.Color);
        Assert.Equal(0L, embed.Id); // EF-generated surrogate key
        Assert.Equal("a footer", embed.Footer!.Text);
        Assert.Equal("https://cdn.example/i.png", embed.Footer.IconUrl);
        Assert.Equal("an author", embed.Author!.Name);
        Assert.Equal("https://example/a", embed.Author.Url);

        var field = Assert.Single(embed.Fields);
        Assert.Equal("fname", field.Name);
        Assert.Equal("fvalue", field.Value);
        Assert.True(field.Inline);
        Assert.Equal(0L, field.Id);
    }

    [Fact]
    public void ToMessageEntity_tolerates_an_embed_with_no_color_footer_author_or_fields()
    {
        // DiscordEmbed.Color is Optional<DiscordColor>, and Fields is genuinely null
        // — not an empty list — when the payload omits it.
        var entity = MakeMessage(embeds: BareEmbed).ToMessageEntity();

        var embed = Assert.Single(entity.Embeds);
        Assert.Equal("a title", embed.Title);
        Assert.Null(embed.Color);
        Assert.Null(embed.Footer);
        Assert.Null(embed.Author);
        Assert.Empty(embed.Fields);
    }

    [Fact]
    public void ToMessageEntity_tolerates_a_bare_message()
    {
        // Gateway payloads are routinely partial — a message-delete event carries little
        // more than ids. Mapping must not throw, and absent optional data stays null.
        var entity = MakeMessage(content: null).ToMessageEntity();

        Assert.Null(entity.Content);
        Assert.Null(entity.EditedAt);
        Assert.Empty(entity.Attachments);
        Assert.Empty(entity.Reactions);
        Assert.Empty(entity.Embeds);
    }

    [Fact]
    public void ToHistoryEntity_snapshots_content_and_records_the_change_type()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = MakeMessage(id: 555UL, content: "hello").ToHistoryEntity(HistoryChangeType.Edited);

        Assert.Equal(555UL, entity.MessageId);
        Assert.Equal("hello", entity.Content);
        Assert.Equal(HistoryChangeType.Edited, entity.ChangeType);
        Assert.InRange(entity.RecordedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(0L, entity.Id); // EF-generated surrogate key
    }

    [Theory]
    [InlineData(HistoryChangeType.Created)]
    [InlineData(HistoryChangeType.Edited)]
    [InlineData(HistoryChangeType.Deleted)]
    public void ToHistoryEntity_passes_every_change_type_through(HistoryChangeType changeType)
    {
        Assert.Equal(changeType, MakeMessage().ToHistoryEntity(changeType).ChangeType);
    }

    [Fact]
    public void ToHistoryEntity_snapshots_a_null_content_as_null()
    {
        Assert.Null(MakeMessage(content: null).ToHistoryEntity(HistoryChangeType.Deleted).Content);
    }
}
