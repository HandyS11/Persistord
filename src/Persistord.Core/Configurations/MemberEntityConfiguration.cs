using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Persistord.Core.Entities;

namespace Persistord.Core.Configurations;

/// <summary>EF Core configuration for <see cref="MemberEntity"/> with its composite key.</summary>
public sealed class MemberEntityConfiguration : IEntityTypeConfiguration<MemberEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemberEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(m => new
        {
            m.GuildId, m.UserId
        });
    }
}
