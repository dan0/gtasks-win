using CommunityToolkit.Mvvm.ComponentModel;

namespace GTasks.Core.Models;

/// <summary>
/// Represents a task item, mirroring Google Tasks API structure.
/// </summary>
public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _googleId = string.Empty;

    [ObservableProperty]
    private string _taskListId = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private TaskStatus _status = TaskStatus.NeedsAction;

    [ObservableProperty]
    private DateTimeOffset? _due;

    [ObservableProperty]
    private DateTimeOffset? _completed;

    [ObservableProperty]
    private string? _parentId;

    [ObservableProperty]
    private string _position = string.Empty;

    [ObservableProperty]
    private bool _isDeleted;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private DateTimeOffset _updatedAt;

    [ObservableProperty]
    private DateTimeOffset _createdAt;

    [ObservableProperty]
    private SyncState _syncState = SyncState.Synced;

    // Navigation properties
    public TaskItem? Parent { get; set; }
    public ICollection<TaskItem> Subtasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskLink> Links { get; set; } = new List<TaskLink>();

    public bool IsCompleted => Status == TaskStatus.Completed;
    public bool HasSubtasks => Subtasks.Count > 0;
    public bool IsOverdue => Due.HasValue && Due.Value < DateTimeOffset.Now && !IsCompleted;
}

public enum TaskStatus
{
    NeedsAction,
    Completed
}

public enum SyncState
{
    Synced,
    PendingCreate,
    PendingUpdate,
    PendingDelete,
    Conflict
}
