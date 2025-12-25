using GTasks.Core.Models;

namespace GTasks.Core.Services;

/// <summary>
/// Repository for local task data persistence.
/// </summary>
public interface ITaskRepository
{
    // Task Lists
    Task<IReadOnlyList<TaskList>> GetTaskListsAsync();
    Task<TaskList?> GetTaskListAsync(string id);
    Task<TaskList> UpsertTaskListAsync(TaskList taskList);
    Task DeleteTaskListAsync(string id);

    // Tasks
    Task<IReadOnlyList<TaskItem>> GetTasksAsync(string taskListId);
    Task<TaskItem?> GetTaskAsync(string id);
    Task<TaskItem> UpsertTaskAsync(TaskItem task);
    Task DeleteTaskAsync(string id);

    // Pending Changes
    Task<IReadOnlyList<PendingChange>> GetPendingChangesAsync();
    Task<int> GetPendingChangesCountAsync();
    Task AddPendingChangeAsync(PendingChange change);
    Task UpdatePendingChangeAsync(PendingChange change);
    Task RemovePendingChangeAsync(int id);

    // Sync Support
    Task UpdateGoogleIdAsync(string entityType, string localId, string googleId);
    Task MarkOrphanedItemsAsync(IReadOnlyList<string> validGoogleIds);

    // Search
    Task<IReadOnlyList<TaskItem>> SearchTasksAsync(string query);
    Task<IReadOnlyList<TaskItem>> GetTasksByFilterAsync(TaskFilter filter);
}

public class TaskFilter
{
    public DateTimeOffset? DueAfter { get; set; }
    public DateTimeOffset? DueBefore { get; set; }
    public bool? IsCompleted { get; set; }
    public bool IncludeOverdue { get; set; }
    public bool TodayOnly { get; set; }
    public bool NoDueDate { get; set; }
    public string? TaskListId { get; set; }
}
