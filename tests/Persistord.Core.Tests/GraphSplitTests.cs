using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class GraphSplitTests
{
    [Fact]
    public void Conventions_only_context_maps_no_entity_types()
    {
        var (database, context) = SqliteFixture.Create<ConventionsOnlyContext>(o => new ConventionsOnlyContext(o));
        using (database)
        using (context)
        {
            Assert.Empty(context.Model.GetEntityTypes());
        }
    }

    [Fact]
    public void Graph_context_maps_the_five_skeleton_types()
    {
        var (database, context) = SqliteFixture.Create();
        using (database)
        using (context)
        {
            var mapped = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

            Assert.Equal(5, mapped.Count);
            Assert.Contains(typeof(GuildEntity), mapped);
            Assert.Contains(typeof(ChannelEntity), mapped);
            Assert.Contains(typeof(UserEntity), mapped);
            Assert.Contains(typeof(MemberEntity), mapped);
            Assert.Contains(typeof(RoleEntity), mapped);
        }
    }

    [Fact]
    public void Obsolete_ApplyCoreConfiguration_still_applies_the_graph()
    {
        var (database, context) = SqliteFixture.Create<LegacyGraphContext>(o => new LegacyGraphContext(o));
        using (database)
        using (context)
        {
            Assert.Equal(5, context.Model.GetEntityTypes().Count());
        }
    }

    /// <summary>Pins the one-release forwarder a beta2 consumer's OnModelCreating still calls.</summary>
    private sealed class LegacyGraphContext(DbContextOptions<LegacyGraphContext> options)
        : Persistord.Core.DiscordDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
#pragma warning disable CS0618 // Testing the obsolete forwarder is the point of this context.
            modelBuilder.ApplyCoreConfiguration();
#pragma warning restore CS0618
        }
    }
}
