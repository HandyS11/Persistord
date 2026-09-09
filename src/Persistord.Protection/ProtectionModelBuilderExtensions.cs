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
    /// annotated <see cref="ProtectedAttribute"/>, anywhere in the model — including one nested
    /// inside an EF complex type (<c>ComplexProperty</c>), recursively. Call it last in
    /// <c>OnModelCreating</c>, after the module and entity configurations that create those
    /// properties: it walks the model as already built, so a property configured afterwards is not
    /// seen. Prefer <see cref="ProtectedStringConvention"/> instead when that ordering requirement is
    /// a concern: registered from <c>ConfigureConventions</c>, it runs at model finalization, so it
    /// cannot be called too early. Three declaration sites are honoured here as well as there: the
    /// attribute on the property itself, on a base class it inherits from (ordinary .NET attribute
    /// inheritance), or on an interface member the property implements — resolved precisely through
    /// the interface map, not by name, so an unrelated interface that happens to declare a
    /// same-named property cannot pull a property into encryption by accident.
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
            foreach (var property in ProtectedPropertyWalker.FindProtectedProperties(entityType).ToList())
            {
                ((IMutableProperty)property).SetValueConverter(converter);
            }
        }

        return modelBuilder;
    }
}
