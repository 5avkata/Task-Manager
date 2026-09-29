namespace TaskManager.Models;

public enum TaskPriority { Low, Medium, High }
public enum TaskView { Dashboard, Today, Upcoming, Calendar, AllTasks, Completed }
public enum TaskSort { DueDate, Priority }
public enum RecurrenceType { None, Daily, Weekly, Monthly }

public sealed class SubtaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public SubtaskItem Copy() => new() { Id = Id, Title = Title, IsCompleted = IsCompleted };
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Personal";
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime DueDate { get; set; } = DateTime.Today;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public string Notes { get; set; } = "";
    public List<SubtaskItem> Subtasks { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public RecurrenceType Recurrence { get; set; }
    public Guid? RecurrenceSeriesId { get; set; }
    public DateTime? ReminderAt { get; set; }
    public bool ReminderTriggered { get; set; }

    public bool IsOverdue(DateTime today) => !IsCompleted && DueDate.Date < today.Date;
    public int CompletedSubtaskCount => Subtasks.Count(s => s.IsCompleted);
    public int SubtaskPercent => Subtasks.Count == 0 ? 0 : (int)Math.Round(100d * CompletedSubtaskCount / Subtasks.Count);
    public TaskItem Copy() => new()
    {
        Id = Id, Title = Title, Description = Description, Category = Category, Priority = Priority,
        DueDate = DueDate, IsCompleted = IsCompleted, CreatedAt = CreatedAt, CompletedAt = CompletedAt,
        Notes = Notes, Subtasks = Subtasks.Select(s => s.Copy()).ToList(), Tags = [.. Tags],
        Recurrence = Recurrence, RecurrenceSeriesId = RecurrenceSeriesId,
        ReminderAt = ReminderAt, ReminderTriggered = ReminderTriggered
    };
}

public sealed class AppData
{
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; set; } = 1;
    [System.Text.Json.Serialization.JsonRequired]
    public List<string> Categories { get; set; } = ["Personal", "School", "Work"];
    [System.Text.Json.Serialization.JsonRequired]
    public List<TaskItem> Tasks { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public AppData Copy() => new()
    {
        Version = Version,
        Categories = [.. Categories],
        Tasks = Tasks.Select(t => t.Copy()).ToList(),
        Tags = [.. Tags]
    };
}
