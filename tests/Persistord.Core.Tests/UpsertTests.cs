using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistord.Testing;
using Xunit;

namespace Persistord.Core.Tests;

public class UpsertTests
{
    private static WidgetEntity NewWidget(ulong guildId, string key) => new()
    {
        GuildId = guildId, Key = key
    };

    [Fact]
    public async Task Upsert_inserts_when_the_natural_key_is_missing()
    {
        var (database, context) = SqliteFixture.Create<UpsertContext>(o => new UpsertContext(o));
        using (database)
        await using (context)
        {
            var row = await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);

            Assert.Equal(42UL, row.DiscordId);
            Assert.Single(await context.Widgets.ToListAsync());
        }
    }

    [Fact]
    public async Task Upsert_updates_the_existing_row_instead_of_inserting_a_second()
    {
        var (database, context) = SqliteFixture.Create<UpsertContext>(o => new UpsertContext(o));
        using (database)
        await using (context)
        {
            await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);
            context.ChangeTracker.Clear();

            var row = await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 43UL);

            Assert.Equal(43UL, row.DiscordId);
            Assert.Single(await context.Widgets.ToListAsync());
        }
    }

    [Fact]
    public async Task UpsertIfChanged_reports_no_change_and_writes_nothing_when_values_match()
    {
        var (database, context) = SqliteFixture.Create<UpsertContext>(o => new UpsertContext(o));
        using (database)
        await using (context)
        {
            await context.Widgets.AddAsync(NewWidget(1UL, "dash"));
            context.Widgets.Local.Single().DiscordId = 42UL;
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var result = await context.Widgets.UpsertIfChangedAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);

            Assert.False(result.Changed);
            Assert.Equal(42UL, result.Entity.DiscordId);
            Assert.DoesNotContain(
                EntityState.Modified,
                context.ChangeTracker.Entries().Select(e => e.State));
        }
    }

    [Fact]
    public async Task UpsertIfChanged_reports_a_change_when_a_value_differs()
    {
        var (database, context) = SqliteFixture.Create<UpsertContext>(o => new UpsertContext(o));
        using (database)
        await using (context)
        {
            await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);
            context.ChangeTracker.Clear();

            var result = await context.Widgets.UpsertIfChangedAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 99UL);

            Assert.True(result.Changed);
            Assert.Equal(99UL, result.Entity.DiscordId);
        }
    }

    [Fact]
    public async Task UpsertIfChanged_persists_the_change_under_context_wide_NoTracking()
    {
        var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        var context = database.CreateContext<UpsertContext>(
            o => new UpsertContext(o),
            configure: builder => builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        using (database)
        await using (context)
        {
            await context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 42UL);
            context.ChangeTracker.Clear();

            var result = await context.Widgets.UpsertIfChangedAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.DiscordId = 99UL);

            // Before the fix, the natural-key read honoured the context-wide NoTracking default,
            // so context.Entry(row) reported Detached, the dirty check read that as "no change",
            // and SaveChangesAsync was never called: Changed came back false and the mutation was
            // silently lost even though the returned entity showed the new value.
            Assert.True(result.Changed);
            Assert.Equal(99UL, result.Entity.DiscordId);

            context.ChangeTracker.Clear();
            var stored = await context.Widgets.SingleAsync(w => w.GuildId == 1UL && w.Key == "dash");
            Assert.Equal(99UL, stored.DiscordId);
        }
    }

    [Fact]
    public async Task Upsert_recovers_when_another_writer_wins_the_insert_race()
    {
        await using var database = SqliteTestDatabase.Shared(schema: TestSchema.EnsureCreated);
        var interference = new InterferingInsertInterceptor(database);
        await using var context = database.CreateContext<UpsertContext>(o => new UpsertContext(o), interference);

        var row = await context.Widgets.UpsertAsync(
            w => w.GuildId == 1UL && w.Key == "dash",
            () => NewWidget(1UL, "dash"),
            w => w.DiscordId = 99UL);

        Assert.Equal(99UL, row.DiscordId);

        await using var verify = database.CreateContext<UpsertContext>(o => new UpsertContext(o));
        var all = await verify.Widgets.ToListAsync();
        Assert.Single(all);
        Assert.Equal(99UL, all[0].DiscordId);
    }

    [Fact]
    public async Task Upsert_rethrows_when_the_failure_is_not_a_lost_race()
    {
        var (database, context) = SqliteFixture.Create<UpsertContext>(o => new UpsertContext(o));
        using (database)
        await using (context)
        {
            // Key is required in the store; a null value fails the insert and no row exists to
            // recover onto, so the original DbUpdateException must surface.
            await Assert.ThrowsAsync<DbUpdateException>(() => context.Widgets.UpsertAsync(
                w => w.GuildId == 1UL && w.Key == "dash",
                () => NewWidget(1UL, "dash"),
                w => w.Key = null!));
        }
    }

    [Fact]
    public async Task Upsert_guards_its_arguments()
    {
        var (database, context) = SqliteFixture.Create<UpsertContext>(o => new UpsertContext(o));
        using (database)
        await using (context)
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                context.Widgets.UpsertAsync(null!, () => NewWidget(1UL, "d"), _ => { }));
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                context.Widgets.UpsertAsync(w => w.Key == "d", null!, _ => { }));
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                context.Widgets.UpsertAsync(w => w.Key == "d", () => NewWidget(1UL, "d"), null!));
        }
    }

    /// <summary>Inserts the same natural key from a second context while the first one saves.</summary>
    private sealed class InterferingInsertInterceptor(SqliteTestDatabase database) : SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;
                await using var other = database.CreateContext<UpsertContext>(o => new UpsertContext(o));
                await other.Widgets.AddAsync(
                    new WidgetEntity
                    {
                        GuildId = 1UL, Key = "dash", DiscordId = 7UL
                    },
                    cancellationToken);
                await other.SaveChangesAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

public sealed class WidgetEntity
{
    public long Id { get; set; }

    public ulong GuildId { get; set; }

    public string Key { get; set; } = string.Empty;

    public ulong DiscordId { get; set; }
}

public sealed class UpsertContext(DbContextOptions<UpsertContext> options)
    : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<WidgetEntity> Widgets => Set<WidgetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<WidgetEntity>().Property(w => w.Key).IsRequired();
        modelBuilder.Entity<WidgetEntity>().HasIndex(w => new
        {
            w.GuildId, w.Key
        }).IsUnique();
    }
}
