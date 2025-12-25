using GTasks.Core.Models;

namespace GTasks.Core.Services;

/// <summary>
/// Wraps the Google Tasks API with strongly-typed methods.
/// </summary>
public interface IGoogleTasksService
{
    // Task Lists
    Task<IReadOnlyList<TaskList>> GetTaskListsAsync(CancellationToken cancellationToken = default);
    Task<TaskList> CreateTaskListAsync(string title, CancellationToken cancellationToken = default);
    Task<TaskList> UpdateTaskListAsync(string id, string title, CancellationToken cancellationToken = default);
    Task DeleteTaskListAsync(string id, CancellationToken cancellationToken = default);

    // Tasks
    Task<IReadOnlyList<TaskItem>> GetTasksAsync(string taskListId, bool includeCompleted = true, bool includeHidden = false, CancellationToken cancellationToken = default);
    Task<TaskItem> GetTaskAsync(string taskListId, string taskId, CancellationToken cancellationToken = default);
    Task<TaskItem> CreateTaskAsync(string taskListId, TaskItem task, CancellationToken cancellationToken = default);
    Task<TaskItem> UpdateTaskAsync(string taskListId, TaskItem task, CancellationToken cancellationToken = default);
    Task DeleteTaskAsync(string taskListId, string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a task to a new position, optionally under a new parent.
    /// </summary>
    Task<TaskItem> MoveTaskAsync(string taskListId, string taskId, string? parentId = null, string? previousId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all completed tasks from a task list.
    /// </summary>
    Task ClearCompletedAsync(string taskListId, CancellationToken cancellationToken = default);
}
