namespace Persistord.Core.Abstractions;

/// <summary>
/// Marks a <see cref="string"/> property as a secret that belongs encrypted at rest. The attribute
/// is inert on its own: reference <c>Persistord.Protection</c> and call
/// <c>modelBuilder.ApplyProtection(provider)</c> to install the value converter that encrypts it.
/// Without that call the property is stored as plaintext. It is silently a no-op on a non-<see
/// cref="string"/> property: <c>ApplyProtection</c> only ever considers <see cref="string"/>
/// properties, so annotating anything else changes nothing, with no error or warning.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtectedAttribute : Attribute;
