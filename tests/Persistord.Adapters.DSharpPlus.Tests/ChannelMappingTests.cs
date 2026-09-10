using DSharpPlus.Entities;
using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;
using ChannelType = Persistord.Core.Entities.ChannelType;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class ChannelMappingTests
{
    [Theory]
    [InlineData(0, ChannelType.Text)]          // Text
    [InlineData(1, ChannelType.Text)]          // Private (DM) — fallback
    [InlineData(2, ChannelType.Voice)]         // Voice
    [InlineData(3, ChannelType.Text)]          // Group (group DM) — fallback
    [InlineData(4, ChannelType.Category)]      // Category
    [InlineData(5, ChannelType.Text)]          // News (announcement)
    [InlineData(6, ChannelType.Text)]          // Store
    [InlineData(10, ChannelType.Thread)]       // NewsThread
    [InlineData(11, ChannelType.Thread)]       // PublicThread
    [InlineData(12, ChannelType.Thread)]       // PrivateThread
    [InlineData(13, ChannelType.Voice)]        // Stage
    [InlineData(14, ChannelType.Text)]         // Directory — fallback
    [InlineData(15, ChannelType.Text)]         // GuildForum — fallback
    [InlineData(2147483647, ChannelType.Text)] // Unknown — fallback
    public void ToChannelEntity_translates_every_channel_type(int raw, ChannelType expected)
    {
        var entity = MakeChannel(type: raw).ToChannelEntity();

        Assert.Equal(expected, entity.Type);
    }

    [Fact]
    public void ToChannelEntity_maps_all_fields()
    {
        var entity = MakeChannel(id: 111UL, guildId: 222UL, name: "general", parentId: 444UL).ToChannelEntity();

        Assert.Equal(111UL, entity.Id);
        Assert.Equal(222UL, entity.GuildId);
        Assert.Equal(444UL, entity.ParentId);
        Assert.Equal("general", entity.Name);
    }

    [Fact]
    public void ToChannelEntity_leaves_parent_null_when_the_channel_has_no_parent()
    {
        Assert.Null(MakeChannel(type: 4, parentId: null).ToChannelEntity().ParentId);
    }

    [Fact]
    public void ToChannelEntity_maps_a_guildless_channel_to_guild_zero()
    {
        // DM and group-DM channels carry no guild_id. ChannelEntity.GuildId is a
        // non-nullable ulong, so the mapper collapses the absent id to 0 rather than
        // throwing — per spec §3 rule 5, missing optional data must not throw.
        Assert.Equal(0UL, MakeChannel(type: 1, guildId: null).ToChannelEntity().GuildId);
    }

    [Fact]
    public void ToChannelEntity_maps_a_nameless_channel_to_an_empty_name()
    {
        var channel = Make<DiscordChannel>("""{"id":"111","guild_id":"222","type":0}""");

        Assert.Equal(string.Empty, channel.ToChannelEntity().Name);
    }
}
