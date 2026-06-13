using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistord.Core.Conversions;

/// <summary>
/// Converts a Discord snowflake (<see cref="ulong"/>) to a provider <see cref="long"/>
/// using an unchecked bit-faithful cast, so the round-trip is exact for all values
/// including those with the high bit set.
/// </summary>
public sealed class UlongToLongConverter : ValueConverter<ulong, long>
{
    /// <summary>Initializes a new instance of the <see cref="UlongToLongConverter"/> class.</summary>
    public UlongToLongConverter()
        : base(v => unchecked((long)v), v => unchecked((ulong)v))
    {
    }
}
