namespace Persistord.Protection;

/// <summary>The Data Protection purpose strings Persistord derives its protectors from.</summary>
public static class ProtectionPurposes
{
    /// <summary>
    /// The purpose for every <c>[Protected]</c> column. Fixed on purpose: a protector derived from a
    /// different purpose cannot read what this one wrote, so changing this string orphans every
    /// existing ciphertext.
    /// </summary>
    public const string V1 = "Persistord.Protection.v1";
}
