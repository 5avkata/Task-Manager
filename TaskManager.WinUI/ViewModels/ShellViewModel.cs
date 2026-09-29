using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager_WinUI.ViewModels;

public sealed class TaskCardViewModel : ObservableObject
{
    public TaskItem Model { get; }
    private bool isSelected;
    public TaskCardViewModel(TaskItem model) => Model=model;
    public Guid Id=>Model.Id;
    public string Title=>Model.Title;
    public string Description=>Model.Description;
    public string Category=>Model.Category;
    public string Priority=>Model.Priority.ToString();
    public TaskPriority PriorityValue=>Model.Priority;
    public string DueText=>Model.DueDate.Date==DateTime.Today?"Today":Model.DueDate.Date==DateTime.Today.AddDays(1)?"Tomorrow":Model.DueDate.ToString("ddd, dd MMM");
    public string DueDetail=>Model.IsCompleted&&Model.CompletedAt.HasValue?$"Completed {Model.CompletedAt:dd MMM}":Model.IsOverdue(DateTime.Today)?"Overdue":"";
    public bool IsOverdue=>Model.IsOverdue(DateTime.Today);
    public bool IsCompleted=>Model.IsCompleted;
    public bool HasDescription=>!string.IsNullOrWhiteSpace(Model.Description);
    public bool HasSubtasks=>Model.Subtasks.Count>0;
    public string SubtaskText=>$"{Model.CompletedSubtaskCount}/{Model.Subtasks.Count}";
    public double SubtaskProgress=>Model.SubtaskPercent;
    public bool HasReminder=>Model.ReminderAt.HasValue&&!Model.ReminderTriggered;
    public bool IsRecurring=>Model.Recurrence!=RecurrenceType.None;
    public string RecurrenceText=>Model.Recurrence.ToString();
    public IReadOnlyList<string> Tags=>Model.Tags;
    public IEnumerable<string> VisibleTags=>Model.Tags.Take(3);
    public bool IsSelected{get=>isSelected;set=>SetProperty(ref isSelected,value);}
}

public sealed class TaskGroupViewModel
{
    public string Title { get; }
    public ObservableCollection<TaskCardViewModel> Tasks { get; }
    public TaskGroupViewModel(string title,IEnumerable<TaskCardViewModel> tasks){Title=title;Tasks=new(tasks);}
}

public sealed class ShellViewModel : ObservableObject
{
    private readonly TaskStore store;
    private AppData data;
    private TaskView currentView=TaskView.Dashboard;
    private string searchText="";
    private string? categoryFilter,tagFilter;
    private TaskPriority? priorityFilter;
    private TaskSort sort=TaskSort.DueDate;
    private RecurrenceType? recurrenceFilter;
    private bool? completionFilter;
    private string statusText="Saved on this device";
    public event Action? Changed;
    public event Action<string>? Error;
    public ObservableCollection<TaskGroupViewModel> Groups { get; }=[];
    public ObservableCollection<TaskCardViewModel> TodayPreview { get; }=[];
    public ObservableCollection<TaskCardViewModel> UpcomingPreview { get; }=[];
    public AppData Data=>data;
    public TaskView CurrentView{get=>currentView;set{if(SetProperty(ref currentView,value))Refresh();}}
    public string SearchText{get=>searchText;set{if(SetProperty(ref searchText,value))Refresh();}}
    public string? CategoryFilter{get=>categoryFilter;set{if(SetProperty(ref categoryFilter,value))Refresh();}}
    public string? TagFilter{get=>tagFilter;set{if(SetProperty(ref tagFilter,value))Refresh();}}
    public TaskPriority? PriorityFilter{get=>priorityFilter;set{if(SetProperty(ref priorityFilter,value))Refresh();}}
    public RecurrenceType? RecurrenceFilter{get=>recurrenceFilter;set{if(SetProperty(ref recurrenceFilter,value))Refresh();}}
    public bool? CompletionFilter{get=>completionFilter;set{if(SetProperty(ref completionFilter,value))Refresh();}}
    public TaskSort Sort{get=>sort;set{if(SetProperty(ref sort,value))Refresh();}}
    public string StatusText{get=>statusText;private set=>SetProperty(ref statusText,value);}
    public int Total=>data.Tasks.Count;
    public int Pending=>data.Tasks.Count(t=>!t.IsCompleted);
    public int Overdue=>data.Tasks.Count(t=>t.IsOverdue(DateTime.Today));
    public ProductivityInsights Insights=>TaskLogic.Insights(data.Tasks,DateTime.Now);
    public int SelectedCount=>Groups.SelectMany(g=>g.Tasks).Count(t=>t.IsSelected);
    public IEnumerable<TaskItem> SelectedTasks=>Groups.SelectMany(g=>g.Tasks).Where(t=>t.IsSelected).Select(t=>t.Model);
    public bool HasFilters=>SearchText.Length>0||CategoryFilter is not null||TagFilter is not null||PriorityFilter is not null||RecurrenceFilter is not null||CompletionFilter is not null;

