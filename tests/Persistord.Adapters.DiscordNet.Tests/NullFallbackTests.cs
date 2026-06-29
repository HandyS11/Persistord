using Discord;
using NSubstitute;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

/// <summary>
/// Verifies that mappers coalesce null Discord string fields to <see cref="string.Empty"/>
/// and that optional embed blocks map to null. These pin the <c>?? string.Empty</c>
/// fallbacks and the <c>HasValue</c> conditionals against mutation.
/// </summary>
public class NullFallbackTests
{
    [Fact]
    public void ToChannelEntity_coalesces_null_name_to_empty()
    {
        var channel = Substitute.For<IGuildChannel>();
        channel.Name.Returns((string?)null);

        Assert.Equal(string.Empty, channel.ToChannelEntity().Name);
    }

    [Fact]
    public void ToGuildEntity_coalesces_null_name_to_empty()
    {
        var guild = Substitute.For<IGuild>();
        guild.Name.Returns((string?)null);

        Assert.Equal(string.Empty, guild.ToGuildEntity().Name);
    }

    [Fact]
    public void ToUserEntity_coalesces_null_username_to_empty()
    {
        var user = Substitute.For<IUser>();
        user.Username.Returns((string?)null);

        Assert.Equal(string.Empty, user.ToUserEntity().Username);
    }

    [Fact]
    public void ToRoleEntity_coalesces_null_name_to_empty()
    {
        var role = Substitute.For<IRole>();
        role.Name.Returns((string?)null);

        Assert.Equal(string.Empty, role.ToRoleEntity().Name);
    }

    [Fact]
    public void ToMessageEntity_coalesces_null_attachment_strings_to_empty()
    {
        var attachment = Substitute.For<IAttachment>();
        attachment.Filename.Returns((string?)null);
        attachment.Url.Returns((string?)null);

        var message = MessageStub();
        message.Attachments.Returns([attachment]);

        var mapped = Assert.Single(message.ToMessageEntity().Attachments);
        Assert.Equal(string.Empty, mapped.FileName);
        Assert.Equal(string.Empty, mapped.Url);
    }

    [Fact]
    public void ToMessageEntity_coalesces_null_unicode_emote_name_to_empty()
    {
        var emote = Substitute.For<IEmote>();
        emote.Name.Returns((string?)null);

        object boxed = new ReactionMetadata();
        typeof(ReactionMetadata).GetProperty(nameof(ReactionMetadata.ReactionCount))!.SetValue(boxed, 1);

        var message = MessageStub();
        message.Reactions.Returns(new Dictionary<IEmote, ReactionMetadata>
        {
            [emote] = (ReactionMetadata)boxed,
        });

        var mapped = Assert.Single(message.ToMessageEntity().Reactions);
        Assert.Equal(string.Empty, mapped.Emoji);
    }

    [Fact]
    public void ToMessageEntity_maps_embed_without_optional_blocks()
    {
        var embed = Substitute.For<IEmbed>();
        embed.Color.Returns((Color?)null);
        embed.Footer.Returns((EmbedFooter?)null);
        embed.Author.Returns((EmbedAuthor?)null);
        embed.Fields.Returns([default]);

        var message = MessageStub();
        message.Embeds.Returns([embed]);

        var mapped = Assert.Single(message.ToMessageEntity().Embeds);
        Assert.Null(mapped.Color);
        Assert.Null(mapped.Footer);
        Assert.Null(mapped.Author);
        var field = Assert.Single(mapped.Fields);
        Assert.Equal(string.Empty, field.Name);
        Assert.Equal(string.Empty, field.Value);
    }

    private static IMessage MessageStub()
    {
        var author = Substitute.For<IUser>();
        author.Id.Returns(1UL);
        var channel = Substitute.For<IMessageChannel>();
        channel.Id.Returns(2UL);

        var message = Substitute.For<IMessage>();
        message.Id.Returns(3UL);
        message.Channel.Returns(channel);
        message.Author.Returns(author);
        message.Embeds.Returns([]);
        message.Attachments.Returns([]);
        message.Reactions.Returns(new Dictionary<IEmote, ReactionMetadata>());
        return message;
    }
}
