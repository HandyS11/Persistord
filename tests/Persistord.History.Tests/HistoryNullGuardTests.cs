using Microsoft.EntityFrameworkCore;
using Persistord.History;
using Persistord.History.Configurations;
using Xunit;

namespace Persistord.History.Tests;

/// <summary>Pins the <c>ArgumentNullException.ThrowIfNull</c> guards on the History
/// configuration and the model-builder extension.</summary>
public class HistoryNullGuardTests
{
    [Fact]
    public void ApplyHistoryModule_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyHistoryModule());

    [Fact]
    public void HistoryConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new MessageHistoryEntityConfiguration().Configure(null!));
}
