using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager_WinUI.Dialogs;
using TaskManager_WinUI.Services;
using TaskManager_WinUI.ViewModels;
using Windows.Storage.Pickers;

namespace TaskManager_WinUI;

public sealed partial class MainPage:Page
{
    public ShellViewModel ViewModel{get;}=new();
    private readonly UserSettingsService settings=new();
    private readonly DispatcherTimer reminderTimer=new(){Interval=TimeSpan.FromMinutes(1)};
    private DateTime calendarMonth=new(DateTime.Today.Year,DateTime.Today.Month,1),selectedDate=DateTime.Today;
    private bool loaded;

    public MainPage()
    {
        InitializeComponent();DataContext=ViewModel;ViewModel.Changed+=RefreshPresentation;ViewModel.Error+=message=>ShowInfo("Unable to save",message,InfoBarSeverity.Error);
        Loaded+=MainPage_Loaded;reminderTimer.Tick+=(_,_)=>CheckReminders();
    }

    private void MainPage_Loaded(object sender,RoutedEventArgs e)
    {
        loaded=true;ApplyTheme(settings.Current.Theme);ThemeSelector.SelectedIndex=settings.Current.Theme switch{"Light"=>1,"Dark"=>2,_=>0};BuildWeekdayHeaders();RefreshPresentation();UpdateResponsiveLayout();reminderTimer.Start();CheckReminders();
    }

    private void Navigation_SelectionChanged(NavigationView sender,NavigationViewSelectionChangedEventArgs args)
    {
        if(!loaded)return;
        if(args.IsSettingsSelected){ShowSection("Settings");return;}
        if(args.SelectedItemContainer?.Tag is string tag)ShowSection(tag);
    }

    private void ShowSection(string key)
    {
        TaskView? target=key switch{"Dashboard"=>TaskView.Dashboard,"Today"=>TaskView.Today,"Upcoming"=>TaskView.Upcoming,"Calendar"=>TaskView.Calendar,"AllTasks"=>TaskView.AllTasks,"Completed"=>TaskView.Completed,_=>null};
        DashboardView.Visibility=key=="Dashboard"?Visibility.Visible:Visibility.Collapsed;TasksView.Visibility=target.HasValue&&target!=TaskView.Dashboard&&target!=TaskView.Calendar?Visibility.Visible:Visibility.Collapsed;CalendarView.Visibility=key=="Calendar"?Visibility.Visible:Visibility.Collapsed;SettingsView.Visibility=key=="Settings"?Visibility.Visible:Visibility.Collapsed;
        SearchBar.Visibility=TasksView.Visibility==Visibility.Visible?Visibility.Visible:Visibility.Collapsed;
        bool today=key=="Today",upcoming=key=="Upcoming",all=key=="AllTasks",completed=key=="Completed";
        TodayOverviewPanel.Visibility=TodayNextPanel.Visibility=today?Visibility.Visible:Visibility.Collapsed;
        UpcomingOverviewPanel.Visibility=UpcomingPreviewPanel.Visibility=upcoming?Visibility.Visible:Visibility.Collapsed;
        AllOverviewPanel.Visibility=DueSoonPanel.Visibility=all?Visibility.Visible:Visibility.Collapsed;
        CompletedOverviewPanel.Visibility=RecentCompletedPanel.Visibility=completed?Visibility.Visible:Visibility.Collapsed;
        PageTitle.Text=key switch{"AllTasks"=>"All Tasks","Settings"=>"Settings",_=>key};
        PageSubtitle.Text=key switch{"Dashboard"=>"What needs your attention today","Today"=>"A focused view of today and anything overdue","Upcoming"=>"Tomorrow, this week, and what comes later","Calendar"=>"Plan deadlines across the month","AllTasks"=>"Everything in one calm workspace","Completed"=>"A record of progress you have made","Settings"=>"Appearance, organization, and local data",_=>""};
        if(target.HasValue)ViewModel.CurrentView=target.Value;else RefreshSettings();
        if(key=="Calendar")BuildCalendar();
        UpdateResponsiveLayout();
    }