    public ShellViewModel()
    {
        store=new TaskStore();var loaded=store.Load();data=loaded.Data;
        if(loaded.Warning is not null)StatusText=loaded.Warning;
        Refresh();
    }

    public void Refresh()
    {
        var cards=Filtered().Select(t=>new TaskCardViewModel(t)).ToList();
        Groups.Clear();
        foreach(var group in BuildGroups(cards))Groups.Add(group);
        TodayPreview.Clear();foreach(var card in data.Tasks.Where(t=>!t.IsCompleted&&(t.DueDate.Date<=DateTime.Today)).OrderBy(t=>t.DueDate).ThenByDescending(t=>t.Priority).Take(5).Select(t=>new TaskCardViewModel(t)))TodayPreview.Add(card);
        UpcomingPreview.Clear();foreach(var card in data.Tasks.Where(t=>!t.IsCompleted&&t.DueDate.Date>DateTime.Today).OrderBy(t=>t.DueDate).ThenByDescending(t=>t.Priority).Take(4).Select(t=>new TaskCardViewModel(t)))UpcomingPreview.Add(card);
        OnPropertyChanged(nameof(Total));OnPropertyChanged(nameof(Pending));OnPropertyChanged(nameof(Overdue));OnPropertyChanged(nameof(Insights));OnPropertyChanged(nameof(HasFilters));OnPropertyChanged(nameof(SelectedCount));Changed?.Invoke();
    }

    private IEnumerable<TaskItem> Filtered()
    {
        TaskView queryView=currentView switch{TaskView.Calendar=>TaskView.AllTasks,TaskView.Today=>TaskView.Dashboard,_=>currentView};
        var result=TaskQuery.Filter(data.Tasks,queryView,SearchText,CategoryFilter,PriorityFilter,Sort,DateTime.Today,TagFilter).AsEnumerable();
        if(currentView==TaskView.Today)result=result.Where(t=>t.DueDate.Date<=DateTime.Today);
        if(recurrenceFilter.HasValue)result=result.Where(t=>t.Recurrence==recurrenceFilter);
        if(completionFilter.HasValue)result=result.Where(t=>t.IsCompleted==completionFilter);
        return result;
    }

