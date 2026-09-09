using System.Linq.Expressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistord.Protection;

/// <summary>
/// Encrypts a string on the way to the database and decrypts it on the way back. A failed decrypt
/// surfaces as <see cref="System.Security.Cryptography.CryptographicException"/> while EF
/// materializes the entity; the usual cause is a key ring that was lost or rotated away.
/// </summary>
/// <param name="protector">
/// The protector to use, normally created for <see cref="ProtectionPurposes.V1"/>.
/// </param>
public sealed class ProtectedStringConverter(IDataProtector protector)
    : ValueConverter<string, string>(ToProvider(protector), FromProvider(protector))
{
    /// <summary>Built in a static helper so the null check runs before the base constructor captures it.</summary>
    /// <param name="protector">The protector to guard and capture.</param>
    /// <returns>An expression that protects a value through <paramref name="protector"/>.</returns>
    private static Expression<Func<string, string>> ToProvider(IDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return value => protector.Protect(value);
    }

    /// <summary>Built in a static helper for symmetry with <see cref="ToProvider"/>.</summary>
    /// <param name="protector">The protector to capture.</param>
    /// <returns>An expression that unprotects a value through <paramref name="protector"/>.</returns>
    private static Expression<Func<string, string>> FromProvider(IDataProtector protector) =>
        value => protector.Unprotect(value);
}