    private void RefreshPresentation()
    {
        if(!loaded)return;
        var insights=ViewModel.Insights;CompletionRing.Value=insights.CompletionRate;CompletionPercent.Text=insights.CompletionRate+"%";WeekMetric.Text=insights.CompletedThisWeek.ToString();MonthMetric.Text=insights.CompletedThisMonth.ToString();OverdueMetric.Text=insights.Overdue.ToString();
        DateTime today=DateTime.Today;
        var tasks=ViewModel.Data.Tasks;
        int completedToday=tasks.Count(t=>t.CompletedAt?.Date==today),pendingToday=tasks.Count(t=>!t.IsCompleted&&t.DueDate.Date==today),todayTotal=completedToday+pendingToday,todayRate=todayTotal==0?0:(int)Math.Round(100d*completedToday/todayTotal);
        int overdue=tasks.Count(t=>t.IsOverdue(today)),highAttention=tasks.Count(t=>!t.IsCompleted&&t.DueDate.Date<=today&&t.Priority==TaskPriority.High);
        TodayCompletedCount.Text=completedToday.ToString();TodayRemainingCount.Text=pendingToday.ToString();TodayOverdueCount.Text=overdue.ToString();TodayHighCount.Text=highAttention.ToString();TodayProgress.Value=todayRate;TodayProgressText.Text=todayTotal==0?"No tasks due":todayRate+"% complete";
        var nextAttention=tasks.Where(t=>!t.IsCompleted&&t.DueDate.Date<=today).OrderBy(t=>t.DueDate).ThenByDescending(t=>t.Priority).FirstOrDefault()
            ??tasks.Where(t=>!t.IsCompleted).OrderBy(t=>t.DueDate).ThenByDescending(t=>t.Priority).FirstOrDefault();
        TodayNextTitle.Text=nextAttention?.Title??"Your schedule is clear";TodayNextMeta.Text=nextAttention is null?"There are no active deadlines.":TaskMeta(nextAttention);

        var future=tasks.Where(t=>!t.IsCompleted&&t.DueDate.Date>today).OrderBy(t=>t.DueDate).ThenByDescending(t=>t.Priority).ToList();
        var nextImportant=future.OrderByDescending(t=>t.Priority).ThenBy(t=>t.DueDate).FirstOrDefault();
        UpcomingSevenCount.Text=future.Count(t=>t.DueDate.Date<=today.AddDays(7)).ToString();UpcomingHighCount.Text=future.Count(t=>t.Priority==TaskPriority.High).ToString();
        UpcomingNextTitle.Text=nextImportant?.Title??"Nothing scheduled";UpcomingNextMeta.Text=nextImportant is null?"Your upcoming plan is clear.":TaskMeta(nextImportant);
        var upcomingSide=future.Take(4).Select(t=>new TaskCardViewModel(t)).ToList();UpcomingSideList.ItemsSource=upcomingSide;NoUpcomingSide.Visibility=upcomingSide.Count==0?Visibility.Visible:Visibility.Collapsed;

        AllActiveCount.Text=tasks.Count(t=>!t.IsCompleted).ToString();AllCompletedCount.Text=tasks.Count(t=>t.IsCompleted).ToString();AllOverdueCount.Text=overdue.ToString();
        RefreshCategorySummary();
        var dueSoon=tasks.Where(t=>!t.IsCompleted&&t.DueDate.Date<=today.AddDays(7)).OrderBy(t=>t.DueDate).ThenByDescending(t=>t.Priority).Take(4).Select(t=>new TaskCardViewModel(t)).ToList();DueSoonList.ItemsSource=dueSoon;NoDueSoon.Visibility=dueSoon.Count==0?Visibility.Visible:Visibility.Collapsed;

        var recentCompleted=tasks.Where(t=>t.IsCompleted).OrderByDescending(t=>t.CompletedAt??DateTime.MinValue).Take(4).Select(t=>new TaskCardViewModel(t)).ToList();
        CompletedWeekCount.Text=insights.CompletedThisWeek.ToString();CompletedMonthCount.Text=insights.CompletedThisMonth.ToString();CompletedRateText.Text=$"{insights.CompletionRate}% overall completion  •  {insights.Completed} of {insights.Total} tasks";
        RecentCompletedList.ItemsSource=recentCompleted;NoRecentCompleted.Visibility=recentCompleted.Count==0?Visibility.Visible:Visibility.Collapsed;
        FocusSummary.Text=ViewModel.TodayPreview.Count==0?"Your day is clear":$"{ViewModel.TodayPreview.Count} item{(ViewModel.TodayPreview.Count==1?"":"s")} need attention";DashboardEmpty.IsOpen=ViewModel.TodayPreview.Count==0;UpcomingEmpty.Visibility=ViewModel.UpcomingPreview.Count==0?Visibility.Visible:Visibility.Collapsed;
        int high=insights.PendingByPriority.GetValueOrDefault(TaskPriority.High),medium=insights.PendingByPriority.GetValueOrDefault(TaskPriority.Medium),low=insights.PendingByPriority.GetValueOrDefault(TaskPriority.Low),max=Math.Max(1,Math.Max(high,Math.Max(medium,low)));HighBar.Value=100d*high/max;MediumBar.Value=100d*medium/max;LowBar.Value=100d*low/max;HighCount.Text=high.ToString();MediumCount.Text=medium.ToString();LowCount.Text=low.ToString();
        bool empty=ViewModel.Groups.Count==0;TasksEmpty.Visibility=empty?Visibility.Visible:Visibility.Collapsed;EmptyTitle.Text=ViewModel.HasFilters?"No tasks match these filters":ViewModel.CurrentView switch{TaskView.Today=>"Nothing due today",TaskView.Upcoming=>"Nothing coming up",TaskView.Completed=>"No completed tasks yet",_=>"Your task list is clear"};EmptyMessage.Text=ViewModel.HasFilters?"Clear a filter or try another search.":"Create a task whenever you are ready.";
        int filters=(string.IsNullOrWhiteSpace(ViewModel.SearchText)?0:1)+(ViewModel.CategoryFilter is null?0:1)+(ViewModel.TagFilter is null?0:1)+(ViewModel.PriorityFilter is null?0:1)+(ViewModel.RecurrenceFilter is null?0:1)+(ViewModel.CompletionFilter is null?0:1);FilterCountText.Text=filters.ToString();FilterCountBadge.Visibility=filters>0?Visibility.Visible:Visibility.Collapsed;ClearFiltersButton.Visibility=filters>0?Visibility.Visible:Visibility.Collapsed;
        UpdateBulkBar();if(CalendarView.Visibility==Visibility.Visible)BuildCalendar();if(SettingsView.Visibility==Visibility.Visible)RefreshSettings();
    }

