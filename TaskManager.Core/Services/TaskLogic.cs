using TaskManager.Models;

namespace TaskManager.Services;

public sealed record ProductivityInsights(int CompletedThisWeek, int CompletedThisMonth, int Overdue,
    int Completed, int Total, IReadOnlyDictionary<TaskPriority,int> PendingByPriority,
    IReadOnlyDictionary<string,int> TasksByCategory)
{
    public int CompletionRate => Total == 0 ? 0 : (int)Math.Round(100d * Completed / Total);
}

public static class TaskLogic
{
    public static DateTime NextDueDate(DateTime dueDate, RecurrenceType recurrence) => recurrence switch
    {
        RecurrenceType.Daily => dueDate.Date.AddDays(1),
        RecurrenceType.Weekly => dueDate.Date.AddDays(7),
        RecurrenceType.Monthly => dueDate.Date.AddMonths(1),
        _ => dueDate.Date
    };

    public static TaskItem? Complete(AppData data, Guid taskId, DateTime completedAt)
    {
        var task = data.Tasks.Single(t => t.Id == taskId);
        task.IsCompleted = true; task.CompletedAt = completedAt;
        if (task.Recurrence == RecurrenceType.None) return null;
        task.RecurrenceSeriesId ??= task.Id;
        DateTime nextDate = NextDueDate(task.DueDate, task.Recurrence);
        if (data.Tasks.Any(t => t.RecurrenceSeriesId == task.RecurrenceSeriesId && t.DueDate.Date == nextDate)) return null;
        var next = task.Copy();
        next.Id = Guid.NewGuid(); next.IsCompleted = false; next.CompletedAt = null; next.CreatedAt = completedAt;
        next.DueDate = nextDate; next.Subtasks.ForEach(s => s.IsCompleted = false);
        if (task.ReminderAt.HasValue)
        {
            TimeSpan offset = task.ReminderAt.Value - task.DueDate.Date;
            next.ReminderAt = nextDate.Add(offset); next.ReminderTriggered = false;
        }
        data.Tasks.Add(next); return next;
    }

    public static void MarkIncomplete(TaskItem task) { task.IsCompleted = false; task.CompletedAt = null; }

    public static ProductivityInsights Insights(IEnumerable<TaskItem> source, DateTime now)
    {
        var tasks = source.ToList();
        int mondayOffset = ((int)now.DayOfWeek + 6) % 7;
        DateTime weekStart = now.Date.AddDays(-mondayOffset), monthStart = new(now.Year,now.Month,1);
        return new(
            tasks.Count(t => t.CompletedAt >= weekStart && t.CompletedAt <= now),
            tasks.Count(t => t.CompletedAt >= monthStart && t.CompletedAt <= now),
            tasks.Count(t => t.IsOverdue(now.Date)), tasks.Count(t => t.IsCompleted), tasks.Count,
            Enum.GetValues<TaskPriority>().ToDictionary(p => p,p => tasks.Count(t => !t.IsCompleted && t.Priority == p)),
            tasks.GroupBy(t => t.Category,StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key,g => g.Count(),StringComparer.OrdinalIgnoreCase));
    }

    public static List<TaskItem> DueReminders(AppData data, DateTime now) => data.Tasks
        .Where(t => !t.IsCompleted && t.ReminderAt.HasValue && !t.ReminderTriggered && t.ReminderAt <= now)
        .OrderBy(t => t.ReminderAt).ToList();
}
