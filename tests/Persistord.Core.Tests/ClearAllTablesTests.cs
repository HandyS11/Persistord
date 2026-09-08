using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Core.Tests;

public class ClearAllTablesTests
{
    [Fact]
    public async Task Clear_empties_every_table_dependents_first()
    {
        var (connection, context) =
            SqliteFixture.Create<GuildPurgeTests.PurgeContext>(o => new GuildPurgeTests.PurgeContext(o));
        using (connection)
        await using (context)
        {
            await context.Guilds.AddAsync(new GuildEntity
            {
                Id = 1UL
            });
            await context.Parents.AddAsync(new GuildPurgeTests.ScopedParent
            {
                Id = 10, GuildId = 1UL
            });
            await context.Children.AddAsync(new GuildPurgeTests.ScopedChild
            {
                GuildId = 1UL, ParentId = 10
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var deleted = await context.ClearAllTablesAsync();

            Assert.Equal(3, deleted);
            Assert.Empty(await context.Guilds.ToListAsync());
            Assert.Empty(await context.Parents.ToListAsync());
            Assert.Empty(await context.Children.ToListAsync());
        }
    }

    [Fact]
    public async Task Clear_on_an_empty_database_deletes_nothing()
    {
        var (connection, context) =
            SqliteFixture.Create<GuildPurgeTests.PurgeContext>(o => new GuildPurgeTests.PurgeContext(o));
        using (connection)
        await using (context)
        {
            Assert.Equal(0, await context.ClearAllTablesAsync());
        }
    }

    [Fact]
    public async Task Clear_guards_its_context() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).ClearAllTablesAsync());
}