    private static string TaskMeta(TaskItem task)
    {
        string due=task.IsOverdue(DateTime.Today)?$"Overdue since {task.DueDate:ddd, dd MMM}":task.DueDate.Date==DateTime.Today?"Due today":task.DueDate.Date==DateTime.Today.AddDays(1)?"Due tomorrow":$"Due {task.DueDate:ddd, dd MMM}";
        return $"{due}  •  {task.Category}  •  {task.Priority} priority";
    }

    private void RefreshCategorySummary()
    {
        CategorySummaryPanel.Children.Clear();
        var groups=ViewModel.Data.Tasks.GroupBy(t=>t.Category,StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).Take(5).ToList();
        if(groups.Count==0){CategorySummaryPanel.Children.Add(new TextBlock{Text="No categories to summarize yet.",Foreground=(Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]});return;}
        int maximum=groups.Max(g=>g.Count());
        foreach(var group in groups)
        {
            var row=new Grid{ColumnSpacing=8};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(82)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});
            var label=new TextBlock{Text=group.Key,TextTrimming=TextTrimming.CharacterEllipsis};
            var bar=new ProgressBar{Maximum=maximum,Value=group.Count(),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(bar,1);
            var count=new TextBlock{Text=group.Count().ToString(),HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(count,2);
            row.Children.Add(label);row.Children.Add(bar);row.Children.Add(count);CategorySummaryPanel.Children.Add(row);
        }
    }

