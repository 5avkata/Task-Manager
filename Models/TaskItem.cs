namespace TaskManager.Models;

public enum TaskPriority { Low, Medium, High }
public enum TaskView { Dashboard, Today, Upcoming, AllTasks, Completed }
public enum TaskSort { DueDate, Priority }

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

    public bool IsOverdue(DateTime today) => !IsCompleted && DueDate.Date < today.Date;
    public TaskItem Copy() => (TaskItem)MemberwiseClone();
}

public sealed class AppData
{
    [System.Text.Json.Serialization.JsonRequired]
    public int Version { get; set; } = 1;
    [System.Text.Json.Serialization.JsonRequired]
    public List<string> Categories { get; set; } = ["Personal", "School", "Work"];
    [System.Text.Json.Serialization.JsonRequired]
    public List<TaskItem> Tasks { get; set; } = [];
    public AppData Copy() => new()
    {
        Version = Version,
        Categories = [.. Categories],
        Tasks = Tasks.Select(t => t.Copy()).ToList()
    };
}
