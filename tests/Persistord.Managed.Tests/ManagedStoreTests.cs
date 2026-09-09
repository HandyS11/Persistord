using Microsoft.EntityFrameworkCore;
using Persistord.Managed.Entities;
using Persistord.Testing;
using Xunit;

namespace Persistord.Managed.Tests;

public class ManagedStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Two_upserts_of_the_same_global_key_produce_one_row()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 100UL);
        context.ChangeTracker.Clear();
        var second = await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 200UL);

        Assert.Equal(200UL, second.DiscordId);
        Assert.Equal(ManagedScope.Global, second.Scope);
        Assert.Single(await context.Channels.ToListAsync());
    }

    [Fact]
    public async Task Upsert_applies_the_configure_callback()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        var message = await context.UpsertManagedAsync<ManagedMessage>(
            1UL,
            "server-7",
            "dashboard",
            500UL,
            m =>
            {
                m.ChannelDiscordId = 42UL;
                m.ContentHash = "hash-1";
            });

        Assert.Equal(42UL, message.ChannelDiscordId);
        Assert.Equal("hash-1", message.ContentHash);
        Assert.Equal("server-7", message.Scope);
    }

    [Fact]
    public async Task The_same_key_in_two_scopes_is_two_rows()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 100UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-2", "chat", 200UL);

        Assert.Equal(2, await context.Channels.CountAsync());
    }

    [Fact]
    public async Task Find_returns_the_row_or_null()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-1", "rust", 100UL);
        context.ChangeTracker.Clear();

        Assert.NotNull(await context.FindManagedAsync<ManagedCategory>(1UL, "server-1", "rust"));
        Assert.Null(await context.FindManagedAsync<ManagedCategory>(1UL, null, "rust"));
        Assert.Null(await context.FindManagedAsync<ManagedCategory>(2UL, "server-1", "rust"));
    }

    [Fact]
    public async Task DeleteScope_removes_that_scope_across_all_four_tables_only()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-1", "rust", 1UL);
        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-2", "rust", 10UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 2UL);
        await context.UpsertManagedAsync<ManagedMessage>(1UL, "server-1", "dash", 3UL);
        await context.UpsertManagedAsync<ManagedMessage>(1UL, "server-2", "dash", 11UL);
        await context.UpsertManagedAsync<ManagedWebhook>(1UL, "server-1", "bridge", 4UL, w => w.Token = "t");
        await context.UpsertManagedAsync<ManagedWebhook>(1UL, "server-2", "bridge", 12UL, w => w.Token = "t2");
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-2", "chat", 5UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "log", 6UL);
        await context.UpsertManagedAsync<ManagedChannel>(2UL, "server-1", "chat", 7UL);
        context.ChangeTracker.Clear();

        var deleted = await context.DeleteScopeAsync(1UL, "server-1");

        Assert.Equal(4, deleted);
        Assert.Equal("server-2", (await context.Categories.SingleAsync()).Scope);
        Assert.Equal("server-2", (await context.Messages.SingleAsync()).Scope);
        Assert.Equal("server-2", (await context.Webhooks.SingleAsync()).Scope);
        Assert.Equal(3, await context.Channels.CountAsync()); // server-2, global, other guild
    }

    [Fact]
    public async Task DeleteScope_can_target_the_global_scope()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "log", 1UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 2UL);
        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.DeleteScopeAsync(1UL, null));
        Assert.Equal("server-1", (await context.Channels.SingleAsync()).Scope);
    }

    [Fact]
    public async Task DeleteScope_joins_an_ambient_transaction_so_a_rollback_undoes_it()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-1", "chat", 1UL);
        context.ChangeTracker.Clear();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.DeleteScopeAsync(1UL, "server-1");
            await transaction.RollbackAsync();
        }

        Assert.Equal(1, await context.Channels.CountAsync());
    }

    [Fact]
    public async Task ListScopes_returns_the_distinct_sorted_non_global_scopes()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, "server-2", "chat", 1UL);
        await context.UpsertManagedAsync<ManagedMessage>(1UL, "server-2", "dash", 2UL);
        await context.UpsertManagedAsync<ManagedCategory>(1UL, "server-1", "rust", 3UL);
        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "log", 4UL);
        await context.UpsertManagedAsync<ManagedChannel>(2UL, "server-9", "chat", 5UL);
        context.ChangeTracker.Clear();

        Assert.Equal(["server-1", "server-2"], await context.ListScopesAsync(1UL));
    }

    [Fact]
    public async Task Upserts_are_stamped_by_the_timestamp_interceptor()
    {
        var clock = new FakeClock(Start);
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o, clock));

        await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 100UL);
        clock.Now = Start.AddMinutes(5);
        var updated = await context.UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 200UL);

        Assert.Equal(Start, updated.CreatedAt);
        Assert.Equal(Start.AddMinutes(5), updated.UpdatedAt);
    }

    [Fact]
    public async Task Helpers_guard_their_arguments()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context = database.CreateContext<ManagedContext>(o => new ManagedContext(o));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((DbContext)null!).UpsertManagedAsync<ManagedChannel>(1UL, null, "chat", 1UL));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.UpsertManagedAsync<ManagedChannel>(1UL, null, string.Empty, 1UL));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((DbContext)null!).FindManagedAsync<ManagedChannel>(1UL, null, "chat"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.FindManagedAsync<ManagedChannel>(1UL, null, string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).DeleteScopeAsync(1UL, null));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((DbContext)null!).ListScopesAsync(1UL));
    }

    [Fact]
    public void Normalize_maps_null_to_global_and_passes_through_otherwise()
    {
        Assert.Equal(ManagedScope.Global, ManagedScope.Normalize(null));
        Assert.Equal("server-1", ManagedScope.Normalize("server-1"));
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
