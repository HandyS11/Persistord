using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.NetCord.Tests;

public class MessageMappingTests
{
    [Fact]
    public void Maps_scalar_fields()
    {
        var edited = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var entity = NetCordFakes.MakeMessage(
            id: 555UL, channelId: 111UL, authorId: 789UL, content: "hello", editedAt: edited).ToMessageEntity();

        Assert.Equal(555UL, entity.Id);
        Assert.Equal(111UL, entity.ChannelId);
        Assert.Equal(789UL, entity.AuthorId);
        Assert.Equal("hello", entity.Content);
        Assert.Equal(edited, entity.EditedAt);
    }

    [Fact]
    public void Leaves_soft_delete_state_at_its_default()
    {
        var entity = NetCordFakes.MakeMessage().ToMessageEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAt);
    }

    [Fact]
    public void Maps_attachments()
    {
        var entity = NetCordFakes.MakeMessage(
            attachments: [NetCordFakes.MakeAttachment(id: 900UL, fileName: "shot.png", url: "https://cdn.example/shot.png")])
            .ToMessageEntity();

        var attachment = Assert.Single(entity.Attachments);
        Assert.Equal(900UL, attachment.Id);
        Assert.Equal("shot.png", attachment.FileName);
        Assert.Equal("https://cdn.example/shot.png", attachment.Url);
        Assert.Equal(0UL, attachment.MessageId); // EF fills the FK on save
    }

    [Fact]
    public void Maps_a_unicode_reaction()
    {
        var entity = NetCordFakes.MakeMessage(
            reactions: [NetCordFakes.MakeReaction(count: 3, emojiId: null, emojiName: "\U0001F44D")]).ToMessageEntity();

        var reaction = Assert.Single(entity.Reactions);
        Assert.Equal("\U0001F44D", reaction.Emoji);
        Assert.Equal(3, reaction.Count);
        Assert.Equal(0L, reaction.Id); // EF assigns the surrogate key
    }

    [Fact]
    public void Maps_a_custom_reaction_as_name_colon_id()
    {
        var entity = NetCordFakes.MakeMessage(
            reactions: [NetCordFakes.MakeReaction(count: 2, emojiId: 42UL, emojiName: "blobwave")]).ToMessageEntity();

        Assert.Equal("blobwave:42", Assert.Single(entity.Reactions).Emoji);
    }

    [Fact]
    public void Maps_an_embed_with_footer_author_and_fields()
    {
        var entity = NetCordFakes.MakeMessage(embeds: [NetCordFakes.MakeEmbed()]).ToMessageEntity();

        var embed = Assert.Single(entity.Embeds);
        Assert.Equal("a title", embed.Title);
        Assert.Equal("a description", embed.Description);
        Assert.Equal(0x112233, embed.Color);
        Assert.Equal("a footer", embed.Footer?.Text);
        Assert.Equal("an author", embed.Author?.Name);

        var field = Assert.Single(embed.Fields);
        Assert.Equal("fname", field.Name);
        Assert.Equal("fvalue", field.Value);
        Assert.True(field.Inline);
        Assert.Equal(0L, embed.Id); // EF assigns the surrogate key
    }

    [Fact]
    public void Tolerates_an_embed_without_colour_footer_or_author()
    {
        var entity = NetCordFakes.MakeMessage(
            embeds: [NetCordFakes.MakeEmbed(color: null, footerText: null, authorName: null)]).ToMessageEntity();

        var embed = Assert.Single(entity.Embeds);
        Assert.Null(embed.Color);
        Assert.Null(embed.Footer);
        Assert.Null(embed.Author);
    }

    [Fact]
    public void Maps_an_empty_message_to_empty_collections()
    {
        var entity = NetCordFakes.MakeMessage().ToMessageEntity();

        Assert.Empty(entity.Embeds);
        Assert.Empty(entity.Attachments);
        Assert.Empty(entity.Reactions);
    }

    [Fact]
    public void History_snapshot_carries_message_id_content_and_change_type()
    {
        var entity = NetCordFakes.MakeMessage(id: 555UL, content: "hello")
            .ToHistoryEntity(HistoryChangeType.Edited);

        Assert.Equal(555UL, entity.MessageId);
        Assert.Equal("hello", entity.Content);
        Assert.Equal(HistoryChangeType.Edited, entity.ChangeType);
    }

    [Fact]
    public void History_snapshot_stamps_recorded_at_with_now()
    {
        var before = DateTimeOffset.UtcNow;
        var entity = NetCordFakes.MakeMessage().ToHistoryEntity(HistoryChangeType.Created);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(entity.RecordedAt, before, after);
    }
}
