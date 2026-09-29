using TaskManager.Models;

namespace TaskManager.Services;

public static class TaskQuery
{
    public static List<TaskItem> Filter(IEnumerable<TaskItem> tasks, TaskView view, string search,
        string? category, TaskPriority? priority, TaskSort sort, DateTime today, string? tag = null)
    {
        var result = tasks.Where(t => view switch
        {
            TaskView.Dashboard => !t.IsCompleted,
            TaskView.Today => !t.IsCompleted && t.DueDate.Date == today.Date,
            TaskView.Upcoming => !t.IsCompleted && t.DueDate.Date > today.Date,
            TaskView.Completed => t.IsCompleted,
            TaskView.Calendar => true,
            _ => true
        });
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            result = result.Where(t => t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.Tags.Any(x => x.Contains(term, StringComparison.OrdinalIgnoreCase))
                || t.Subtasks.Any(x => x.Title.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }
        if (category is not null) result = result.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (priority.HasValue) result = result.Where(t => t.Priority == priority.Value);
        if (tag is not null) result = result.Where(t => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        return (sort == TaskSort.Priority
            ? result.OrderBy(t => t.IsCompleted).ThenByDescending(t => t.Priority).ThenBy(t => t.DueDate)
            : result.OrderBy(t => t.IsCompleted).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority))
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id).ToList();
    }
}
