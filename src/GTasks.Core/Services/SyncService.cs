using System.Text.Json;
using GTasks.Core.Models;

namespace GTasks.Core.Services;

public class SyncService : ISyncService
{
    private readonly IGoogleTasksService _googleTasksService;
    private readonly ITaskRepository _taskRepository;
    private Timer? _syncTimer;
    private bool _isSyncing;

    public bool IsSyncing => _isSyncing;
    public DateTimeOffset? LastSyncTime { get; private set; }
    public int PendingChangesCount => _taskRepository.GetPendingChangesCountAsync().GetAwaiter().GetResult();

    public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;

    public SyncService(IGoogleTasksService googleTasksService, ITaskRepository taskRepository)
    {
        _googleTasksService = googleTasksService;
        _taskRepository = taskRepository;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (_isSyncing)
            return new SyncResult { Success = false, Errors = { "Sync already in progress" } };

        _isSyncing = true;
        RaiseSyncStatus(true, "Starting sync...", 0);

        try
        {
            // Push local changes first
            RaiseSyncStatus(true, "Pushing local changes...", 25);
            var pushResult = await PushChangesAsync(cancellationToken);

            // Then pull remote changes
            RaiseSyncStatus(true, "Pulling remote changes...", 50);
            var pullResult = await PullChangesAsync(cancellationToken);

            LastSyncTime = DateTimeOffset.Now;
            RaiseSyncStatus(false, "Sync complete", 100);

            return new SyncResult
            {
                Success = pushResult.Success && pullResult.Success,
                ItemsPushed = pushResult.ItemsPushed,
                ItemsPulled = pullResult.ItemsPulled,
                Conflicts = pushResult.Conflicts + pullResult.Conflicts,
                Errors = pushResult.Errors.Concat(pullResult.Errors).ToList()
            };
        }
        catch (Exception ex)
        {
            RaiseSyncStatus(false, $"Sync failed: {ex.Message}", 0);
            return new SyncResult { Success = false, Errors = { ex.Message } };
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public async Task<SyncResult> PushChangesAsync(CancellationToken cancellationToken = default)
    {
        var pendingChanges = await _taskRepository.GetPendingChangesAsync();
        var errors = new List<string>();
        int pushed = 0;

        foreach (var change in pendingChanges)
        {
            try
            {
                await ProcessChangeAsync(change, cancellationToken);
                await _taskRepository.RemovePendingChangeAsync(change.Id);
                pushed++;
            }
            catch (Exception ex)
            {
                change.Attempts++;
                change.ErrorMessage = ex.Message;
                await _taskRepository.UpdatePendingChangeAsync(change);
                errors.Add($"Failed to sync {change.EntityType} {change.EntityId}: {ex.Message}");
            }
        }

        return new SyncResult { Success = errors.Count == 0, ItemsPushed = pushed, Errors = errors };
    }

    public async Task<SyncResult> PullChangesAsync(CancellationToken cancellationToken = default)
    {
        int pulled = 0;
        var errors = new List<string>();

        try
        {
            // Get all task lists from Google
            var remoteLists = await _googleTasksService.GetTaskListsAsync(cancellationToken);

            foreach (var remoteList in remoteLists)
            {
                // Upsert task list
                await _taskRepository.UpsertTaskListAsync(remoteList);
                pulled++;

                // Get tasks for this list
                var remoteTasks = await _googleTasksService.GetTasksAsync(
                    remoteList.GoogleId,
                    includeCompleted: true,
                    includeHidden: true,
                    cancellationToken);

                foreach (var remoteTask in remoteTasks)
                {
                    await _taskRepository.UpsertTaskAsync(remoteTask);
                    pulled++;
                }
            }

            // Mark local-only items as needing sync
            await _taskRepository.MarkOrphanedItemsAsync(remoteLists.Select(l => l.GoogleId).ToList());
        }
        catch (Exception ex)
        {
            errors.Add($"Pull failed: {ex.Message}");
        }

        return new SyncResult { Success = errors.Count == 0, ItemsPulled = pulled, Errors = errors };
    }

    private async Task ProcessChangeAsync(PendingChange change, CancellationToken cancellationToken)
    {
        switch (change.EntityType)
        {
            case "TaskList":
                await ProcessTaskListChangeAsync(change, cancellationToken);
                break;
            case "Task":
                await ProcessTaskChangeAsync(change, cancellationToken);
                break;
        }
    }

    private async Task ProcessTaskListChangeAsync(PendingChange change, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TaskList>(change.Payload);
        if (payload == null) return;

        switch (change.Operation)
        {
            case ChangeOperation.Create:
                var created = await _googleTasksService.CreateTaskListAsync(payload.Title, cancellationToken);
                await _taskRepository.UpdateGoogleIdAsync("TaskList", change.EntityId, created.GoogleId);
                break;
            case ChangeOperation.Update:
                await _googleTasksService.UpdateTaskListAsync(payload.GoogleId, payload.Title, cancellationToken);
                break;
            case ChangeOperation.Delete:
                await _googleTasksService.DeleteTaskListAsync(payload.GoogleId, cancellationToken);
                break;
        }
    }

    private async Task ProcessTaskChangeAsync(PendingChange change, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TaskItem>(change.Payload);
        if (payload == null) return;

        switch (change.Operation)
        {
            case ChangeOperation.Create:
                var created = await _googleTasksService.CreateTaskAsync(payload.TaskListId, payload, cancellationToken);
                await _taskRepository.UpdateGoogleIdAsync("Task", change.EntityId, created.GoogleId);
                break;
            case ChangeOperation.Update:
                await _googleTasksService.UpdateTaskAsync(payload.TaskListId, payload, cancellationToken);
                break;
            case ChangeOperation.Delete:
                await _googleTasksService.DeleteTaskAsync(payload.TaskListId, payload.GoogleId, cancellationToken);
                break;
            case ChangeOperation.Move:
                await _googleTasksService.MoveTaskAsync(
                    payload.TaskListId,
                    payload.GoogleId,
                    payload.ParentId,
                    cancellationToken: cancellationToken);
                break;
        }
    }

    public void StartBackgroundSync(TimeSpan interval)
    {
        StopBackgroundSync();
        _syncTimer = new Timer(
            async _ => await SyncAsync(),
            null,
            interval,
            interval);
    }

    public void StopBackgroundSync()
    {
        _syncTimer?.Dispose();
        _syncTimer = null;
    }

    private void RaiseSyncStatus(bool isSyncing, string message, int progress)
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
        {
            IsSyncing = isSyncing,
            Message = message,
            Progress = progress
        });
    }
}
