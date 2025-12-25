using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTasks.Core.Models;
using GTasks.Core.Services;
using TaskStatus = GTasks.Core.Models.TaskStatus;

namespace GTasks.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ISyncService _syncService;
    private readonly ITaskRepository _taskRepository;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string _syncStatusMessage = "Ready";

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private TaskListViewModel? _selectedTaskList;

    [ObservableProperty]
    private TaskItem? _selectedTask;

    [ObservableProperty]
    private string _currentFilter = "All";

    public ObservableCollection<TaskListViewModel> TaskLists { get; } = new();
    public ObservableCollection<TaskItem> FilteredTasks { get; } = new();
    public ObservableCollection<TaskItem> SearchResults { get; } = new();

    public MainViewModel(
        IAuthService authService,
        ISyncService syncService,
        ITaskRepository taskRepository)
    {
        _authService = authService;
        _syncService = syncService;
        _taskRepository = taskRepository;

        _authService.AuthenticationChanged += OnAuthenticationChanged;
        _syncService.SyncStatusChanged += OnSyncStatusChanged;

        IsAuthenticated = _authService.IsAuthenticated;
    }

    public async Task InitializeAsync()
    {
        // Try to restore authentication
        if (await _authService.TryRestoreAsync())
        {
            await LoadDataAsync();
            _syncService.StartBackgroundSync(TimeSpan.FromMinutes(5));
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (await _authService.LoginAsync())
        {
            await SyncAsync();
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _syncService.StopBackgroundSync();
        await _authService.LogoutAsync();
        TaskLists.Clear();
        FilteredTasks.Clear();
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        await _syncService.SyncAsync();
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task CreateTaskListAsync(string title)
    {
        var taskList = new TaskList
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            SyncState = SyncState.PendingCreate
        };

        await _taskRepository.UpsertTaskListAsync(taskList);
        TaskLists.Add(new TaskListViewModel(taskList, _taskRepository));
    }

    [RelayCommand]
    private async Task CreateTaskAsync(string title)
    {
        if (SelectedTaskList == null) return;

        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            TaskListId = SelectedTaskList.TaskList.Id,
            Title = title,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
            SyncState = SyncState.PendingCreate
        };

        // Parse natural language (simple version)
        ParseNaturalLanguage(task, ref title);
        task.Title = title.Trim();

        await _taskRepository.UpsertTaskAsync(task);
        await LoadTasksForSelectedListAsync();
    }

    [RelayCommand]
    private async Task ToggleTaskCompletionAsync(TaskItem task)
    {
        task.Status = task.IsCompleted ? TaskStatus.NeedsAction : TaskStatus.Completed;
        task.Completed = task.IsCompleted ? DateTimeOffset.Now : null;
        task.UpdatedAt = DateTimeOffset.Now;
        task.SyncState = SyncState.PendingUpdate;

        await _taskRepository.UpsertTaskAsync(task);
    }

    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItem task)
    {
        task.IsDeleted = true;
        task.SyncState = SyncState.PendingDelete;
        await _taskRepository.UpsertTaskAsync(task);
        FilteredTasks.Remove(task);
    }

    [RelayCommand]
    private void OpenCommandPalette()
    {
        IsCommandPaletteOpen = true;
    }

    [RelayCommand]
    private void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private async Task SearchAsync(string query)
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(query)) return;

        var results = await _taskRepository.SearchTasksAsync(query);
        foreach (var result in results)
        {
            SearchResults.Add(result);
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync(string filterName)
    {
        CurrentFilter = filterName;
        var today = DateTimeOffset.Now.Date;
        var tomorrow = today.AddDays(1);

        var filter = filterName switch
        {
            "Today" => new TaskFilter { DueAfter = today, DueBefore = tomorrow },
            "Tomorrow" => new TaskFilter { DueAfter = tomorrow, DueBefore = tomorrow.AddDays(1) },
            "Overdue" => new TaskFilter { IncludeOverdue = true },
            "Week" => new TaskFilter { DueBefore = DateTimeOffset.Now.AddDays(7) },
            "No Date" => new TaskFilter { NoDueDate = true },
            "Completed" => new TaskFilter { IsCompleted = true },
            _ => new TaskFilter { TaskListId = SelectedTaskList?.TaskList.Id }
        };

        FilteredTasks.Clear();
        var tasks = await _taskRepository.GetTasksByFilterAsync(filter);
        foreach (var task in tasks)
        {
            FilteredTasks.Add(task);
        }
    }

    private async Task LoadDataAsync()
    {
        TaskLists.Clear();
        var lists = await _taskRepository.GetTaskListsAsync();
        foreach (var list in lists)
        {
            TaskLists.Add(new TaskListViewModel(list, _taskRepository));
        }

        if (TaskLists.Any())
        {
            SelectedTaskList = TaskLists.First();
            await LoadTasksForSelectedListAsync();
        }
    }

    private async Task LoadTasksForSelectedListAsync()
    {
        FilteredTasks.Clear();
        if (SelectedTaskList == null) return;

        var tasks = await _taskRepository.GetTasksAsync(SelectedTaskList.TaskList.Id);

        // Build tree structure
        var rootTasks = tasks.Where(t => string.IsNullOrEmpty(t.ParentId)).OrderBy(t => t.Position);
        foreach (var task in rootTasks)
        {
            FilteredTasks.Add(task);
            AddSubtasksRecursive(task, tasks);
        }
    }

    private void AddSubtasksRecursive(TaskItem parent, IReadOnlyList<TaskItem> allTasks)
    {
        var subtasks = allTasks.Where(t => t.ParentId == parent.Id).OrderBy(t => t.Position);
        foreach (var subtask in subtasks)
        {
            FilteredTasks.Add(subtask);
            AddSubtasksRecursive(subtask, allTasks);
        }
    }

    private void ParseNaturalLanguage(TaskItem task, ref string title)
    {
        // Parse "tomorrow"
        if (title.Contains(" tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            task.Due = DateTimeOffset.Now.AddDays(1).Date;
            title = title.Replace(" tomorrow", "", StringComparison.OrdinalIgnoreCase);
        }
        // Parse "today"
        else if (title.Contains(" today", StringComparison.OrdinalIgnoreCase))
        {
            task.Due = DateTimeOffset.Now.Date;
            title = title.Replace(" today", "", StringComparison.OrdinalIgnoreCase);
        }
    }

    partial void OnSelectedTaskListChanged(TaskListViewModel? value)
    {
        if (value != null)
        {
            _ = LoadTasksForSelectedListAsync();
        }
    }

    private void OnAuthenticationChanged(object? sender, bool isAuthenticated)
    {
        IsAuthenticated = isAuthenticated;
    }

    private void OnSyncStatusChanged(object? sender, SyncStatusEventArgs e)
    {
        IsSyncing = e.IsSyncing;
        SyncStatusMessage = e.Message ?? "Ready";
    }
}
