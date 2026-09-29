using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using TaskManager.Models;

namespace TaskManager_WinUI.Services;

public static class NotificationService
{
    public static bool TryShow(TaskItem task)
    {
        try
        {
            var builder=new AppNotificationBuilder().AddText("Task reminder").AddText(task.Title).AddText(task.DueDate.Date<DateTime.Today?"This task is overdue.":$"Due {task.DueDate:dddd, dd MMM}.");
            AppNotificationManager.Default.Show(builder.BuildNotification());return true;
        }
        catch{return false;}
    }
}
