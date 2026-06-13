using Microsoft.EntityFrameworkCore;
using Persistord.Messages.Configurations;

namespace Persistord.Messages;

/// <summary>Model-building extensions that wire the Messages module.</summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the <c>MessageEntity</c> configuration (with owned embeds and relational
    /// attachments/reactions). Call from <c>OnModelCreating</c> after the core configuration.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="filterDeleted">
    /// When true (default), a global query filter hides soft-deleted messages. Use
    /// <c>IgnoreQueryFilters()</c> on a query to include them, or pass false to disable
    /// the filter entirely.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyMessagesModule(this ModelBuilder modelBuilder, bool filterDeleted = true)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new MessageEntityConfiguration(filterDeleted));
        return modelBuilder;
    }
}
