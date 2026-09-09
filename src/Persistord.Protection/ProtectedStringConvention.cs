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
/// <remarks>
/// Applies the converter at EF's <c>DataAnnotation</c> configuration-source precedence, because
/// <c>[Protected]</c> genuinely is a data annotation. This has one consequence worth knowing: an
/// explicit fluent <c>HasConversion(...)</c> the consumer configured on the same property, in
/// <c>OnModelCreating</c>, is <c>Explicit</c> precedence and wins over this convention — the
/// property keeps the consumer's converter, unprotected. <see cref="ProtectionModelBuilderExtensions.ApplyProtection"/>
/// does not have this gap: it calls the raw mutable setter, which overwrites unconditionally
/// regardless of what was configured before it, provided it is called last. A <c>[Protected]</c>
/// property that also needs its own custom conversion is a narrow enough case that neither route
/// tries to merge the two; pick <c>ApplyProtection</c> if you need protection to win regardless.
/// </remarks>
/// <param name="dataProtectionProvider">
/// The provider to derive the protector from. Use one instance for the whole application: EF caches
/// the model per context type, so the first context's protector is the one baked into the cached
/// model.
/// </param>
public sealed class ProtectedStringConvention(IDataProtectionProvider dataProtectionProvider)
    : IModelFinalizingConvention
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
                // fromDataAnnotation: true, because [Protected] genuinely is a data annotation:
                // this must not silently lose to a lower-precedence Convention-source call, and
                // it must not silently overwrite an explicit fluent HasConversion(...) the
                // consumer wrote for the same property either. EF's own precedence order —
                // Convention < DataAnnotation < Explicit — makes that trade-off for us. See
                // docs/articles/protection.md's "Precedence" section for the one case where this
                // still disagrees with ApplyProtection.
                ((IConventionProperty)property).Builder.HasConversion(_converter, fromDataAnnotation: true);
            }
        }
    }

    private static ProtectedStringConverter BuildConverter(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        return new ProtectedStringConverter(dataProtectionProvider.CreateProtector(ProtectionPurposes.V1));
    }
}
