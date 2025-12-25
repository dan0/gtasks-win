using Microsoft.EntityFrameworkCore;
using GTasks.Core.Models;
using GTasks.Core.Services;

namespace GTasks.Data;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    #region Task Lists

    public async Task<IReadOnlyList<TaskList>> GetTaskListsAsync()
    {
        return await _context.TaskLists
            .Include(tl => tl.Tasks.Where(t => !t.IsDeleted && !t.IsHidden))
            .OrderBy(tl => tl.Title)
            .ToListAsync();
    }

    public async Task<TaskList?> GetTaskListAsync(string id)
    {
        return await _context.TaskLists
            .Include(tl => tl.Tasks)
            .FirstOrDefaultAsync(tl => tl.Id == id);
    }

    public async Task<TaskList> UpsertTaskListAsync(TaskList taskList)
    {
        var existing = await _context.TaskLists
            .FirstOrDefaultAsync(tl => tl.GoogleId == taskList.GoogleId);

        if (existing != null)
        {
            existing.Title = taskList.Title;
            existing.UpdatedAt = taskList.UpdatedAt;
            existing.SyncState = SyncState.Synced;
        }
        else
        {
            if (string.IsNullOrEmpty(taskList.Id))
                taskList.Id = Guid.NewGuid().ToString();
            _context.TaskLists.Add(taskList);
        }

        await _context.SaveChangesAsync();
        return existing ?? taskList;
    }

    public async Task DeleteTaskListAsync(string id)
    {
        var taskList = await _context.TaskLists.FindAsync(id);
        if (taskList != null)
        {
            _context.TaskLists.Remove(taskList);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region Tasks

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync(string taskListId)
    {
        return await _context.Tasks
            .Include(t => t.Links)
            .Include(t => t.Subtasks)
            .Where(t => t.TaskListId == taskListId && !t.IsDeleted)
            .OrderBy(t => t.Position)
            .ToListAsync();
    }

    public async Task<TaskItem?> GetTaskAsync(string id)
    {
        return await _context.Tasks
            .Include(t => t.Links)
            .Include(t => t.Subtasks)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TaskItem> UpsertTaskAsync(TaskItem task)
    {
        var existing = await _context.Tasks
            .FirstOrDefaultAsync(t => t.GoogleId == task.GoogleId && !string.IsNullOrEmpty(task.GoogleId));

        if (existing != null)
        {
            // Only update if remote is newer (conflict resolution: last-write-wins)
            if (task.UpdatedAt >= existing.UpdatedAt || existing.SyncState == SyncState.Synced)
            {
                existing.Title = task.Title;
                existing.Notes = task.Notes;
                existing.Status = task.Status;
                existing.Due = task.Due;
                existing.Completed = task.Completed;
                existing.ParentId = task.ParentId;
                existing.Position = task.Position;
                existing.IsDeleted = task.IsDeleted;
                existing.IsHidden = task.IsHidden;
                existing.UpdatedAt = task.UpdatedAt;
                existing.SyncState = SyncState.Synced;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(task.Id))
                task.Id = Guid.NewGuid().ToString();
            task.CreatedAt = DateTimeOffset.Now;
            _context.Tasks.Add(task);
        }

        await _context.SaveChangesAsync();
        return existing ?? task;
    }

    public async Task DeleteTaskAsync(string id)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task != null)
        {
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region Pending Changes

    public async Task<IReadOnlyList<PendingChange>> GetPendingChangesAsync()
    {
        return await _context.PendingChanges
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetPendingChangesCountAsync()
    {
        return await _context.PendingChanges.CountAsync();
    }

    public async Task AddPendingChangeAsync(PendingChange change)
    {
        _context.PendingChanges.Add(change);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePendingChangeAsync(PendingChange change)
    {
        _context.PendingChanges.Update(change);
        await _context.SaveChangesAsync();
    }

    public async Task RemovePendingChangeAsync(int id)
    {
        var change = await _context.PendingChanges.FindAsync(id);
        if (change != null)
        {
            _context.PendingChanges.Remove(change);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region Sync Support

    public async Task UpdateGoogleIdAsync(string entityType, string localId, string googleId)
    {
        if (entityType == "TaskList")
        {
            var taskList = await _context.TaskLists.FindAsync(localId);
            if (taskList != null)
            {
                taskList.GoogleId = googleId;
                taskList.SyncState = SyncState.Synced;
                await _context.SaveChangesAsync();
            }
        }
        else if (entityType == "Task")
        {
            var task = await _context.Tasks.FindAsync(localId);
            if (task != null)
            {
                task.GoogleId = googleId;
                task.SyncState = SyncState.Synced;
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task MarkOrphanedItemsAsync(IReadOnlyList<string> validGoogleIds)
    {
        // Mark task lists that don't exist remotely
        var orphanedLists = await _context.TaskLists
            .Where(tl => !string.IsNullOrEmpty(tl.GoogleId) && !validGoogleIds.Contains(tl.GoogleId))
            .ToListAsync();

        foreach (var list in orphanedLists)
        {
            _context.TaskLists.Remove(list);
        }

        await _context.SaveChangesAsync();
    }

    #endregion

    #region Search & Filters

    public async Task<IReadOnlyList<TaskItem>> SearchTasksAsync(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        return await _context.Tasks
            .Where(t => !t.IsDeleted && !t.IsHidden &&
                (t.Title.ToLower().Contains(lowerQuery) ||
                 t.Notes.ToLower().Contains(lowerQuery)))
            .OrderByDescending(t => t.UpdatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksByFilterAsync(TaskFilter filter)
    {
        var query = _context.Tasks.Where(t => !t.IsDeleted && !t.IsHidden);

        if (!string.IsNullOrEmpty(filter.TaskListId))
            query = query.Where(t => t.TaskListId == filter.TaskListId);

        if (filter.IsCompleted.HasValue)
            query = query.Where(t => t.IsCompleted == filter.IsCompleted.Value);

        if (filter.TodayOnly)
        {
            var today = DateTimeOffset.Now.Date;
            var tomorrow = today.AddDays(1);
            query = query.Where(t => t.Due >= today && t.Due < tomorrow);
        }

        if (filter.IncludeOverdue)
        {
            var now = DateTimeOffset.Now.Date;
            query = query.Where(t => t.Due < now);
        }

        if (filter.NoDueDate)
            query = query.Where(t => t.Due == null);

        if (filter.DueAfter.HasValue)
            query = query.Where(t => t.Due >= filter.DueAfter.Value);

        if (filter.DueBefore.HasValue)
            query = query.Where(t => t.Due < filter.DueBefore.Value);

        // Exclude completed tasks from date-based filters (unless specifically requesting completed or overdue)
        if ((filter.DueAfter.HasValue || filter.DueBefore.HasValue)
            && !filter.IsCompleted.HasValue && !filter.IncludeOverdue)
            query = query.Where(t => t.Status != Core.Models.TaskStatus.Completed);

        return await query
            .OrderBy(t => t.Due)
            .ThenBy(t => t.Position)
            .ToListAsync();
    }

    #endregion
}
