using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Core.Abstractions;

namespace Persistord.Protection;

/// <summary>Model-building extensions that encrypt annotated columns.</summary>
public static class ProtectionModelBuilderExtensions
{
    /// <summary>
    /// Installs a <see cref="ProtectedStringConverter"/> on every <see cref="string"/> property
    /// annotated <see cref="ProtectedAttribute"/>, anywhere in the model. Call it last in
    /// <c>OnModelCreating</c>, after the module and entity configurations that create those
    /// properties: it walks the model as already built, so a property configured afterwards is not
    /// seen. Three declaration sites are honoured: the attribute on the property itself, on a base
    /// class it inherits from (ordinary .NET attribute inheritance), or on an interface member the
    /// entity's property implements — resolved precisely through the interface map, not by name, so
    /// an unrelated interface that happens to declare a same-named property cannot pull a property
    /// into encryption by accident.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="dataProtectionProvider">
    /// The provider to derive the protector from. Use one instance for the whole application: EF
    /// caches the model per context type, so the first context's protector is the one baked into the
    /// cached model. Tests that need different key rings must also replace
    /// <c>IModelCacheKeyFactory</c> — <c>Persistord.Testing</c>'s fixtures already do.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyProtection(
        this ModelBuilder modelBuilder,
        IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        var converter = new ProtectedStringConverter(
            dataProtectionProvider.CreateProtector(ProtectionPurposes.V1));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(p => IsProtected(entityType, p)).ToList())
            {
                property.SetValueConverter(converter);
            }
        }

        return modelBuilder;
    }

    private static bool IsProtected(IReadOnlyEntityType entityType, IMutableProperty property)
    {
        if (property.ClrType != typeof(string) || property.PropertyInfo is not { } propertyInfo)
        {
            return false;
        }

        return propertyInfo.GetCustomAttribute<ProtectedAttribute>() is not null
               || ImplementsProtectedInterfaceMember(entityType.ClrType, propertyInfo);
    }

    /// <summary>
    /// .NET does not propagate attributes across an interface-implementation boundary the way it
    /// does across base-class inheritance, so a property whose <see cref="ProtectedAttribute"/>
    /// lives only on an interface member it implements is invisible to
    /// <see cref="CustomAttributeExtensions.GetCustomAttribute{T}(MemberInfo)"/> above. This walks
    /// the entity's interface map to find the interface property the accessor implements — a
    /// precise match on the accessor method, not a name match, so an unrelated same-named property
    /// on some other interface cannot be mistaken for this one.
    /// </summary>
    /// <param name="entityClrType">The entity's concrete CLR type, to resolve interface maps from.</param>
    /// <param name="propertyInfo">The property to check.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="propertyInfo"/> implements an interface property
    /// annotated <see cref="ProtectedAttribute"/>; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool ImplementsProtectedInterfaceMember(Type entityClrType, PropertyInfo propertyInfo)
    {
        if (propertyInfo.GetMethod is not { } getter)
        {
            return false;
        }

        foreach (var interfaceType in entityClrType.GetInterfaces())
        {
            var map = entityClrType.GetInterfaceMap(interfaceType);
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
