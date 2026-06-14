using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Messages.Tests;

public sealed class TestContext(DbContextOptions<TestContext> options, bool filterDeleted = true)
    : Persistord.Core.DiscordDbContext(options)
{
    internal readonly bool FilterDeleted = filterDeleted;

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(FilterDeleted);
    }

    public static (SqliteConnection, TestContext) Create(bool filterDeleted = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCacheKeyFactory, FilterAwareModelCacheKeyFactory>()
            .Options;
        var context = new TestContext(options, filterDeleted);
        context.Database.EnsureCreated();
        return (connection, context);
    }
}
