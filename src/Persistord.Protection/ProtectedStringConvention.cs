using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Persistord.Protection;

/// <summary>
/// Installs a <see cref="ProtectedStringConverter"/> on every <see cref="string"/> property
/// annotated <c>[Protected]</c>, anywhere in the model — including nested inside an EF complex
/// type. This is the recommended way to wire encryption: register it from
/// <c>ConfigureConventions</c>,
/// <code>
/// protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
/// {
///     base.ConfigureConventions(configurationBuilder);
///     configurationBuilder.Conventions.Add(_ =&gt; new ProtectedStringConvention(dataProtectionProvider));
/// }
/// </code>
/// and it runs at model finalization, after every module and entity configuration has had a chance
/// to add properties — so, unlike <see cref="ProtectionModelBuilderExtensions.ApplyProtection"/>,
/// it cannot be called too early. Keep <c>ApplyProtection</c> for call sites that configure
/// protection explicitly and inline in <c>OnModelCreating</c>; just call it last there, since that
/// route still walks the model as already built.
/// </summary>
/// <param name="dataProtectionProvider">
/// The provider to derive the protector from. Use one instance for the whole application: EF caches
/// the model per context type, so the first context's protector is the one baked into the cached
/// model.
/// </param>
public sealed class ProtectedStringConvention(IDataProtectionProvider dataProtectionProvider) : IModelFinalizingConvention
{
    private readonly ProtectedStringConverter _converter = BuildConverter(dataProtectionProvider);

    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var property in ProtectedPropertyWalker.FindProtectedProperties(entityType).ToList())
            {
                ((IConventionProperty)property).Builder.HasConversion(_converter);
            }
        }
    }

    private static ProtectedStringConverter BuildConverter(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        return new ProtectedStringConverter(dataProtectionProvider.CreateProtector(ProtectionPurposes.V1));
    }
}
