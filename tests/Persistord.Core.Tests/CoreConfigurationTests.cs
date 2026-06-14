using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Conversions;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

/// <summary>
/// Asserts the EF model metadata produced by the core entity configurations:
/// caller-supplied keys, secondary indexes, the self-referencing channel FK, and the
/// global snowflake conversions. These pin configuration calls that EF conventions
/// would not otherwise reproduce.
/// </summary>
public class CoreConfigurationTests
{
    private static IModel BuildModel()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            return context.Model;
        }
    }

    [Theory]
    [InlineData(typeof(GuildEntity), nameof(GuildEntity.Id))]
    [InlineData(typeof(UserEntity), nameof(UserEntity.Id))]
    [InlineData(typeof(RoleEntity), nameof(RoleEntity.Id))]
    [InlineData(typeof(ChannelEntity), nameof(ChannelEntity.Id))]
    public void Snowflake_key_is_caller_supplied_not_store_generated(Type entity, string key)
    {
        var property = BuildModel().FindEntityType(entity)!.FindProperty(key)!;
        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
    }

    [Theory]
    [InlineData(typeof(ChannelEntity), nameof(ChannelEntity.GuildId))]
    [InlineData(typeof(RoleEntity), nameof(RoleEntity.GuildId))]
    public void GuildId_is_indexed(Type entity, string property)
    {
        var indexes = BuildModel().FindEntityType(entity)!.GetIndexes();
        Assert.Contains(indexes, i => i.Properties.Select(p => p.Name).SequenceEqual([property]));
    }

    [Fact]
    public void Channel_has_self_referencing_parent_fk_with_restrict()
    {
        var channel = BuildModel().FindEntityType(typeof(ChannelEntity))!;
        var fk = Assert.Single(channel.GetForeignKeys(), f => f.PrincipalEntityType == channel);

        Assert.Equal(nameof(ChannelEntity.ParentId), Assert.Single(fk.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void Ulong_properties_use_the_snowflake_converter()
    {
        var id = BuildModel().FindEntityType(typeof(GuildEntity))!.FindProperty(nameof(GuildEntity.Id))!;
        Assert.IsType<UlongToLongConverter>(id.GetValueConverter());
    }

    [Fact]
    public void Nullable_ulong_properties_use_the_nullable_snowflake_converter()
    {
        var parentId = BuildModel().FindEntityType(typeof(ChannelEntity))!
            .FindProperty(nameof(ChannelEntity.ParentId))!;
        Assert.IsType<NullableUlongToLongConverter>(parentId.GetValueConverter());
    }

    [Fact]
    public void ParentId_round_trips_through_the_nullable_converter()
    {
        var (connection, context) = SqliteFixture.Create();
        using (connection)
        using (context)
        {
            // The parent uses a high-bit id so the round-trip exercises the converter.
            context.Channels.Add(new ChannelEntity
            {
                Id = ulong.MaxValue, GuildId = 2UL, Name = "parent"
            });
            context.Channels.Add(new ChannelEntity
            {
                Id = 1UL, GuildId = 2UL, ParentId = ulong.MaxValue, Name = "child"
            });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var child = context.Channels.Single(c => c.Id == 1UL);
            Assert.Equal(ulong.MaxValue, child.ParentId);
        }
    }
}