    private IEnumerable<TaskGroupViewModel> BuildGroups(List<TaskCardViewModel> cards)
    {
        if(currentView==TaskView.Today)
        {
            var overdue=cards.Where(t=>t.Model.DueDate.Date<DateTime.Today).ToList();if(overdue.Count>0)yield return new("Needs attention",overdue);
            var today=cards.Where(t=>t.Model.DueDate.Date==DateTime.Today).ToList();if(today.Count>0)yield return new("Due today",today);yield break;
        }
        if(currentView==TaskView.Upcoming)
        {
            DateTime tomorrow=DateTime.Today.AddDays(1),week=DateTime.Today.AddDays(7);
            var first=cards.Where(t=>t.Model.DueDate.Date==tomorrow).ToList();if(first.Count>0)yield return new("Tomorrow",first);
            var second=cards.Where(t=>t.Model.DueDate.Date>tomorrow&&t.Model.DueDate.Date<=week).ToList();if(second.Count>0)yield return new("This week",second);
            var later=cards.Where(t=>t.Model.DueDate.Date>week).ToList();if(later.Count>0)yield return new("Later",later);yield break;
        }
        if(currentView==TaskView.Completed)
        {
            DateTime week=DateTime.Today.AddDays(-7);var recent=cards.Where(t=>t.Model.CompletedAt>=week).ToList();if(recent.Count>0)yield return new("This week",recent);
            var older=cards.Except(recent).ToList();if(older.Count>0)yield return new("Earlier",older);yield break;
        }
        if(cards.Count>0)yield return new("Tasks",cards);
    }

    public void ClearFilters(){searchText="";categoryFilter=null;tagFilter=null;priorityFilter=null;recurrenceFilter=null;completionFilter=null;sort=TaskSort.DueDate;OnPropertyChanged(nameof(SearchText));OnPropertyChanged(nameof(Sort));Refresh();}
    public bool Save(AppData next,string message)
    {
        try{store.Save(next);data=next;StatusText=message+"  •  "+DateTime.Now.ToString("HH:mm");Refresh();return true;}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){Error?.Invoke(ex.Message);return false;}
    }
    public void ReplaceData(AppData replacement)=>Save(replacement,"Backup imported");
    public void Toggle(TaskItem item)
    {
        var next=data.Copy();if(item.IsCompleted)TaskLogic.MarkIncomplete(next.Tasks.Single(t=>t.Id==item.Id));else TaskLogic.Complete(next,item.Id,DateTime.Now);Save(next,item.IsCompleted?"Task marked incomplete":"Task completed");
    }
    public void Delete(IEnumerable<Guid> ids){var set=ids.ToHashSet();var next=data.Copy();next.Tasks.RemoveAll(t=>set.Contains(t.Id));Save(next,set.Count==1?"Task deleted":$"{set.Count} tasks deleted");}
    public void Upsert(TaskItem item,IEnumerable<string> categories,IEnumerable<string> tags)
    {
        var next=data.Copy();next.Categories=[..categories];next.Tags=[..tags];int index=next.Tasks.FindIndex(t=>t.Id==item.Id);bool wasIncomplete=index<0||!next.Tasks[index].IsCompleted;
        if(index<0)next.Tasks.Add(item);else next.Tasks[index]=item;
        if(item.IsCompleted&&wasIncomplete&&item.Recurrence!=RecurrenceType.None)TaskLogic.Complete(next,item.Id,item.CompletedAt??DateTime.Now);
        Save(next,index<0?"Task created":"Task updated");
    }
    public void BulkComplete(bool complete)
    {
        var ids=SelectedTasks.Select(t=>t.Id).ToList();var next=data.Copy();foreach(var id in ids){if(complete)TaskLogic.Complete(next,id,DateTime.Now);else TaskLogic.MarkIncomplete(next.Tasks.Single(t=>t.Id==id));}Save(next,$"{ids.Count} tasks updated");
    }
    public void BulkCategory(string value){MutateSelected(t=>t.Category=value,"Categories updated");}
    public void BulkPriority(TaskPriority value){MutateSelected(t=>t.Priority=value,"Priorities updated");}
    private void MutateSelected(Action<TaskItem> mutation,string message){var ids=SelectedTasks.Select(t=>t.Id).ToHashSet();var next=data.Copy();foreach(var task in next.Tasks.Where(t=>ids.Contains(t.Id)))mutation(task);Save(next,message);}
}
