using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTasks.Core.Models;
using GTasks.Core.Services;

namespace GTasks.UI.ViewModels;

public partial class TaskListViewModel : ObservableObject
{
    private readonly ITaskRepository _taskRepository;

    public TaskList TaskList { get; }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    public string Title => TaskList.Title;
    public int TaskCount => TaskList.TaskCount;
    public int PendingCount => TaskList.PendingCount;
    public bool HasPendingSync => TaskList.SyncState != SyncState.Synced;

    public TaskListViewModel(TaskList taskList, ITaskRepository taskRepository)
    {
        TaskList = taskList;
        _taskRepository = taskRepository;
        EditTitle = taskList.Title;
    }

    [RelayCommand]
    private void StartEditing()
    {
        EditTitle = TaskList.Title;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (!string.IsNullOrWhiteSpace(EditTitle) && EditTitle != TaskList.Title)
        {
            TaskList.Title = EditTitle;
            TaskList.UpdatedAt = DateTimeOffset.Now;
            TaskList.SyncState = SyncState.PendingUpdate;
            await _taskRepository.UpsertTaskListAsync(TaskList);
            OnPropertyChanged(nameof(Title));
        }
        IsEditing = false;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditTitle = TaskList.Title;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        TaskList.SyncState = SyncState.PendingDelete;
        await _taskRepository.DeleteTaskListAsync(TaskList.Id);
    }
}