    private void Page_SizeChanged(object sender,SizeChangedEventArgs e)=>UpdateResponsiveLayout();
    private void UpdateResponsiveLayout()
    {
        if(!loaded)return;
        bool compact=ActualWidth<1320;
        TasksWorkspace.ColumnDefinitions[1].Width=compact?new GridLength(0):new GridLength(320);
        Grid.SetColumn(TaskInsightsColumn,compact?0:1);Grid.SetRow(TaskInsightsColumn,compact?1:0);
        TaskInsightsColumn.Margin=compact?new Thickness(0,18,0,0):new Thickness(0);
    }

    private TaskCardViewModel? TaskFrom(object sender)=>(sender as FrameworkElement)?.DataContext as TaskCardViewModel;
    private async void NewTask_Click(object sender,RoutedEventArgs e)=>await EditTaskAsync(null,null);
    private async void NewTaskForDay_Click(object sender,RoutedEventArgs e)=>await EditTaskAsync(null,selectedDate);
    private async void EditTask_Click(object sender,RoutedEventArgs e){if(TaskFrom(sender) is { } task)await EditTaskAsync(task.Model,null);}
    private async Task EditTaskAsync(TaskItem? task,DateTime? suggestedDate)
    {
        var dialog=new TaskEditorDialog(ViewModel.Data.Categories,task,ViewModel.Data.Tags,suggestedDate){XamlRoot=XamlRoot};var result=await dialog.ShowAsync();if(result!=ContentDialogResult.Primary)return;
        if(dialog.Result.IsCompleted&&(task is null||!task.IsCompleted)&&dialog.Result.Subtasks.Any(s=>!s.IsCompleted)&&!await ConfirmAsync("Incomplete subtasks","Some subtasks are still incomplete. Mark this task complete anyway?","Complete task"))return;
        ViewModel.Upsert(dialog.Result,dialog.Categories,dialog.Tags);
    }
    private async void CompleteTask_Click(object sender,RoutedEventArgs e)
    {
        if(TaskFrom(sender) is not { } card)return;if(!card.IsCompleted&&card.Model.Subtasks.Any(s=>!s.IsCompleted)&&!await ConfirmAsync("Incomplete subtasks","Some subtasks are still incomplete. Complete the task anyway?","Complete task")){ViewModel.Refresh();return;}ViewModel.Toggle(card.Model);
    }
    private async void DeleteTask_Click(object sender,RoutedEventArgs e){if(TaskFrom(sender) is { } task&&await ConfirmAsync("Delete task",$"Delete “{task.Title}”? This cannot be undone.","Delete"))ViewModel.Delete([task.Id]);}
    private void TaskSelectionChanged(object sender,RoutedEventArgs e)=>DispatcherQueue.TryEnqueue(UpdateBulkBar);
    private void UpdateBulkBar(){int count=ViewModel.SelectedCount;BulkCount.Text=$"{count} selected";BulkBar.Visibility=count>1?Visibility.Visible:Visibility.Collapsed;StatusText.Visibility=count>1?Visibility.Collapsed:Visibility.Visible;}
    private void BulkCancel_Click(object sender,RoutedEventArgs e){foreach(var item in ViewModel.Groups.SelectMany(g=>g.Tasks))item.IsSelected=false;UpdateBulkBar();}
    private void BulkComplete_Click(object sender,RoutedEventArgs e)=>ViewModel.BulkComplete(true);
    private void BulkIncomplete_Click(object sender,RoutedEventArgs e)=>ViewModel.BulkComplete(false);
    private async void BulkDelete_Click(object sender,RoutedEventArgs e){var ids=ViewModel.SelectedTasks.Select(t=>t.Id).ToList();if(await ConfirmAsync("Delete selected tasks",$"Delete {ids.Count} tasks? This cannot be undone.","Delete all"))ViewModel.Delete(ids);}
    private async void BulkCategory_Click(object sender,RoutedEventArgs e){string? value=await ChooseAsync("Change category",ViewModel.Data.Categories);if(value is not null)ViewModel.BulkCategory(value);}
    private async void BulkPriority_Click(object sender,RoutedEventArgs e){string? value=await ChooseAsync("Change priority",Enum.GetNames<TaskPriority>());if(value is not null)ViewModel.BulkPriority(Enum.Parse<TaskPriority>(value));}

