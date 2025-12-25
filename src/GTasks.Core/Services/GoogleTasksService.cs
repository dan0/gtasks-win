using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using GTasks.Core.Models;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task;
using GoogleTaskList = Google.Apis.Tasks.v1.Data.TaskList;
using TaskStatus = GTasks.Core.Models.TaskStatus;

namespace GTasks.Core.Services;

public class GoogleTasksService : IGoogleTasksService
{
    private readonly AuthService _authService;
    private TasksService? _tasksService;

    public GoogleTasksService(IAuthService authService)
    {
        _authService = (AuthService)authService;
    }

    private TasksService GetService()
    {
        if (_tasksService == null)
        {
            var credential = _authService.GetCredential()
                ?? throw new InvalidOperationException("Not authenticated");

            _tasksService = new TasksService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "GTasks for Windows"
            });
        }
        return _tasksService;
    }

    #region Task Lists

    public async Task<IReadOnlyList<Models.TaskList>> GetTaskListsAsync(CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var request = service.Tasklists.List();
        var response = await request.ExecuteAsync(cancellationToken);

        return response.Items?.Select(MapToTaskList).ToList()
            ?? new List<Models.TaskList>();
    }

    public async System.Threading.Tasks.Task<Models.TaskList> CreateTaskListAsync(string title, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var taskList = new GoogleTaskList { Title = title };
        var created = await service.Tasklists.Insert(taskList).ExecuteAsync(cancellationToken);
        return MapToTaskList(created);
    }

    public async System.Threading.Tasks.Task<Models.TaskList> UpdateTaskListAsync(string id, string title, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var taskList = new GoogleTaskList { Title = title };
        var updated = await service.Tasklists.Update(taskList, id).ExecuteAsync(cancellationToken);
        return MapToTaskList(updated);
    }

    public async System.Threading.Tasks.Task DeleteTaskListAsync(string id, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        await service.Tasklists.Delete(id).ExecuteAsync(cancellationToken);
    }

    #endregion

    #region Tasks

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync(
        string taskListId,
        bool includeCompleted = true,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var request = service.Tasks.List(taskListId);
        request.ShowCompleted = includeCompleted;
        request.ShowHidden = includeHidden;
        request.MaxResults = 100;

        var allTasks = new List<TaskItem>();
        string? pageToken = null;

        do
        {
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(cancellationToken);

            if (response.Items != null)
            {
                allTasks.AddRange(response.Items.Select(t => MapToTaskItem(t, taskListId)));
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return allTasks;
    }

    public async Task<TaskItem> GetTaskAsync(string taskListId, string taskId, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var task = await service.Tasks.Get(taskListId, taskId).ExecuteAsync(cancellationToken);
        return MapToTaskItem(task, taskListId);
    }

    public async Task<TaskItem> CreateTaskAsync(string taskListId, TaskItem task, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var googleTask = MapToGoogleTask(task);

        var request = service.Tasks.Insert(googleTask, taskListId);
        if (!string.IsNullOrEmpty(task.ParentId))
        {
            request.Parent = task.ParentId;
        }

        var created = await request.ExecuteAsync(cancellationToken);
        return MapToTaskItem(created, taskListId);
    }

    public async Task<TaskItem> UpdateTaskAsync(string taskListId, TaskItem task, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var googleTask = MapToGoogleTask(task);
        googleTask.Id = task.GoogleId;

        var updated = await service.Tasks.Update(googleTask, taskListId, task.GoogleId)
            .ExecuteAsync(cancellationToken);
        return MapToTaskItem(updated, taskListId);
    }

    public async System.Threading.Tasks.Task DeleteTaskAsync(string taskListId, string taskId, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        await service.Tasks.Delete(taskListId, taskId).ExecuteAsync(cancellationToken);
    }

    public async Task<TaskItem> MoveTaskAsync(
        string taskListId,
        string taskId,
        string? parentId = null,
        string? previousId = null,
        CancellationToken cancellationToken = default)
    {
        var service = GetService();
        var request = service.Tasks.Move(taskListId, taskId);

        if (!string.IsNullOrEmpty(parentId))
            request.Parent = parentId;
        if (!string.IsNullOrEmpty(previousId))
            request.Previous = previousId;

        var moved = await request.ExecuteAsync(cancellationToken);
        return MapToTaskItem(moved, taskListId);
    }

    public async System.Threading.Tasks.Task ClearCompletedAsync(string taskListId, CancellationToken cancellationToken = default)
    {
        var service = GetService();
        await service.Tasks.Clear(taskListId).ExecuteAsync(cancellationToken);
    }

    #endregion

    #region Mapping

    private static Models.TaskList MapToTaskList(GoogleTaskList googleList)
    {
        return new Models.TaskList
        {
            Id = googleList.Id,
            GoogleId = googleList.Id,
            Title = googleList.Title ?? string.Empty,
            UpdatedAt = DateTimeOffset.TryParse(googleList.Updated, out var updated)
                ? updated
                : DateTimeOffset.Now,
            SyncState = SyncState.Synced
        };
    }

    private static TaskItem MapToTaskItem(GoogleTask googleTask, string taskListId)
    {
        var item = new TaskItem
        {
            Id = googleTask.Id,
            GoogleId = googleTask.Id,
            TaskListId = taskListId,
            Title = googleTask.Title ?? string.Empty,
            Notes = googleTask.Notes ?? string.Empty,
            Status = googleTask.Status == "completed" ? TaskStatus.Completed : TaskStatus.NeedsAction,
            ParentId = googleTask.Parent,
            Position = googleTask.Position ?? string.Empty,
            IsDeleted = googleTask.Deleted ?? false,
            IsHidden = googleTask.Hidden ?? false,
            SyncState = SyncState.Synced
        };

        if (DateTimeOffset.TryParse(googleTask.Due, out var due))
            item.Due = due;
        if (DateTimeOffset.TryParse(googleTask.Completed, out var completed))
            item.Completed = completed;
        if (DateTimeOffset.TryParse(googleTask.Updated, out var updated))
            item.UpdatedAt = updated;

        // Map links
        if (googleTask.Links != null)
        {
            foreach (var link in googleTask.Links)
            {
                item.Links.Add(new TaskLink
                {
                    TaskId = item.Id,
                    Type = link.Type ?? string.Empty,
                    Description = link.Description ?? string.Empty,
                    Url = link.Link ?? string.Empty
                });
            }
        }

        return item;
    }

    private static GoogleTask MapToGoogleTask(TaskItem item)
    {
        return new GoogleTask
        {
            Title = item.Title,
            Notes = string.IsNullOrEmpty(item.Notes) ? null : item.Notes,
            Status = item.Status == TaskStatus.Completed ? "completed" : "needsAction",
            Due = item.Due?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
        };
    }

    #endregion
}
