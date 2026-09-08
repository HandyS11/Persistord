using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.Managed.Configurations;
using Persistord.Managed.Entities;
using Persistord.Testing;
using Xunit;

namespace Persistord.Managed.Tests;

public class ManagedModelTests
{
    [Theory]
    [InlineData(typeof(ManagedCategory))]
    [InlineData(typeof(ManagedChannel))]
    [InlineData(typeof(ManagedMessage))]
    [InlineData(typeof(ManagedWebhook))]
    public void Every_resource_is_unique_per_guild_scope_and_key(Type resource)
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        var entityType = context.Model.FindEntityType(resource)!;
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.IsUnique
                     && index.Properties.Select(p => p.Name).SequenceEqual(["GuildId", "Scope", "Key"]));
    }

    [Fact]
    public void Managed_message_indexes_the_discord_id()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        Assert.Contains(
            context.Model.FindEntityType(typeof(ManagedMessage))!.GetIndexes(),
            index => index.Properties.Select(p => p.Name).SequenceEqual([nameof(ManagedMessage.DiscordId)]));
    }

    [Fact]
    public void Scope_is_a_required_column()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        var scope = context.Model.FindEntityType(typeof(ManagedCategory))!
            .FindProperty(nameof(ManagedCategory.Scope))!;

        Assert.False(scope.IsNullable);
        Assert.Equal(64, scope.GetMaxLength());
    }

    [Fact]
    public void Resources_cascade_from_the_guild_root()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o, guildRoot: true));

        context.AssertCascade<ManagedCategory, GuildEntity>();
        context.AssertCascade<ManagedChannel, GuildEntity>();
        context.AssertCascade<ManagedMessage, GuildEntity>();
        context.AssertCascade<ManagedWebhook, GuildEntity>();
    }

    [Fact]
    public void Tables_are_named_by_the_module_not_by_the_dbset()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        Assert.Equal("ManagedCategories", context.Model.FindEntityType(typeof(ManagedCategory))!.GetTableName());
        Assert.Equal("ManagedChannels", context.Model.FindEntityType(typeof(ManagedChannel))!.GetTableName());
        Assert.Equal("ManagedMessages", context.Model.FindEntityType(typeof(ManagedMessage))!.GetTableName());
        Assert.Equal("ManagedWebhooks", context.Model.FindEntityType(typeof(ManagedWebhook))!.GetTableName());
    }

    [Fact]
    public async Task A_row_round_trips_with_a_high_bit_snowflake_and_no_scope()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.Messages.AddAsync(new ManagedMessage
        {
            GuildId = ulong.MaxValue,
            Key = "dashboard",
            ChannelDiscordId = 1UL,
            DiscordId = ulong.MaxValue - 1UL,
            ContentHash = "abc",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var row = await context.Messages.SingleAsync();
        Assert.Equal(ulong.MaxValue, row.GuildId);
        Assert.Equal(ulong.MaxValue - 1UL, row.DiscordId);
        Assert.Equal(ManagedScope.Global, row.Scope);
        Assert.Equal("abc", row.ContentHash);
    }

    [Fact]
    public void ApplyManagedModule_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyManagedModule());

    [Fact]
    public void Configurations_throw_on_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ManagedCategoryConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new ManagedChannelConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new ManagedMessageConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new ManagedWebhookConfiguration().Configure(null!));
    }
}
