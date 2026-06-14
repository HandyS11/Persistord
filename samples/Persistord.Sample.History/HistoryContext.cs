using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample.History;

/// <summary>Context for the history sample: core skeleton, Messages, and History.</summary>
public sealed class HistoryContext(DbContextOptions<HistoryContext> options) : DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(); // default: soft-deleted messages are filtered out
        modelBuilder.ApplyHistoryModule();
    }
}
