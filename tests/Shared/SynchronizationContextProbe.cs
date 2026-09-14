using System.Runtime.CompilerServices;

namespace Persistord.Tests.Shared;

/// <summary>
/// Library code must not resume on its caller's <see cref="SynchronizationContext"/>: a consumer
/// that blocks on one of its tasks from a UI thread or a legacy ASP.NET request would deadlock.
/// The probe runs an operation with a counting context installed as the caller's, and reports how
/// many of Persistord's own continuations were posted back to it. Pair it with
/// <see cref="YieldingInterceptor"/>: an await only reveals whether it captured the context when it
/// actually completes asynchronously, and SQLite never does on its own.
/// </summary>
/// <remarks>
/// Only continuations of async methods declared in a <c>Persistord.*</c> library assembly count.
/// EF Core's own <c>SaveChangesAsync</c> resumes on the caller's context once when the save
/// completes asynchronously, which no caller can prevent and which is not what these tests pin.
/// </remarks>
internal static class SynchronizationContextProbe
{
    /// <summary>Runs <paramref name="operation"/> under a counting context.</summary>
    /// <param name="operation">Starts the asynchronous operation under test.</param>
    /// <returns>The number of Persistord continuations posted back to the caller's context.</returns>
    public static async Task<int> CountPostsAsync(Func<Task> operation)
    {
        var probe = new CountingSynchronizationContext();
        var previous = SynchronizationContext.Current;

        Task task;
        SynchronizationContext.SetSynchronizationContext(probe);
        try
        {
            task = operation();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await task;
        return probe.Posts;
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => _posts;

        public override void Post(SendOrPostCallback d, object? state)
        {
            if (ResumesPersistordCode(state))
            {
                Interlocked.Increment(ref _posts);
            }

            ThreadPool.QueueUserWorkItem(_ => d(state));
        }

        public override SynchronizationContext CreateCopy() => this;

        /// <summary>
        /// An awaited task posts its continuation as the state machine box's <c>MoveNext</c> action; the
        /// box's generic arguments name the state machine, and so the method that awaited.
        /// </summary>
        private static bool ResumesPersistordCode(object? state) =>
            state is Delegate { Target: { } box }
            && box.GetType().IsGenericType
            && box.GetType().GetGenericArguments().Any(type =>
                typeof(IAsyncStateMachine).IsAssignableFrom(type)
                && type.Assembly.GetName().Name is { } assembly
                && assembly.StartsWith("Persistord.", StringComparison.Ordinal)
                && !assembly.EndsWith(".Tests", StringComparison.Ordinal));
    }
}
