using Microsoft.EntityFrameworkCore;
using Persistord.Messages.Entities;
using Xunit;

namespace Persistord.Messages.Tests;

public class SoftDeleteTests
{
    [Fact]
    public void DeletedMessage_IsHidden_ByDefaultFilter()
    {
        var (connection, context) = TestContext.Create(filterDeleted: true);
        using (connection)
        using (context)
        {
            context.Messages.Add(new MessageEntity { Id = 1UL, ChannelId = 2UL, AuthorId = 3UL, IsDeleted = true });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            Assert.Empty(context.Messages.ToList());
            Assert.Single(context.Messages.IgnoreQueryFilters().ToList());
        }
    }

    [Fact]
    public void DeletedMessage_IsVisible_WhenFilterDisabled()
    {
        var (connection, context) = TestContext.Create(filterDeleted: false);
        using (connection)
        using (context)
        {
            context.Messages.Add(new MessageEntity { Id = 1UL, ChannelId = 2UL, AuthorId = 3UL, IsDeleted = true });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            Assert.Single(context.Messages.ToList());
        }
    }
}
