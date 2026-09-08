namespace Persistord.Core.Abstractions;

/// <summary>An entity whose insert time Persistord stamps.</summary>
public interface ICreatedAt
{
    /// <summary>
    /// When the row was first written. <c>TimestampInterceptor</c> fills it on insert unless the
    /// caller already set it, so a backfilled row keeps its real creation time.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }
}