    private void SearchBox_TextChanged(AutoSuggestBox sender,AutoSuggestBoxTextChangedEventArgs args){if(args.Reason==AutoSuggestionBoxTextChangeReason.UserInput)ViewModel.SearchText=sender.Text.Trim();}
    private void ClearFilters_Click(object sender,RoutedEventArgs e){SearchBox.Text="";SortSelector.SelectedIndex=0;ViewModel.ClearFilters();}
    private void SortSelector_SelectionChanged(object sender,SelectionChangedEventArgs e){if(loaded&&SortSelector.SelectedItem is ComboBoxItem item&&Enum.TryParse<TaskSort>(item.Tag?.ToString(),out var sort))ViewModel.Sort=sort;}
    private async void FilterButton_Click(object sender,RoutedEventArgs e)
    {
        var category=new ComboBox{Header="Category",ItemsSource=new[]{"All categories"}.Concat(ViewModel.Data.Categories).ToList(),HorizontalAlignment=HorizontalAlignment.Stretch,SelectedItem=ViewModel.CategoryFilter??"All categories"};
        var priority=new ComboBox{Header="Priority",ItemsSource=new[]{"All priorities"}.Concat(Enum.GetNames<TaskPriority>()).ToList(),HorizontalAlignment=HorizontalAlignment.Stretch,SelectedItem=ViewModel.PriorityFilter?.ToString()??"All priorities"};
        var tag=new ComboBox{Header="Tag",ItemsSource=new[]{"All tags"}.Concat(ViewModel.Data.Tags).ToList(),HorizontalAlignment=HorizontalAlignment.Stretch,SelectedItem=ViewModel.TagFilter??"All tags"};
        var recurrence=new ComboBox{Header="Recurrence",ItemsSource=new[]{"Any recurrence"}.Concat(Enum.GetNames<RecurrenceType>()).ToList(),HorizontalAlignment=HorizontalAlignment.Stretch,SelectedItem=ViewModel.RecurrenceFilter?.ToString()??"Any recurrence"};
        var completion=new ComboBox{Header="Status",ItemsSource=new[]{"Any status","Pending","Completed"},HorizontalAlignment=HorizontalAlignment.Stretch,SelectedIndex=ViewModel.CompletionFilter switch{false=>1,true=>2,_=>0}};
        var content=new StackPanel{Spacing=12,Width=360};content.Children.Add(category);content.Children.Add(priority);content.Children.Add(tag);content.Children.Add(recurrence);content.Children.Add(completion);
        var dialog=new ContentDialog{XamlRoot=XamlRoot,Title="Filter tasks",Content=content,PrimaryButtonText="Apply",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Primary};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
        ViewModel.CategoryFilter=category.SelectedIndex>0?category.SelectedItem?.ToString():null;ViewModel.PriorityFilter=priority.SelectedIndex>0?Enum.Parse<TaskPriority>(priority.SelectedItem!.ToString()!):null;ViewModel.TagFilter=tag.SelectedIndex>0?tag.SelectedItem?.ToString():null;ViewModel.RecurrenceFilter=recurrence.SelectedIndex>0?Enum.Parse<RecurrenceType>(recurrence.SelectedItem!.ToString()!):null;ViewModel.CompletionFilter=completion.SelectedIndex switch{1=>false,2=>true,_=>null};
    }

