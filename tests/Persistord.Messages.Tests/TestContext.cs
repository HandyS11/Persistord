using Microsoft.EntityFrameworkCore;
using Persistord.Messages;
using Persistord.Messages.Entities;
using Persistord.Testing;

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

    public static (SqliteTestDatabase Database, TestContext Context) Create(bool filterDeleted = true)
    {
        var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        return (database, database.CreateContext<TestContext>(options => new TestContext(options, filterDeleted)));
    }
}
