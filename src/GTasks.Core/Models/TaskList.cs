using CommunityToolkit.Mvvm.ComponentModel;

namespace GTasks.Core.Models;

/// <summary>
/// Represents a task list, mirroring Google Tasks API structure.
/// </summary>
public partial class TaskList : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _googleId = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _updatedAt;

    [ObservableProperty]
    private SyncState _syncState = SyncState.Synced;

    // Navigation
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    // Computed
    public int TaskCount => Tasks.Count(t => !t.IsDeleted && !t.IsHidden);
    public int CompletedCount => Tasks.Count(t => t.IsCompleted && !t.IsDeleted);
    public int PendingCount => Tasks.Count(t => !t.IsCompleted && !t.IsDeleted && !t.IsHidden);
}
