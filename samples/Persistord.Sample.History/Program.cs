using Microsoft.EntityFrameworkCore;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using Persistord.Sample.History;

// The library never picks a provider; this sample uses SQLite to stay
// self-contained. Any EF Core 10 relational provider works the same way.
var options = new DbContextOptionsBuilder<HistoryContext>()
    .UseSqlite("DataSource=history.db")
    .Options;

await using var db = new HistoryContext(options);
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

const ulong messageId = 7001UL;

// 1. Create the message and record a "Created" history snapshot.
db.Messages.Add(new MessageEntity { Id = messageId, ChannelId = 1001UL, AuthorId = 4242UL, Content = "first draft" });
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId, Content = "first draft", RecordedAt = DateTimeOffset.UtcNow, ChangeType = HistoryChangeType.Created,
});
await db.SaveChangesAsync();

// 2. Edit the message and record an "Edited" snapshot.
var message = await db.Messages.SingleAsync(m => m.Id == messageId);
message.Content = "edited text";
message.EditedAt = DateTimeOffset.UtcNow;
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId, Content = "edited text", RecordedAt = DateTimeOffset.UtcNow, ChangeType = HistoryChangeType.Edited,
});
await db.SaveChangesAsync();

// 3. Soft-delete the message (the row survives so history's FK stays valid) and
//    record a "Deleted" snapshot.
message.IsDeleted = true;
message.DeletedAt = DateTimeOffset.UtcNow;
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId, Content = message.Content, RecordedAt = DateTimeOffset.UtcNow, ChangeType = HistoryChangeType.Deleted,
});
await db.SaveChangesAsync();

// The default query filter hides soft-deleted messages...
var visible = await db.Messages.CountAsync();
// ...but IgnoreQueryFilters() includes them, so the row (and its FK target) is still there.
var includingDeleted = await db.Messages.IgnoreQueryFilters().CountAsync();
Console.WriteLine($"Messages visible by default: {visible}; including soft-deleted: {includingDeleted}");

// The append-only history retains every change in order.
var history = await db.MessageHistory
    .Where(h => h.MessageId == messageId)
    .OrderBy(h => h.Id)
    .ToListAsync();
Console.WriteLine($"History rows: {history.Count}");
foreach (var row in history)
{
    Console.WriteLine($"  {row.ChangeType}: \"{row.Content}\"");
}
