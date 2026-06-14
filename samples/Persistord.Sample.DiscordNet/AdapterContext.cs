using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample.DiscordNet;

/// <summary>Context for the Discord.Net adapter sample: core skeleton, Messages, and History.</summary>
public sealed class AdapterContext(DbContextOptions<AdapterContext> options) : DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
        modelBuilder.ApplyHistoryModule();
    }
}
