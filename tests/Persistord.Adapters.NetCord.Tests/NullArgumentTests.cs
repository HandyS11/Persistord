using NetCord;
using NetCord.Rest;
using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.NetCord.Tests;

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
        Assert.Throws<ArgumentNullException>(() => ((RestGuild)null!).ToGuildEntity());

    [Fact]
    public void ToUserEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((User)null!).ToUserEntity());

    [Fact]
    public void ToRoleEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((Role)null!).ToRoleEntity());

    [Fact]
    public void ToMemberEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((GuildUser)null!).ToMemberEntity());

    [Fact]
    public void ToMessageEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((RestMessage)null!).ToMessageEntity());

    [Fact]
    public void ToHistoryEntity_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((RestMessage)null!).ToHistoryEntity(HistoryChangeType.Created));
}
