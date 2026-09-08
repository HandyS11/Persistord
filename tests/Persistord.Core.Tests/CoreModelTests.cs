using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class CoreModelTests
{
    [Fact]
    public void Snowflake_PersistsAndReadsBack_WithHighBitValue()
    {
        var (database, context) = SqliteFixture.Create();
        using (database)
        using (context)
        {
            context.Guilds.Add(new GuildEntity
            {
                Id = ulong.MaxValue, Name = "g", OwnerId = 1UL
            });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var loaded = Assert.Single(context.Guilds.ToList());
            Assert.Equal(ulong.MaxValue, loaded.Id);
        }
    }

    [Fact]
    public void Snowflake_IsStoredAsLongColumn()
    {
        var (database, context) = SqliteFixture.Create();
        using (database)
        using (context)
        {
            var column = context.Model.FindEntityType(typeof(GuildEntity))!
                .FindProperty(nameof(GuildEntity.Id))!;
            Assert.Equal(typeof(long), column.GetValueConverter()!.ProviderClrType);
        }
    }

    [Fact]
    public void Member_HasCompositeKey()
    {
        var (database, context) = SqliteFixture.Create();
        using (database)
        using (context)
        {
            var key = context.Model.FindEntityType(typeof(MemberEntity))!.FindPrimaryKey()!;
            Assert.Equal(
                new[]
                {
                    nameof(MemberEntity.GuildId), nameof(MemberEntity.UserId)
                },
                key.Properties.Select(p => p.Name).ToArray());
        }
    }
}
