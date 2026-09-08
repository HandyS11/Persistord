using Microsoft.EntityFrameworkCore;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;
using Persistord.Testing;

namespace Persistord.History.Tests;

public sealed class TestContext(DbContextOptions<TestContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(filterDeleted: false);
        modelBuilder.ApplyHistoryModule();
    }

    public static (SqliteTestDatabase Database, TestContext Context) Create()
    {
        var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        return (database, database.CreateContext<TestContext>(options => new TestContext(options)));
    }
}
