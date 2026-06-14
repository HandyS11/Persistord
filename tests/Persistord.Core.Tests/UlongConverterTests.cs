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
}
