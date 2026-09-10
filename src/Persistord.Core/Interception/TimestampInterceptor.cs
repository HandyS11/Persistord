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

    /// <summary>
    /// Stamps timestamps on tracked entities at the start of a synchronous save.
    /// </summary>
    /// <param name="eventData">Contextual information about the context being saved.</param>
    /// <param name="result">The current interception result, passed through unchanged.</param>
    /// <returns>The <paramref name="result" /> value passed in; this interceptor never suppresses the save.</returns>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Stamps timestamps on tracked entities at the start of an asynchronous save.
    /// </summary>
    /// <param name="eventData">Contextual information about the context being saved.</param>
    /// <param name="result">The current interception result, passed through unchanged.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the save to complete.</param>
    /// <returns>The <paramref name="result" /> value passed in; this interceptor never suppresses the save.</returns>
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

                    if (entry.Entity is IUpdatedAt updatable)
                    {
                        updatable.UpdatedAt = now;
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
