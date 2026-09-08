using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Persistord.Core.Conventions;

/// <summary>
/// Marks every <see cref="ulong"/> or <see cref="Nullable{T}"/> property that is part of a
/// primary key as caller-supplied (<see cref="ValueGenerated.Never"/>). An unsigned 64-bit key
/// is a value the consumer already owns — a Discord snowflake, a Steam64 id, a Rust entity id —
/// never a store-generated identity column. Explicit fluent configuration still wins, because
/// the convention writes at convention precedence.
/// </summary>
public sealed class SnowflakeKeyConvention : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                continue;
            }

            foreach (var property in primaryKey.Properties)
            {
                if (property.ClrType == typeof(ulong) || property.ClrType == typeof(ulong?))
                {
                    property.Builder.ValueGenerated(ValueGenerated.Never);
                }
            }
        }
    }
}
