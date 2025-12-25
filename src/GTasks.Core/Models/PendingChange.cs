namespace GTasks.Core.Models;

/// <summary>
/// Tracks local changes that need to be synced to Google Tasks.
/// </summary>
public class PendingChange
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty; // "Task" or "TaskList"
    public string EntityId { get; set; } = string.Empty;
    public ChangeOperation Operation { get; set; }
    public string Payload { get; set; } = string.Empty; // JSON serialized change data
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public int Attempts { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ChangeOperation
{
    Create,
    Update,
    Delete,
    Move
}
