using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Persistord.Tests.Shared;

/// <summary>
/// Fails the first asynchronous non-query command whose SQL contains <paramref name="fragment"/>, so a
/// test can break a multi-statement helper halfway and check that the statements before the failure
/// were rolled back. Later matching commands run normally, so the test can still use the context.
/// </summary>
/// <param name="fragment">Matched against each command's SQL.</param>
internal sealed class FailingCommandInterceptor(string fragment) : DbCommandInterceptor
{
    private bool _fired;

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_fired || !command.CommandText.Contains(fragment, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(result);
        }

        _fired = true;
        throw new InvalidOperationException($"Injected failure on: {fragment}");
    }
}
