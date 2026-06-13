using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.History.Tests;

public sealed class TestContext : Persistord.Core.DiscordDbContext
{
    public TestContext(DbContextOptions<TestContext> options)
        : base(options)
    {
    }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(filterDeleted: false);
        modelBuilder.ApplyHistoryModule();
    }

    public static (SqliteConnection, TestContext) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestContext>().UseSqlite(connection).Options;
        var context = new TestContext(options);
        context.Database.EnsureCreated();
        return (connection, context);
    }
}
