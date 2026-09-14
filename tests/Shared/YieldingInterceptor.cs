using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Persistord.Tests.Shared;

/// <summary>
/// Makes one chosen database round-trip complete asynchronously, so the await that consumes it
/// genuinely yields. Everything before it stays synchronous, which keeps the caller's
/// <see cref="SynchronizationContext"/> current up to exactly that await — see
/// <see cref="SynchronizationContextProbe"/>.
/// </summary>
/// <param name="fragment">
/// Matched against each command's SQL, or against the pseudo-operations <c>BEGIN</c> and
/// <c>COMMIT</c> for transactions.
/// </param>
/// <param name="occurrence">Which matching operation to yield on, counting from 1.</param>
internal sealed class YieldingInterceptor(string fragment, int occurrence = 1)
    : DbCommandInterceptor, IDbTransactionInterceptor
{
    private int _matches;

    public async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection,
        TransactionStartingEventData eventData,
        InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        await YieldOnAsync("BEGIN", cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        await YieldOnAsync("COMMIT", cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await YieldOnAsync(command.CommandText, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await YieldOnAsync(command.CommandText, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await YieldOnAsync(command.CommandText, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private Task YieldOnAsync(string operation, CancellationToken cancellationToken) =>
        operation.Contains(fragment, StringComparison.Ordinal) && ++_matches == occurrence
            ? Task.Delay(1, cancellationToken)
            : Task.CompletedTask;
}
