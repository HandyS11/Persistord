using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistord.Core.Conversions;

/// <summary>
/// Nullable counterpart of <see cref="UlongToLongConverter"/> for <see cref="Nullable{T}"/>
/// snowflake properties.
/// </summary>
public sealed class NullableUlongToLongConverter : ValueConverter<ulong?, long?>
{
    /// <summary>Initializes a new instance of the <see cref="NullableUlongToLongConverter"/> class.</summary>
    public NullableUlongToLongConverter()
        : base(
            v => v.HasValue ? unchecked((long)v.Value) : null,
            v => v.HasValue ? unchecked((ulong)v.Value) : null)
    {
    }
}
