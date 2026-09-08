using Microsoft.EntityFrameworkCore;
using Persistord.Core.Abstractions;
using Persistord.Core.Interception;
using Xunit;

namespace Persistord.Core.Tests;

public class TimestampTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Insert_stamps_created_and_updated()
    {
        var clock = new TestTimeProvider(Start);
        var (database, context) = SqliteFixture.Create<StampContext>(o => new StampContext(o, clock));
        using (database)
        await using (context)
        {
            await context.Notes.AddAsync(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.Equal(Start, note.CreatedAt);
            Assert.Equal(Start, note.UpdatedAt);
        }
    }

    [Fact]
    public async Task Update_moves_only_updated_at()
    {
        var clock = new TestTimeProvider(Start);
        var (database, context) = SqliteFixture.Create<StampContext>(o => new StampContext(o, clock));
        using (database)
        await using (context)
        {
            await context.Notes.AddAsync(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            clock.Now = Start.AddMinutes(5);
            context.Notes.Local.Single().Text = "changed";
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.Equal(Start, note.CreatedAt);
            Assert.Equal(Start.AddMinutes(5), note.UpdatedAt);
        }
    }

    [Fact]
    public async Task Caller_supplied_created_at_is_preserved()
    {
        var clock = new TestTimeProvider(Start);
        var (database, context) = SqliteFixture.Create<StampContext>(o => new StampContext(o, clock));
        using (database)
        await using (context)
        {
            var backfilled = Start.AddYears(-1);
            await context.Notes.AddAsync(new NoteEntity
            {
                Text = "imported", CreatedAt = backfilled
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.Equal(backfilled, note.CreatedAt);
            Assert.Equal(Start, note.UpdatedAt);
        }
    }

    [Fact]
    public async Task A_no_op_upsert_leaves_updated_at_alone()
    {
        var clock = new TestTimeProvider(Start);
        var (database, context) = SqliteFixture.Create<StampContext>(o => new StampContext(o, clock));
        using (database)
        await using (context)
        {
            await context.Notes.UpsertAsync(n => n.Text == "hello", () => new NoteEntity(), n => n.Text = "hello");
            context.ChangeTracker.Clear();

            clock.Now = Start.AddMinutes(5);
            var result = await context.Notes.UpsertIfChangedAsync(
                n => n.Text == "hello",
                () => new NoteEntity(),
                n => n.Text = "hello");

            Assert.False(result.Changed);
            Assert.Equal(Start, (await context.Notes.SingleAsync()).UpdatedAt);
        }
    }

    [Fact]
    public async Task Interceptor_defaults_to_the_system_clock()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var (database, context) = SqliteFixture.Create<StampContext>(o => new StampContext(o, timeProvider: null));
        using (database)
        await using (context)
        {
            await context.Notes.AddAsync(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.InRange(note.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
        }
    }

    [Fact]
    public void Interceptor_guards_its_time_provider() =>
        Assert.Throws<ArgumentNullException>(() => new TimestampInterceptor(null!));

    [Fact]
    public async Task Parameterless_constructor_stamps_from_the_system_clock()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var (database, context) = SqliteFixture.Create<DefaultClockContext>(o => new DefaultClockContext(o));
        using (database)
        await using (context)
        {
            await context.Notes.AddAsync(new NoteEntity
            {
                Text = "hello"
            });
            await context.SaveChangesAsync();

            var note = await context.Notes.SingleAsync();
            Assert.InRange(note.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
        }
    }
}

/// <summary>Registers <see cref="TimestampInterceptor"/> via its parameterless constructor, which
/// has no other caller in this test project.</summary>
public sealed class DefaultClockContext(DbContextOptions<DefaultClockContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<NoteEntity> Notes => Set<NoteEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(new TimestampInterceptor());
    }
}

public sealed class NoteEntity : ICreatedAt, IUpdatedAt
{
    public long Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StampContext(DbContextOptions<StampContext> options, TimeProvider? timeProvider)
    : Persistord.Core.DiscordDbContext(options, timeProvider ?? TimeProvider.System)
{
    public DbSet<NoteEntity> Notes => Set<NoteEntity>();
}
