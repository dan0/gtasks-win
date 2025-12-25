using GTasks.Core.Models;

namespace GTasks.Core.Services;

/// <summary>
/// Manages bidirectional sync between local SQLite and Google Tasks.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Gets whether a sync is currently in progress.
    /// </summary>
    bool IsSyncing { get; }

    /// <summary>
    /// Gets the last sync timestamp.
    /// </summary>
    DateTimeOffset? LastSyncTime { get; }

    /// <summary>
    /// Gets the count of pending local changes.
    /// </summary>
    int PendingChangesCount { get; }

    /// <summary>
    /// Performs a full sync with Google Tasks.
    /// </summary>
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes pending local changes to Google Tasks.
    /// </summary>
    Task<SyncResult> PushChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls latest changes from Google Tasks.
    /// </summary>
    Task<SyncResult> PullChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts background sync with the specified interval.
    /// </summary>
    void StartBackgroundSync(TimeSpan interval);

    /// <summary>
    /// Stops background sync.
    /// </summary>
    void StopBackgroundSync();

    /// <summary>
    /// Event raised when sync status changes.
    /// </summary>
    event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;
}

public class SyncResult
{
    public bool Success { get; init; }
    public int ItemsPushed { get; init; }
    public int ItemsPulled { get; init; }
    public int Conflicts { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class SyncStatusEventArgs : EventArgs
{
    public bool IsSyncing { get; init; }
    public string? Message { get; init; }
    public int Progress { get; init; } // 0-100
}
