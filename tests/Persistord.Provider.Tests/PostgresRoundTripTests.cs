using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Xunit;

namespace Persistord.Provider.Tests;

public sealed class PgContext(DbContextOptions<PgContext> options)
    : Persistord.Core.DiscordDbContext(options)
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

public class PostgresRoundTripTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [SkippableFact]
    public async Task Snowflake_RoundTrips_OnPostgres()
    {
        Skip.IfNot(fixture.Available, "Docker/Postgres not available.");

        var connectionString = fixture.ConnectionString
                               ?? throw new InvalidOperationException(
                                   "Fixture reported available but has no connection string.");

        var options = new DbContextOptionsBuilder<PgContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new PgContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Guilds.Add(new GuildEntity
        {
            Id = ulong.MaxValue, Name = "g", OwnerId = 1UL
        });
        context.Messages.Add(new MessageEntity
        {
            Id = 5UL,
            ChannelId = 6UL,
            AuthorId = 7UL,
            Embeds =
            {
                new Embed
                {
                    Title = "t"
                }
            },
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var guild = await context.Guilds.SingleAsync();
        Assert.Equal(ulong.MaxValue, guild.Id);
    }
}
