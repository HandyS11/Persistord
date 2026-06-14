using Discord;
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

/// <summary>
/// Verifies every public mapper rejects a null source. These pin the
/// <c>ArgumentNullException.ThrowIfNull</c> guards against mutation.
/// </summary>
public class NullArgumentTests
{
    [Fact]
    public void ToChannelEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IGuildChannel)null!).ToChannelEntity());

    [Fact]
    public void ToGuildEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IGuild)null!).ToGuildEntity());

    [Fact]
    public void ToUserEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IUser)null!).ToUserEntity());

    [Fact]
    public void ToRoleEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IRole)null!).ToRoleEntity());

    [Fact]
    public void ToMemberEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IGuildUser)null!).ToMemberEntity());

    [Fact]
    public void ToMessageEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IMessage)null!).ToMessageEntity());

    [Fact]
    public void ToHistoryEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((IMessage)null!).ToHistoryEntity(HistoryChangeType.Created));
}
