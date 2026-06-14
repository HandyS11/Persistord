using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

/// <summary>Pins the <c>= string.Empty</c> defaults on required string properties.</summary>
public class EntityDefaultsTests
{
    [Fact]
    public void GuildEntity_name_defaults_to_empty() =>
        Assert.Equal(string.Empty, new GuildEntity().Name);

    [Fact]
    public void UserEntity_username_defaults_to_empty() =>
        Assert.Equal(string.Empty, new UserEntity().Username);

    [Fact]
    public void RoleEntity_name_defaults_to_empty() =>
        Assert.Equal(string.Empty, new RoleEntity().Name);

    [Fact]
    public void ChannelEntity_name_defaults_to_empty() =>
        Assert.Equal(string.Empty, new ChannelEntity().Name);
}
