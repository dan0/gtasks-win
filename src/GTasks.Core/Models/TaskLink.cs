namespace GTasks.Core.Models;

/// <summary>
/// Represents a link attached to a task.
/// </summary>
public class TaskLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    // Navigation
    public TaskItem? Task { get; set; }
}