    private void BuildWeekdayHeaders(){CalendarWeekdays.ColumnDefinitions.Clear();for(int i=0;i<7;i++)CalendarWeekdays.ColumnDefinitions.Add(new());string[] days=["MON","TUE","WED","THU","FRI","SAT","SUN"];for(int i=0;i<7;i++){var label=new TextBlock{Text=days[i],HorizontalAlignment=HorizontalAlignment.Center,Foreground=(Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]};Grid.SetColumn(label,i);CalendarWeekdays.Children.Add(label);}}
    private void BuildCalendar()
    {
        CalendarMonth.Text=calendarMonth.ToString("MMMM yyyy");CalendarGrid.Children.Clear();CalendarGrid.ColumnDefinitions.Clear();CalendarGrid.RowDefinitions.Clear();for(int i=0;i<7;i++)CalendarGrid.ColumnDefinitions.Add(new());for(int i=0;i<6;i++)CalendarGrid.RowDefinitions.Add(new());
        int offset=((int)calendarMonth.DayOfWeek+6)%7;DateTime first=calendarMonth.AddDays(-offset);
        for(int index=0;index<42;index++)
        {
            DateTime date=first.AddDays(index);var due=ViewModel.Data.Tasks.Where(t=>t.DueDate.Date==date.Date).OrderBy(t=>t.IsCompleted).ThenByDescending(t=>t.Priority).ToList();
            var stack=new StackPanel{Spacing=3};stack.Children.Add(new TextBlock{Text=date.Day.ToString(),FontWeight=date.Date==DateTime.Today?Microsoft.UI.Text.FontWeights.Bold:Microsoft.UI.Text.FontWeights.Normal,Opacity=date.Month==calendarMonth.Month?1:.42});
            foreach(var task in due.Take(3)){var text=new TextBlock{Text=task.Title,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,Opacity=task.IsCompleted?.58:1};var badge=new Border{Background=new SolidColorBrush(task.IsCompleted?Microsoft.UI.ColorHelper.FromArgb(34,35,137,104):task.IsOverdue(DateTime.Today)?Microsoft.UI.ColorHelper.FromArgb(34,190,64,64):Microsoft.UI.ColorHelper.FromArgb(28,52,104,232)),CornerRadius=new CornerRadius(5),Padding=new Thickness(5,2,5,2),Child=text};stack.Children.Add(badge);}
            if(due.Count>3)stack.Children.Add(new TextBlock{Text=$"+{due.Count-3} more",FontSize=11,Opacity=.65});
            var button=new Button{Tag=date,Content=stack,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Padding=new Thickness(8),Margin=new Thickness(3),MinHeight=72,BorderThickness=new Thickness(date.Date==DateTime.Today?1.5:0),BorderBrush=(Brush)Application.Current.Resources["AppAccentBrush"],Background=date.Date==selectedDate.Date?(Brush)Application.Current.Resources["SoftAccentBrush"]:new SolidColorBrush(Microsoft.UI.Colors.Transparent)};button.Click+=CalendarDay_Click;Grid.SetColumn(button,index%7);Grid.SetRow(button,index/7);CalendarGrid.Children.Add(button);
        }
        RefreshSelectedDay();
    }
    private void CalendarDay_Click(object sender,RoutedEventArgs e){if(sender is Button{Tag:DateTime date}){selectedDate=date;if(date.Month!=calendarMonth.Month)calendarMonth=new(date.Year,date.Month,1);BuildCalendar();}}
    private void RefreshSelectedDay(){var tasks=ViewModel.Data.Tasks.Where(t=>t.DueDate.Date==selectedDate.Date).OrderBy(t=>t.IsCompleted).ThenByDescending(t=>t.Priority).Select(t=>new TaskCardViewModel(t)).ToList();SelectedDayTitle.Text=$"{selectedDate:dddd, dd MMMM}  •  {tasks.Count} {(tasks.Count==1?"task":"tasks")}";SelectedDayTasks.ItemsSource=tasks;CalendarDayEmpty.Visibility=tasks.Count==0?Visibility.Visible:Visibility.Collapsed;}
    private void CalendarPrevious_Click(object sender,RoutedEventArgs e){calendarMonth=calendarMonth.AddMonths(-1);selectedDate=calendarMonth;BuildCalendar();}
    private void CalendarNext_Click(object sender,RoutedEventArgs e){calendarMonth=calendarMonth.AddMonths(1);selectedDate=calendarMonth;BuildCalendar();}
    private void CalendarToday_Click(object sender,RoutedEventArgs e){calendarMonth=new(DateTime.Today.Year,DateTime.Today.Month,1);selectedDate=DateTime.Today;BuildCalendar();}

