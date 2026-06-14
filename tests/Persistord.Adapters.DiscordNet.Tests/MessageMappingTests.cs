using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

public class MessageMappingTests
{
    private static IMessage MinimalMessage()
    {
        var author = Substitute.For<IUser>();
        author.Id.Returns(300UL);
        var channel = Substitute.For<IMessageChannel>();
        channel.Id.Returns(500UL);

        var message = Substitute.For<IMessage>();
        message.Id.Returns(700UL);
        message.Channel.Returns(channel);
        message.Author.Returns(author);
        message.Content.Returns("hello");
        message.EditedTimestamp.Returns((DateTimeOffset?)null);
        message.Embeds.Returns([]);
        message.Attachments.Returns([]);
        message.Reactions.Returns(new Dictionary<IEmote, ReactionMetadata>());
        return message;
    }

    [Fact]
    public void ToMessageEntity_maps_scalar_fields()
    {
        var entity = MinimalMessage().ToMessageEntity();

        Assert.Equal(700UL, entity.Id);
        Assert.Equal(500UL, entity.ChannelId);
        Assert.Equal(300UL, entity.AuthorId);
        Assert.Equal("hello", entity.Content);
        Assert.Null(entity.EditedAt);
    }

    [Fact]
    public void ToMessageEntity_does_not_set_persistence_fields()
    {
        var entity = MinimalMessage().ToMessageEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAt);
    }

    [Fact]
    public void ToMessageEntity_maps_attachments()
    {
        var attachment = Substitute.For<IAttachment>();
        attachment.Id.Returns(800UL);
        attachment.Filename.Returns("file.png");
        attachment.Url.Returns("https://cdn/file.png");

        var message = MinimalMessage();
        message.Attachments.Returns([attachment]);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Attachments);
        Assert.Equal(800UL, mapped.Id);
        Assert.Equal("file.png", mapped.FileName);
        Assert.Equal("https://cdn/file.png", mapped.Url);
    }

    [Fact]
    public void ToMessageEntity_maps_reactions()
    {
        var emote = Substitute.For<IEmote>();
        emote.Name.Returns("👍");
        // ReactionMetadata.ReactionCount has an internal setter; use reflection+boxing to set it.
        object boxedMetadata = new ReactionMetadata();
        typeof(ReactionMetadata).GetProperty(nameof(ReactionMetadata.ReactionCount))!
            .SetValue(boxedMetadata, 3);
        var reactions = new Dictionary<IEmote, ReactionMetadata>
        {
            [emote] = (ReactionMetadata)boxedMetadata,
        };

        var message = MinimalMessage();
        message.Reactions.Returns(reactions);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Reactions);
        Assert.Equal("👍", mapped.Emoji);
        Assert.Equal(3, mapped.Count);
    }

    [Fact]
    public void ToMessageEntity_maps_custom_emote_reaction_as_name_id()
    {
        var emote = Emote.Parse("<:partyblob:806139563617779712>");

        // ReactionMetadata.ReactionCount has an internal setter; use reflection+boxing to set it.
        object boxedMetadata = new ReactionMetadata();
        typeof(ReactionMetadata).GetProperty(nameof(ReactionMetadata.ReactionCount))!
            .SetValue(boxedMetadata, 1);
        var reactions = new Dictionary<IEmote, ReactionMetadata>
        {
            [emote] = (ReactionMetadata)boxedMetadata,
        };

        var message = MinimalMessage();
        message.Reactions.Returns(reactions);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Reactions);
        Assert.Equal("partyblob:806139563617779712", mapped.Emoji);
    }

    [Fact]
    public void ToMessageEntity_maps_embed_with_footer_author_fields()
    {
        var embed = new EmbedBuilder()
            .WithTitle("T")
            .WithDescription("D")
            .WithColor(new Color(0x00FF00))
            .WithFooter(f =>
            {
                f.Text = "foot";
                f.IconUrl = "https://i/foot.png";
            })
            .WithAuthor(a =>
            {
                a.Name = "auth";
                a.Url = "https://a";
            })
            .AddField("fname", "fvalue", inline: true)
            .Build();

        var message = MinimalMessage();
        message.Embeds.Returns([embed]);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Embeds);
        Assert.Equal("T", mapped.Title);
        Assert.Equal("D", mapped.Description);
        Assert.Equal(0x00FF00, mapped.Color);
        Assert.Equal("foot", mapped.Footer!.Text);
        Assert.Equal("https://i/foot.png", mapped.Footer.IconUrl);
        Assert.Equal("auth", mapped.Author!.Name);
        Assert.Equal("https://a", mapped.Author.Url);
        var field = Assert.Single(mapped.Fields);
        Assert.Equal("fname", field.Name);
        Assert.Equal("fvalue", field.Value);
        Assert.True(field.Inline);
    }
}
