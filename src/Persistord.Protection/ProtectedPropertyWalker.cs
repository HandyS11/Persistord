using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;

namespace Persistord.Protection;

/// <summary>
/// Finds every <see cref="string"/> property annotated <see cref="ProtectedAttribute"/> in a
/// structural type, recursing into EF complex types so a <c>[Protected]</c> member nested behind
/// <c>ComplexProperty</c> is found exactly like one declared directly on an entity. Shared by
/// <see cref="ProtectionModelBuilderExtensions.ApplyProtection"/> and
/// <see cref="ProtectedStringConvention"/> so both entry points cover the same ground.
/// </summary>
internal static class ProtectedPropertyWalker
{
    /// <summary>
    /// Walks <paramref name="typeBase"/> and, recursively, every EF complex type reachable from it,
    /// yielding each <see cref="string"/> property annotated <see cref="ProtectedAttribute"/>.
    /// </summary>
    /// <param name="typeBase">The entity type (or, during recursion, complex type) to walk.</param>
    /// <returns>
    /// The matching properties. Each is the same underlying metadata object <paramref name="typeBase"/>
    /// was obtained from, so a caller holding an <see cref="IMutableEntityType"/> can cast a result
    /// back to <see cref="IMutableProperty"/>, and a caller holding an <see cref="IConventionEntityType"/>
    /// can cast it back to <see cref="IConventionProperty"/> — both views describe the same object
    /// while the model is still being built or finalized.
    /// </returns>
    internal static IEnumerable<IReadOnlyProperty> FindProtectedProperties(IReadOnlyTypeBase typeBase)
    {
        foreach (var property in typeBase.GetProperties().Where(IsProtected))
        {
            yield return property;
        }

        foreach (var complexProperty in typeBase.GetComplexProperties())
        {
            foreach (var nested in FindProtectedProperties(complexProperty.ComplexType))
            {
                yield return nested;
            }
        }
    }

    private static bool IsProtected(IReadOnlyProperty property)
    {
        if (property.ClrType != typeof(string) || property.PropertyInfo is not { } propertyInfo)
        {
            return false;
        }

        return propertyInfo.GetCustomAttribute<ProtectedAttribute>() is not null
               || ImplementsProtectedInterfaceMember(property.DeclaringType.ClrType, propertyInfo);
    }

    /// <summary>
    /// .NET does not propagate attributes across an interface-implementation boundary the way it
    /// does across base-class inheritance, so a property whose <see cref="ProtectedAttribute"/>
    /// lives only on an interface member it implements is invisible to
    /// <see cref="CustomAttributeExtensions.GetCustomAttribute{T}(MemberInfo)"/> above. This walks
    /// the declaring type's interface map to find the interface property the accessor implements —
    /// a precise match on the accessor method, not a name match, so an unrelated same-named property
    /// on some other interface cannot be mistaken for this one.
    /// </summary>
    /// <param name="declaringClrType">
    /// The CLR type the property is declared on — an entity's concrete type, or a complex type's
    /// value-object type — to resolve interface maps from.
    /// </param>
    /// <param name="propertyInfo">The property to check.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="propertyInfo"/> implements an interface property
    /// annotated <see cref="ProtectedAttribute"/>; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool ImplementsProtectedInterfaceMember(Type declaringClrType, PropertyInfo propertyInfo)
    {
        if (propertyInfo.GetMethod is not { } getter)
        {
            return false;
        }

        foreach (var interfaceType in declaringClrType.GetInterfaces())
        {
            var map = declaringClrType.GetInterfaceMap(interfaceType);
            var index = Array.IndexOf(map.TargetMethods, getter);
            if (index < 0)
            {
                continue;
            }

            var interfaceGetter = map.InterfaceMethods[index];
            var interfaceProperty = Array.Find(interfaceType.GetProperties(), p => p.GetMethod == interfaceGetter);

            if (interfaceProperty?.GetCustomAttribute<ProtectedAttribute>() is not null)
            {
                return true;
            }
        }

        return false;
    }
}