    private void RefreshSettings()
    {
        CategoryChips.Children.Clear();foreach(string value in ViewModel.Data.Categories)CategoryChips.Children.Add(MakeChip(value,()=>RemoveCategory(value)));TagChips.Children.Clear();foreach(string value in ViewModel.Data.Tags)TagChips.Children.Add(MakeChip(value,()=>RemoveTag(value)));
    }
    private Border MakeChip(string text,Action remove){var stack=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5};stack.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center});var button=new Button{Content=new FontIcon{Glyph="\uE711",FontSize=10},Style=(Style)Application.Current.Resources["SubtleButtonStyle"],Padding=new Thickness(5)};button.Click+=(_,_)=>remove();stack.Children.Add(button);return new Border{Background=(Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],CornerRadius=new CornerRadius(8),Padding=new Thickness(8,3,3,3),Child=stack};}
    private async void AddCategory_Click(object sender,RoutedEventArgs e){string? value=await PromptAsync("New category","Category name");if(value is null)return;if(ViewModel.Data.Categories.Contains(value,StringComparer.OrdinalIgnoreCase)){ShowInfo("Category exists","Choose another name.",InfoBarSeverity.Warning);return;}var next=ViewModel.Data.Copy();next.Categories.Add(value);ViewModel.Save(next,"Category created");}
    private async void AddTag_Click(object sender,RoutedEventArgs e){string? value=await PromptAsync("New tag","Tag name");if(value is null)return;if(ViewModel.Data.Tags.Contains(value,StringComparer.OrdinalIgnoreCase)){ShowInfo("Tag exists","Choose another name.",InfoBarSeverity.Warning);return;}var next=ViewModel.Data.Copy();next.Tags.Add(value);ViewModel.Save(next,"Tag created");}
    private void RemoveCategory(string value){if(ViewModel.Data.Tasks.Any(t=>t.Category.Equals(value,StringComparison.OrdinalIgnoreCase))){ShowInfo("Category in use","Move its tasks to another category before removing it.",InfoBarSeverity.Warning);return;}var next=ViewModel.Data.Copy();next.Categories.RemoveAll(c=>c.Equals(value,StringComparison.OrdinalIgnoreCase));ViewModel.Save(next,"Category removed");}
    private async void RemoveTag(string value){if(!await ConfirmAsync("Remove tag",$"Remove “{value}” from the tag list and all tasks?","Remove"))return;var next=ViewModel.Data.Copy();next.Tags.RemoveAll(t=>t.Equals(value,StringComparison.OrdinalIgnoreCase));foreach(var task in next.Tasks)task.Tags.RemoveAll(t=>t.Equals(value,StringComparison.OrdinalIgnoreCase));ViewModel.Save(next,"Tag removed");}
    private void ThemeSelector_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!loaded||ThemeSelector.SelectedItem is not ComboBoxItem item)return;settings.Current.Theme=item.Tag?.ToString()??"System";settings.Save();ApplyTheme(settings.Current.Theme);}
    private void ApplyTheme(string value)=>RequestedTheme=value switch{"Light"=>ElementTheme.Light,"Dark"=>ElementTheme.Dark,_=>ElementTheme.Default};

    private async void ExportBackup_Click(object sender,RoutedEventArgs e)
    {
        var picker=new FileSavePicker{SuggestedFileName=$"TaskManager-backup-{DateTime.Today:yyyy-MM-dd}"};picker.FileTypeChoices.Add("JSON backup",[".json"]);WinRT.Interop.InitializeWithWindow.Initialize(picker,App.WindowHandle);var file=await picker.PickSaveFileAsync();if(file is null)return;TaskStore.Export(file.Path,ViewModel.Data);ShowInfo("Backup exported",file.Name,InfoBarSeverity.Success);
    }
    private async void ImportBackup_Click(object sender,RoutedEventArgs e)
    {
        var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");WinRT.Interop.InitializeWithWindow.Initialize(picker,App.WindowHandle);var file=await picker.PickSingleFileAsync();if(file is null)return;try{var imported=TaskStore.Import(file.Path);if(await ConfirmAsync("Import backup",$"Replace current data with {imported.Tasks.Count} tasks, {imported.Categories.Count} categories, and {imported.Tags.Count} tags?","Import"))ViewModel.ReplaceData(imported);}catch(Exception ex){ShowInfo("Invalid backup",ex.Message,InfoBarSeverity.Error);}
    }
    private void CheckReminders(){var due=TaskLogic.DueReminders(ViewModel.Data,DateTime.Now);if(due.Count==0)return;var next=ViewModel.Data.Copy();foreach(var task in due)next.Tasks.Single(t=>t.Id==task.Id).ReminderTriggered=true;if(!ViewModel.Save(next,due.Count==1?"Reminder delivered":$"{due.Count} reminders delivered"))return;foreach(var task in due)if(!NotificationService.TryShow(task))ShowInfo("Task reminder",task.Title,InfoBarSeverity.Informational);}

    private async Task<bool> ConfirmAsync(string title,string message,string primary){var dialog=new ContentDialog{XamlRoot=XamlRoot,Title=title,Content=message,PrimaryButtonText=primary,CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close};return await dialog.ShowAsync()==ContentDialogResult.Primary;}
    private async Task<string?> ChooseAsync(string title,IEnumerable<string> values){var combo=new ComboBox{ItemsSource=values.ToList(),SelectedIndex=0,MinWidth=280};var dialog=new ContentDialog{XamlRoot=XamlRoot,Title=title,Content=combo,PrimaryButtonText="Apply",CloseButtonText="Cancel"};return await dialog.ShowAsync()==ContentDialogResult.Primary?combo.SelectedItem?.ToString():null;}
    private async Task<string?> PromptAsync(string title,string placeholder){var input=new TextBox{PlaceholderText=placeholder,MinWidth=320};var dialog=new ContentDialog{XamlRoot=XamlRoot,Title=title,Content=input,PrimaryButtonText="Add",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Primary};return await dialog.ShowAsync()==ContentDialogResult.Primary&&!string.IsNullOrWhiteSpace(input.Text)?input.Text.Trim():null;}
    private void ShowInfo(string title,string message,InfoBarSeverity severity){AppInfoBar.Title=title;AppInfoBar.Message=message;AppInfoBar.Severity=severity;AppInfoBar.IsOpen=true;}
    private async void Page_KeyDown(object sender,KeyRoutedEventArgs e)
    {
        var ctrl=Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if(ctrl&&e.Key==Windows.System.VirtualKey.N){NewTask_Click(this,new RoutedEventArgs());e.Handled=true;}else if(ctrl&&e.Key==Windows.System.VirtualKey.F&&SearchBar.Visibility==Visibility.Visible){SearchBox.Focus(FocusState.Programmatic);e.Handled=true;}else if(e.Key==Windows.System.VirtualKey.Escape&&BulkBar.Visibility==Visibility.Visible){BulkCancel_Click(this,new RoutedEventArgs());e.Handled=true;}else if(e.Key==Windows.System.VirtualKey.Delete&&ViewModel.SelectedCount>0){var ids=ViewModel.SelectedTasks.Select(t=>t.Id).ToList();if(await ConfirmAsync("Delete selected tasks",$"Delete {ids.Count} selected task{(ids.Count==1?"":"s")}? This cannot be undone.","Delete"))ViewModel.Delete(ids);e.Handled=true;}
    }
}
