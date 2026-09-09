namespace Persistord.Core.Abstractions;

/// <summary>
/// Marks a <see cref="string"/> property as a secret that belongs encrypted at rest. The attribute
/// is inert on its own: reference <c>Persistord.Protection</c> and either register
/// <c>ProtectedStringConvention</c> from <c>ConfigureConventions</c> (the recommended route — it
/// runs at model finalization, so it cannot be applied too early) or call
/// <c>modelBuilder.ApplyProtection(provider)</c> last in <c>OnModelCreating</c> to install the value
/// converter that encrypts it. Without one of those, the property is stored as plaintext. It is
/// silently a no-op on a non-<see cref="string"/> property: both routes only ever consider
/// <see cref="string"/> properties, so annotating anything else changes nothing, with no error or
/// warning.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtectedAttribute : Attribute;
