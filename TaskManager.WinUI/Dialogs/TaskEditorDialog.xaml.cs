using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskManager.Models;

namespace TaskManager_WinUI.Dialogs;

public sealed class SubtaskEditViewModel:ObservableObject
{
    public Guid Id{get;set;}=Guid.NewGuid();private string title="";private bool isCompleted;
    public string Title{get=>title;set=>SetProperty(ref title,value);}
    public bool IsCompleted{get=>isCompleted;set=>SetProperty(ref isCompleted,value);}
    public SubtaskItem ToModel()=>new(){Id=Id,Title=Title.Trim(),IsCompleted=IsCompleted};
}

public sealed partial class TaskEditorDialog:ContentDialog
{
    private readonly TaskItem original;
    private readonly List<string> categories;
    private readonly List<string> tags;
    public ObservableCollection<SubtaskEditViewModel> Subtasks{get;}=[];
    public TaskItem Result{get;private set;}
    public IReadOnlyList<string> Categories=>categories;
    public IReadOnlyList<string> Tags=>tags;
    public TaskEditorDialog(IEnumerable<string> categories,TaskItem? task=null,IEnumerable<string>? tags=null,DateTime? suggestedDate=null)
    {
        InitializeComponent();original=task?.Copy()??new TaskItem{DueDate=(suggestedDate??DateTime.Today).Date};Result=original.Copy();this.categories=[..categories];this.tags=tags?.ToList()??[];
        Title=task is null?"Create task":"Edit task";CategoryInput.ItemsSource=this.categories;CategoryInput.SelectedItem=original.Category;PriorityInput.ItemsSource=Enum.GetNames<TaskPriority>();PriorityInput.SelectedItem=original.Priority.ToString();
        RecurrenceInput.ItemsSource=Enum.GetNames<RecurrenceType>();RecurrenceInput.SelectedItem=original.Recurrence.ToString();TitleInput.Text=original.Title;DescriptionInput.Text=original.Description;NotesInput.Text=original.Notes;TagsInput.Text=string.Join(", ",original.Tags);DueInput.Date=new DateTimeOffset(original.DueDate);CompletedInput.IsOn=original.IsCompleted;
        DateTime reminder=original.ReminderAt??original.DueDate.Date.AddHours(9);ReminderEnabled.IsOn=original.ReminderAt.HasValue;ReminderDate.Date=new DateTimeOffset(reminder.Date);ReminderTime.Time=reminder.TimeOfDay;ReminderFields.Visibility=ReminderEnabled.IsOn?Visibility.Visible:Visibility.Collapsed;
        foreach(var subtask in original.Subtasks)Subtasks.Add(new(){Id=subtask.Id,Title=subtask.Title,IsCompleted=subtask.IsCompleted});SubtaskList.ItemsSource=Subtasks;UpdateProgress();
    }
    private void ReminderEnabled_Toggled(object sender,RoutedEventArgs e)=>ReminderFields.Visibility=ReminderEnabled.IsOn?Visibility.Visible:Visibility.Collapsed;
    private void AddSubtask_Click(object sender,RoutedEventArgs e){Subtasks.Add(new(){Title="New subtask"});UpdateProgress();}
    private SubtaskEditViewModel? ItemFrom(object sender)=>(sender as FrameworkElement)?.DataContext as SubtaskEditViewModel;
    private void RemoveSubtask_Click(object sender,RoutedEventArgs e){if(ItemFrom(sender) is {} item)Subtasks.Remove(item);UpdateProgress();}
    private void MoveSubtaskUp_Click(object sender,RoutedEventArgs e)=>Move(ItemFrom(sender),-1);
    private void MoveSubtaskDown_Click(object sender,RoutedEventArgs e)=>Move(ItemFrom(sender),1);
    private void Move(SubtaskEditViewModel? item,int delta){if(item is null)return;int index=Subtasks.IndexOf(item),next=Math.Clamp(index+delta,0,Subtasks.Count-1);if(index!=next)Subtasks.Move(index,next);}
    private void SubtaskCompletionChanged(object sender,RoutedEventArgs e)=>UpdateProgress();
    private void UpdateProgress(){int complete=Subtasks.Count(s=>s.IsCompleted);SubtaskProgress.Text=Subtasks.Count==0?"Break larger work into manageable steps.":$"{complete} of {Subtasks.Count} complete";}
    private void ContentDialog_PrimaryButtonClick(ContentDialog sender,ContentDialogButtonClickEventArgs args)
    {
        string title=TitleInput.Text.Trim();if(title.Length==0){args.Cancel=true;ValidationBar.Message="Enter a task title.";ValidationBar.IsOpen=true;TitleInput.Focus(FocusState.Programmatic);return;}
        if(Subtasks.Any(s=>string.IsNullOrWhiteSpace(s.Title))){args.Cancel=true;ValidationBar.Message="Name or remove each subtask.";ValidationBar.IsOpen=true;return;}
        Result=original.Copy();Result.Title=title;Result.Description=DescriptionInput.Text.Trim();Result.Notes=NotesInput.Text.Trim();Result.Category=CategoryInput.SelectedItem?.ToString()??"Personal";Result.Priority=Enum.Parse<TaskPriority>(PriorityInput.SelectedItem?.ToString()??"Medium");Result.DueDate=(DueInput.Date??DateTimeOffset.Now).Date;Result.IsCompleted=CompletedInput.IsOn;Result.CompletedAt=Result.IsCompleted?(original.CompletedAt??DateTime.Now):null;
        Result.Tags=TagsInput.Text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();foreach(var value in Result.Tags)if(!tags.Contains(value,StringComparer.OrdinalIgnoreCase))tags.Add(value);
        Result.Subtasks=Subtasks.Select(s=>s.ToModel()).ToList();Result.Recurrence=Enum.Parse<RecurrenceType>(RecurrenceInput.SelectedItem?.ToString()??"None");if(Result.Recurrence!=RecurrenceType.None)Result.RecurrenceSeriesId??=Result.Id;
        Result.ReminderAt=ReminderEnabled.IsOn?(ReminderDate.Date??new DateTimeOffset(Result.DueDate)).Date+ReminderTime.Time:null;if(Result.ReminderAt!=original.ReminderAt)Result.ReminderTriggered=false;
    }
}
