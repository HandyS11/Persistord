namespace Persistord.Core.Abstractions;

/// <summary>
/// Marks a <see cref="string"/> property as a secret that belongs encrypted at rest. The attribute
/// is inert on its own: reference <c>Persistord.Protection</c> and call
/// <c>modelBuilder.ApplyProtection(provider)</c> to install the value converter that encrypts it.
/// Without that call the property is stored as plaintext.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtectedAttribute : Attribute;
