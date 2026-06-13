using Persistord.History.Entities;
using Persistord.Messages.Entities;
using Xunit;

namespace Persistord.History.Tests;

public class HistoryModelTests
{
    [Fact]
    public void DeleteHistory_SurvivesSoftDeletedMessage()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            var message = new MessageEntity { Id = 100UL, ChannelId = 1UL, AuthorId = 2UL, Content = "original" };
            context.Messages.Add(message);
            context.MessageHistory.Add(new MessageHistoryEntity
            {
                MessageId = 100UL,
                Content = "original",
                RecordedAt = DateTimeOffset.UtcNow,
                ChangeType = HistoryChangeType.Created,
            });
            context.SaveChanges();

            // Soft-delete the message and log the delete in history.
            message.IsDeleted = true;
            message.DeletedAt = DateTimeOffset.UtcNow;
            context.MessageHistory.Add(new MessageHistoryEntity
            {
                MessageId = 100UL,
                Content = null,
                RecordedAt = DateTimeOffset.UtcNow,
                ChangeType = HistoryChangeType.Deleted,
            });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            // The (soft-deleted) message row still exists, so both history rows survive.
            Assert.NotNull(context.Messages.Single(m => m.Id == 100UL));
            var rows = context.MessageHistory.Where(h => h.MessageId == 100UL).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.ChangeType == HistoryChangeType.Deleted);
        }
    }

    [Fact]
    public void HistoryIndex_IsOnMessageIdAndRecordedAt()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            var entity = context.Model.FindEntityType(typeof(MessageHistoryEntity))!;
            var index = entity.GetIndexes().Single();
            Assert.Equal(
                new[] { nameof(MessageHistoryEntity.MessageId), nameof(MessageHistoryEntity.RecordedAt) },
                index.Properties.Select(p => p.Name).ToArray());
        }
    }
}
