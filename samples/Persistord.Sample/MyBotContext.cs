using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample;

/// <summary>Example derived context wiring all three Persistord modules.</summary>
public sealed class MyBotContext : DiscordDbContext
{
    public MyBotContext(DbContextOptions<MyBotContext> options) : base(options) { }

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
        modelBuilder.ApplyHistoryModule();
    }
}
