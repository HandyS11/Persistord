using Persistord.Core.Conversions;
using Xunit;

namespace Persistord.Core.Tests;

public class UlongConverterTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(175928847299117063UL)] // a real-shaped snowflake
    [InlineData(ulong.MaxValue)] // exercises the high bit
    public void RoundTrip_IsExact(ulong value)
    {
        var converter = new UlongToLongConverter();
        var stored = (long)converter.ConvertToProvider(value)!;
        var back = (ulong)converter.ConvertFromProvider(stored)!;
        Assert.Equal(value, back);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42UL)]
    public void NullableRoundTrip_IsExact(ulong? value)
    {
        var converter = new NullableUlongToLongConverter();
        var stored = converter.ConvertToProvider(value);
        var back = (ulong?)converter.ConvertFromProvider(stored);
        Assert.Equal(value, back);
    }

    [Fact]
    public void NullableConverter_expression_handles_both_branches()
    {
        // EF's ConvertToProvider/ConvertFromProvider short-circuit nulls before the lambda
        // runs, so the HasValue branch is only reachable by invoking the raw expression.
        var converter = new NullableUlongToLongConverter();
        var toProvider = converter.ConvertToProviderExpression.Compile();
        var fromProvider = converter.ConvertFromProviderExpression.Compile();

        Assert.Null(toProvider(null));
        Assert.Equal(42L, toProvider(42UL));
        Assert.Null(fromProvider(null));
        Assert.Equal(42UL, fromProvider(42L));
    }
}
