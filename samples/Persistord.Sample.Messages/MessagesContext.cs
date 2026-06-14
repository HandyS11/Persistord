using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample.Messages;

/// <summary>Context for the messages sample: core skeleton plus the Messages module.</summary>
public sealed class MessagesContext(DbContextOptions<MessagesContext> options) : DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
    }
}
