namespace Persistord.History.Entities;

/// <summary>The kind of change a history row records.</summary>
public enum HistoryChangeType
{
    /// <summary>The message was created.</summary>
    Created = 0,

    /// <summary>The message was edited.</summary>
    Edited = 1,

    /// <summary>The message was deleted.</summary>
    Deleted = 2,
}
