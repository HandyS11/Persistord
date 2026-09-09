using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Persistord.Core.Abstractions;

namespace Persistord.Core.Conventions;

/// <summary>
/// Gives every <see cref="IGuildScoped"/> entity an index on <c>GuildId</c>, which is the column
/// every tenant-scoped query filters on. Skipped when the primary key or an existing index
/// already leads with <c>GuildId</c> — that index already serves the same lookups.
/// </summary>
public sealed class GuildScopeConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IGuildScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var guildId = entityType.FindProperty(nameof(IGuildScoped.GuildId));
            if (guildId is null || LeadsAnIndex(entityType, guildId.Name))
            {
                continue;
            }

            entityType.Builder.HasIndex([guildId.Name]);
        }
    }

    private static bool LeadsAnIndex(IConventionEntityType entityType, string guildId)
    {
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is not null && string.Equals(primaryKey.Properties[0].Name, guildId, StringComparison.Ordinal))
        {
            return true;
        }

        return entityType.GetIndexes()
            .Any(index => string.Equals(index.Properties[0].Name, guildId, StringComparison.Ordinal));
    }
}
