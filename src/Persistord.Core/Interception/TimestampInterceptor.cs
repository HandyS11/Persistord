using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistord.Core.Abstractions;

namespace Persistord.Core.Interception;

/// <summary>
/// Stamps <see cref="ICreatedAt"/> and <see cref="IUpdatedAt"/> entities as they are saved, from
/// a <see cref="TimeProvider"/>. Register it with
/// <c>options.AddInterceptors(new TimestampInterceptor(timeProvider))</c>, or pass the provider to
/// <see cref="DiscordDbContext"/>'s two-argument constructor and let the context register it.
/// </summary>
public sealed class TimestampInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the interceptor with <see cref="TimeProvider.System"/>.</summary>
    public TimestampInterceptor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes the interceptor with an explicit clock.</summary>
    /// <param name="timeProvider">The clock to stamp from. Tests inject a fake.</param>
    public TimestampInterceptor(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        // Entries() runs change detection first, so Added/Modified below are final.
        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is ICreatedAt created && created.CreatedAt == default)
                    {
                        created.CreatedAt = now;
                    }

                    if (entry.Entity is IUpdatedAt inserted)
                    {
                        inserted.UpdatedAt = now;
                    }

                    break;

                case EntityState.Modified:
                    if (entry.Entity is IUpdatedAt modified)
                    {
                        modified.UpdatedAt = now;
                    }

                    break;
            }
        }
    }
}
