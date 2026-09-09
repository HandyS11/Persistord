namespace Persistord.Core.Abstractions;

/// <summary>An entity whose last-write time Persistord stamps.</summary>
public interface IUpdatedAt
{
    /// <summary>
    /// When the row was last written. <c>TimestampInterceptor</c> fills it on insert and on every
    /// update. A save that changes nothing does not touch it.
    /// </summary>
    DateTimeOffset UpdatedAt { get; set; }
}
