using System.Globalization;
using DSharpPlus.Entities;
using Xunit;
using static Persistord.Adapters.DSharpPlus.Tests.DSharpPlusFakes;

namespace Persistord.Adapters.DSharpPlus.Tests;

public class CoreEntityMappingTests
{
    [Fact]
    public void ToGuildEntity_maps_id_name_and_owner()
    {
        var entity = MakeGuild(id: 100UL, name: "a-guild", ownerId: 101UL).ToGuildEntity();

        Assert.Equal(100UL, entity.Id);
        Assert.Equal("a-guild", entity.Name);
        Assert.Equal(101UL, entity.OwnerId);
    }

    [Fact]
    public void ToGuildEntity_does_not_touch_the_bot_membership_lifecycle()
    {
        // JoinedAt/LeftAt record when the BOT joined and left. That is the consumer's
        // state, not data on a Discord guild, so the mapper must leave both alone.
        var entity = MakeGuild().ToGuildEntity();

        Assert.Null(entity.JoinedAt);
        Assert.Null(entity.LeftAt);
    }

    [Fact]
    public void ToUserEntity_maps_id_and_username()
    {
        var entity = MakeUser(id: 789UL, username: "someone").ToUserEntity();

        Assert.Equal(789UL, entity.Id);
        Assert.Equal("someone", entity.Username);
    }

    [Fact]
    public void ToUserEntity_leaves_global_name_null_because_DSharpPlus_does_not_expose_it()
    {
        // DiscordUser in DSharpPlus 4.5.3 has no GlobalName member at all. The nearest
        // thing, DiscordMember.DisplayName, is a cache-resolved nickname fallback — a
        // different concept, and it throws on a cache-less member. So this stays null.
        Assert.Null(MakeUser().ToUserEntity().GlobalName);
    }

    [Fact]
    public void ToUserEntity_maps_a_nameless_user_to_an_empty_username()
    {
        var user = Make<DiscordUser>("""{"id":"789"}""");

        Assert.Equal(string.Empty, user.ToUserEntity().Username);
    }

    [Fact]
    public void ToMemberEntity_maps_the_composite_key_from_the_member_and_the_argument()
    {
        var entity = MakeMember(userId: 789UL).ToMemberEntity(222UL);

        Assert.Equal(222UL, entity.GuildId);
        Assert.Equal(789UL, entity.UserId);
    }

    [Fact]
    public void ToMemberEntity_maps_nickname_and_join_date()
    {
        var entity = MakeMember(nickname: "nick", joinedAt: "2026-02-03T04:05:06+00:00").ToMemberEntity(222UL);

        Assert.Equal("nick", entity.Nickname);
        Assert.Equal(
            DateTimeOffset.Parse("2026-02-03T04:05:06+00:00", CultureInfo.InvariantCulture),
            entity.JoinedAt);
    }

    [Fact]
    public void ToMemberEntity_tolerates_a_member_with_no_nickname()
    {
        Assert.Null(MakeMember(nickname: null).ToMemberEntity(222UL).Nickname);
    }

    [Fact]
    public void ToRoleEntity_maps_the_guild_id_from_the_argument()
    {
        // DiscordRole has no publicly reachable guild id — the value lives in an internal
        // _guild_id field that deserialization does not even populate — so the caller
        // supplies it.
        Assert.Equal(222UL, MakeRole().ToRoleEntity(222UL).GuildId);
    }

    [Fact]
    public void ToRoleEntity_maps_id_name_permissions_and_color()
    {
        var entity = MakeRole(id: 333UL, name: "admin", permissions: 8L, color: 0xFF00FF).ToRoleEntity(222UL);

        Assert.Equal(333UL, entity.Id);
        Assert.Equal("admin", entity.Name);
        Assert.Equal(8UL, entity.Permissions);
        Assert.Equal(0xFF00FF, entity.Color);
    }

    [Fact]
    public void ToRoleEntity_preserves_a_permission_bitfield_with_the_high_bit_set()
    {
        // DSharpPlus's Permissions enum is Int64-backed while RoleEntity.Permissions is
        // ulong, so the conversion must be unchecked and must not lose or sign-extend bits.
        var entity = MakeRole(permissions: long.MinValue).ToRoleEntity(222UL);

        Assert.Equal(9223372036854775808UL, entity.Permissions);
    }

    [Fact]
    public void ToRoleEntity_maps_a_nameless_role_to_an_empty_name()
    {
        var role = Make<DiscordRole>("""{"id":"333","permissions":0,"color":0}""");

        Assert.Equal(string.Empty, role.ToRoleEntity(222UL).Name);
    }
}
