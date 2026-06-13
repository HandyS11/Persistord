using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

public class CoreEntityMappingTests
{
    [Fact]
    public void ToGuildEntity_maps_id_name_owner()
    {
        var guild = Substitute.For<IGuild>();
        guild.Id.Returns(100UL);
        guild.Name.Returns("Test Guild");
        guild.OwnerId.Returns(200UL);

        var entity = guild.ToGuildEntity();

        Assert.Equal(100UL, entity.Id);
        Assert.Equal("Test Guild", entity.Name);
        Assert.Equal(200UL, entity.OwnerId);
    }

    [Fact]
    public void ToUserEntity_maps_id_username_globalname()
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(300UL);
        user.Username.Returns("alice");
        user.GlobalName.Returns("Alice");

        var entity = user.ToUserEntity();

        Assert.Equal(300UL, entity.Id);
        Assert.Equal("alice", entity.Username);
        Assert.Equal("Alice", entity.GlobalName);
    }

    [Fact]
    public void ToUserEntity_tolerates_null_globalname()
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(301UL);
        user.Username.Returns("bob");
        user.GlobalName.Returns((string?)null);

        var entity = user.ToUserEntity();

        Assert.Null(entity.GlobalName);
    }

    [Fact]
    public void ToRoleEntity_maps_id_guild_name_permissions_color()
    {
        var guild = Substitute.For<IGuild>();
        guild.Id.Returns(100UL);
        var role = Substitute.For<IRole>();
        role.Id.Returns(400UL);
        role.Guild.Returns(guild);
        role.Name.Returns("Admins");
        role.Permissions.Returns(new GuildPermissions(8UL));
        role.Color.Returns(new Color(0xFF0000));

        var entity = role.ToRoleEntity();

        Assert.Equal(400UL, entity.Id);
        Assert.Equal(100UL, entity.GuildId);
        Assert.Equal("Admins", entity.Name);
        Assert.Equal(8UL, entity.Permissions);
        Assert.Equal(0xFF0000, entity.Color);
    }

    [Fact]
    public void ToMemberEntity_maps_guild_user_nickname_joinedat()
    {
        var joined = DateTimeOffset.UtcNow;
        var member = Substitute.For<IGuildUser>();
        member.GuildId.Returns(100UL);
        member.Id.Returns(300UL);
        member.Nickname.Returns("Ali");
        member.JoinedAt.Returns(joined);

        var entity = member.ToMemberEntity();

        Assert.Equal(100UL, entity.GuildId);
        Assert.Equal(300UL, entity.UserId);
        Assert.Equal("Ali", entity.Nickname);
        Assert.Equal(joined, entity.JoinedAt);
    }
}
