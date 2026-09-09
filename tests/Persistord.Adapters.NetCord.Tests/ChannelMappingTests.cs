using Xunit;
using ChannelType = Persistord.Core.Entities.ChannelType;
using NetCordChannelType = global::NetCord.ChannelType;

namespace Persistord.Adapters.NetCord.Tests;

public class ChannelMappingTests
{
    [Theory]
    [InlineData(NetCordChannelType.TextGuildChannel, ChannelType.Text)]
    [InlineData(NetCordChannelType.AnnouncementGuildChannel, ChannelType.Text)]
    [InlineData(NetCordChannelType.VoiceGuildChannel, ChannelType.Voice)]
    [InlineData(NetCordChannelType.StageGuildChannel, ChannelType.Voice)]
    [InlineData(NetCordChannelType.CategoryChannel, ChannelType.Category)]
    [InlineData(NetCordChannelType.PublicGuildThread, ChannelType.Thread)]
    [InlineData(NetCordChannelType.PrivateGuildThread, ChannelType.Thread)]
    [InlineData(NetCordChannelType.AnnouncementGuildThread, ChannelType.Thread)]
    [InlineData(NetCordChannelType.ForumGuildChannel, ChannelType.Text)]
    [InlineData(NetCordChannelType.MediaForumGuildChannel, ChannelType.Text)]
    [InlineData(NetCordChannelType.DirectoryGuildChannel, ChannelType.Text)]
    public void Maps_channel_class_to_type(NetCordChannelType source, ChannelType expected) =>
        Assert.Equal(expected, NetCordFakes.MakeChannel(source).ToChannelEntity().Type);

    [Fact]
    public void Maps_id_guild_and_name()
    {
        var entity = NetCordFakes.MakeChannel(
            NetCordChannelType.TextGuildChannel, id: 915UL, guildId: 842UL, name: "general").ToChannelEntity();

        Assert.Equal(915UL, entity.Id);
        Assert.Equal(842UL, entity.GuildId);
        Assert.Equal("general", entity.Name);
    }

    [Fact]
    public void Maps_parent_id_on_a_text_channel() =>
        Assert.Equal(
            777UL,
            NetCordFakes.MakeChannel(NetCordChannelType.TextGuildChannel, parentId: 777UL).ToChannelEntity().ParentId);

    [Fact]
    public void Maps_parent_id_on_a_thread() =>
        Assert.Equal(
            777UL,
            NetCordFakes.MakeChannel(NetCordChannelType.PublicGuildThread, parentId: 777UL).ToChannelEntity().ParentId);

    [Fact]
    public void Maps_parent_id_on_a_forum() =>
        Assert.Equal(
            777UL,
            NetCordFakes.MakeChannel(NetCordChannelType.ForumGuildChannel, parentId: 777UL).ToChannelEntity().ParentId);

    [Fact]
    public void Leaves_parent_id_null_on_a_category() =>
        Assert.Null(
            NetCordFakes.MakeChannel(NetCordChannelType.CategoryChannel, parentId: 777UL).ToChannelEntity().ParentId);

    [Fact]
    public void Round_trips_a_snowflake_near_ulong_MaxValue()
    {
        var entity = NetCordFakes.MakeChannel(
            NetCordChannelType.TextGuildChannel, id: ulong.MaxValue - 1, guildId: ulong.MaxValue - 2).ToChannelEntity();

        Assert.Equal(ulong.MaxValue - 1, entity.Id);
        Assert.Equal(ulong.MaxValue - 2, entity.GuildId);
    }
}
