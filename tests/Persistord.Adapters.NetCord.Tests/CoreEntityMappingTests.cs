using Xunit;

namespace Persistord.Adapters.NetCord.Tests;

public class CoreEntityMappingTests
{
    [Fact]
    public void Guild_maps_id_name_and_owner()
    {
        var entity = NetCordFakes.MakeGuild(id: 100UL, name: "a-guild", ownerId: 101UL).ToGuildEntity();

        Assert.Equal(100UL, entity.Id);
        Assert.Equal("a-guild", entity.Name);
        Assert.Equal(101UL, entity.OwnerId);
    }

    [Fact]
    public void Guild_leaves_bot_lifecycle_fields_unset()
    {
        var entity = NetCordFakes.MakeGuild().ToGuildEntity();

        Assert.Null(entity.JoinedAt);
        Assert.Null(entity.LeftAt);
    }

    [Fact]
    public void User_maps_id_username_and_global_name()
    {
        var entity = NetCordFakes.MakeUser(id: 789UL, username: "someone", globalName: "Someone").ToUserEntity();

        Assert.Equal(789UL, entity.Id);
        Assert.Equal("someone", entity.Username);
        Assert.Equal("Someone", entity.GlobalName);
    }

    [Fact]
    public void User_tolerates_a_missing_global_name() =>
        Assert.Null(NetCordFakes.MakeUser(globalName: null).ToUserEntity().GlobalName);

    [Fact]
    public void Member_maps_composite_key_nickname_and_joined_at()
    {
        var joined = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var entity = NetCordFakes.MakeMember(guildId: 222UL, userId: 789UL, nickname: "nick", joinedAt: joined)
            .ToMemberEntity();

        Assert.Equal(222UL, entity.GuildId);
        Assert.Equal(789UL, entity.UserId);
        Assert.Equal("nick", entity.Nickname);
        Assert.Equal(joined, entity.JoinedAt);
    }

    [Fact]
    public void Member_tolerates_a_missing_nickname_and_join_date()
    {
        var entity = NetCordFakes.MakeMember(nickname: null, joinedAt: null).ToMemberEntity();

        Assert.Null(entity.Nickname);
        Assert.Null(entity.JoinedAt);
    }

    [Fact]
    public void Role_maps_id_guild_and_name()
    {
        var entity = NetCordFakes.MakeRole(id: 333UL, guildId: 222UL, name: "admin").ToRoleEntity();

        Assert.Equal(333UL, entity.Id);
        Assert.Equal(222UL, entity.GuildId);
        Assert.Equal("admin", entity.Name);
    }

    [Fact]
    public void Role_maps_permissions_losslessly() =>
        Assert.Equal(
            (ulong)global::NetCord.Permissions.Administrator,
            NetCordFakes.MakeRole(permissions: global::NetCord.Permissions.Administrator).ToRoleEntity().Permissions);

    [Fact]
    public void Role_maps_the_primary_colour() =>
        Assert.Equal(0xFF00FF, NetCordFakes.MakeRole(color: 0xFF00FF).ToRoleEntity().Color);
}
