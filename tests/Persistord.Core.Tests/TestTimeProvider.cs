namespace Persistord.Core.Tests;

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
