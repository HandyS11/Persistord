using DSharpPlus.Entities;
using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class NullArgumentTests
{
    [Fact]
    public void ToChannelEntity_throws_on_a_null_channel() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordChannel)null!).ToChannelEntity());

    [Fact]
    public void ToGuildEntity_throws_on_a_null_guild() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordGuild)null!).ToGuildEntity());

    [Fact]
    public void ToUserEntity_throws_on_a_null_user() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordUser)null!).ToUserEntity());

    [Fact]
    public void ToMemberEntity_throws_on_a_null_member() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordMember)null!).ToMemberEntity(222UL));

    [Fact]
    public void ToRoleEntity_throws_on_a_null_role() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordRole)null!).ToRoleEntity(222UL));

    [Fact]
    public void ToMessageEntity_throws_on_a_null_message() =>
        Assert.Throws<ArgumentNullException>(() => ((DiscordMessage)null!).ToMessageEntity());

    [Fact]
    public void ToHistoryEntity_throws_on_a_null_message() =>
        Assert.Throws<ArgumentNullException>(
            () => ((DiscordMessage)null!).ToHistoryEntity(HistoryChangeType.Created));
}
