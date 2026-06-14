using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Xunit;
using ChannelType = Persistord.Core.Entities.ChannelType;

namespace Persistord.Adapters.DiscordNet.Tests;

public class ChannelTypeMappingTests
{
    [Fact]
    public void TextChannel_maps_to_Text()
    {
        var channel = Substitute.For<ITextChannel>();
        Assert.Equal(ChannelType.Text, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void VoiceChannel_maps_to_Voice()
    {
        var channel = Substitute.For<IVoiceChannel>();
        Assert.Equal(ChannelType.Voice, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void CategoryChannel_maps_to_Category()
    {
        var channel = Substitute.For<ICategoryChannel>();
        Assert.Equal(ChannelType.Category, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void ThreadChannel_maps_to_Thread()
    {
        var channel = Substitute.For<IThreadChannel>();
        Assert.Equal(ChannelType.Thread, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void UnknownGuildChannel_falls_back_to_Text()
    {
        var channel = Substitute.For<IGuildChannel>();
        Assert.Equal(ChannelType.Text, channel.ToChannelEntity().Type);
    }
}
