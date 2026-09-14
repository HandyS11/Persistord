using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Persistord.Tests.Shared;

/// <summary>
/// Fails the first asynchronous non-query command whose SQL contains <paramref name="fragment"/>, so a
/// test can break a multi-statement helper halfway and check that the statements before the failure
/// were rolled back.
/// </summary>
/// <param name="fragment">Matched against each command's SQL.</param>
internal sealed class FailingCommandInterceptor(string fragment) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        command.CommandText.Contains(fragment, StringComparison.Ordinal)
            ? throw new InvalidOperationException($"Injected failure on: {fragment}")
            : ValueTask.FromResult(result);
}
