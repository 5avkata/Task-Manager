using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.UI;

internal sealed class MainForm : Form
{
    private readonly TaskStore store;
    private AppData data;
    private readonly bool readOnly;
    private TaskView view = TaskView.Dashboard;
    private readonly Dictionary<TaskView, StyledButton> navigation = [];
    private readonly Label heading = Theme.Label("Dashboard", 25, style: FontStyle.Bold);
    private readonly Label subtitle = Theme.Label("A clear view of what matters next.", 10, Theme.Muted);
    private readonly Label listTitle = Theme.Label("Your focus", 13, style: FontStyle.Bold);
    private readonly Label listCount = Theme.Label("", 9, Theme.Muted);
    private readonly Label status = Theme.Label("", 8.5f, Theme.Muted);
    private readonly RoundedTextBox search = new() { PlaceholderText = "Search tasks...", SearchIcon = true, Dock = DockStyle.Fill, MaxLength = 160, AccessibleName = "Search tasks by title" };
    private readonly RoundedComboBox category = Theme.Combo();
    private readonly RoundedComboBox priority = Theme.Combo();
    private readonly RoundedComboBox sort = Theme.Combo();
    private readonly TaskGrid grid = new();
    private readonly Panel empty = new() { Dock = DockStyle.Top, Height = 136, BackColor = Theme.Background };
    private readonly Label emptyTitle = Theme.Label("", 16, style: FontStyle.Bold);
    private readonly Label emptyDetail = Theme.Label("", 10, Theme.Muted);
    private readonly StyledButton emptyAction = new("Create your first task", true) { Width = 200, Height = 42 };
    private readonly TableLayoutPanel main = new() { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 10), ColumnCount = 1, RowCount = 6 };
    private readonly TableLayoutPanel overview = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
    private readonly StatCard totalCard = new() { Caption = "Total tasks" };
    private readonly StatCard pendingCard = new() { Caption = "Pending", Accent = Theme.Ink, Icon = Glyph.Clock };
    private readonly StatCard completedCard = new() { Caption = "Completed", Accent = Theme.Green, Icon = Glyph.Check };
    private readonly StatCard overdueCard = new() { Caption = "Overdue", Accent = Theme.Red, Icon = Glyph.Calendar };
    private readonly ProgressStrip progress = new();
    private readonly System.Windows.Forms.Timer dayTimer = new() { Interval = 30_000 };
    private DateTime displayedDay = DateTime.Today;
    private bool refreshing;

    public MainForm(TaskStore store)
    {
        SuspendLayout();
        this.store = store;
        var loaded = store.Load(); data = loaded.Data; readOnly = loaded.ReadOnly;
        Text = "Task Manager • Your day, organized";
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Font(); ForeColor = Theme.Ink; BackColor = Theme.Background;
        ClientSize = new Size(1180, 760); MinimumSize = new Size(980, 580);
        StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(BuildSidebar(), 0, 0); shell.Controls.Add(main, 1, 0); Controls.Add(shell);
        main.Margin = Padding.Empty;
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        BuildHeader(); BuildOverview(); BuildFilters(); BuildList();
        main.Controls.Add(status, 0, 5);
        grid.ToggleRequested += ToggleTask; grid.EditRequested += EditTask; grid.DeleteRequested += DeleteTask;
        RefreshCategories(); RefreshView();
        SetStatus(readOnly ? "Read-only session • Saved data is protected" : "Saved on this device • All changes save automatically");
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { EditTask(null); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; }
        };
        dayTimer.Tick += (_, _) =>
        {
            if (displayedDay != DateTime.Today) { displayedDay = DateTime.Today; RefreshView(); }
        };
        dayTimer.Start();
        Shown += (_, _) =>
        {
            if (StartPosition != FormStartPosition.Manual)
            {
                var area = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
            }
            if (loaded.Warning is not null) MessageBox.Show(this, loaded.Warning + "\n\nData folder:\n" + store.DirectoryPath, "Saved data notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        ResumeLayout(true);
        RefreshView();
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(23, 36, 58), Padding = new Padding(16, 22, 16, 18), Margin = Padding.Empty, ColumnCount = 1, RowCount = 11 };
        foreach (int h in new[] { 42, 38, 30, 44, 44, 44, 44, 44 }) sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        sidebar.Controls.Add(Theme.Label("Task Manager", 14, Color.White, FontStyle.Bold), 0, 0);
        sidebar.Controls.Add(Theme.Label("Your day, organized.", 9, Color.FromArgb(158, 178, 209)), 0, 1);
        sidebar.Controls.Add(Theme.Label("WORKSPACE", 8, Color.FromArgb(128, 149, 181), FontStyle.Bold), 0, 2);
        var items = new[] { (TaskView.Dashboard, "Dashboard"), (TaskView.Today, "Today"), (TaskView.Upcoming, "Upcoming"), (TaskView.AllTasks, "All Tasks"), (TaskView.Completed, "Completed") };
        int row = 3;
        foreach (var (target, name) in items)
        {
            var button = new StyledButton(name) { Navigation = true, Dock = DockStyle.Fill, BackColor = sidebar.BackColor, Margin = new Padding(0, 3, 0, 3) };
            button.Click += (_, _) => { view = target; RefreshView(); }; navigation.Add(target, button); sidebar.Controls.Add(button, 0, row++);
        }
        var categoryButton = new StyledButton("+ New category") { Navigation = true, Dock = DockStyle.Fill, BackColor = sidebar.BackColor, Enabled = !readOnly, Font = Theme.Font(9) };
        categoryButton.Click += (_, _) => AddCategory(); sidebar.Controls.Add(categoryButton, 0, 9);
        sidebar.Controls.Add(Theme.Label("A little progress, every day.\n\nSaved on your device.", 8, Color.FromArgb(143, 163, 193)), 0, 10);
        return sidebar;
    }

    private void BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); header.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        header.Controls.Add(heading, 0, 0); header.Controls.Add(subtitle, 0, 1);
        var add = new StyledButton("+  New task", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0), Enabled = !readOnly };
        add.Click += (_, _) => EditTask(null); header.Controls.Add(add, 1, 0); main.Controls.Add(header, 0, 0);
    }

    private void BuildOverview()
    {
        overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); overview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var cards = new StatCardRow();
        foreach (var card in new[] { totalCard,pendingCard,completedCard,overdueCard }) { card.Dock = DockStyle.None; cards.Controls.Add(card); }
        overview.Controls.Add(cards, 0, 0); overview.Controls.Add(progress, 0, 1); main.Controls.Add(overview, 0, 1);
    }

    private void BuildFilters()
    {
        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Margin = Padding.Empty, Padding = new Padding(0, 3, 0, 5) };
        foreach (float width in new[] { 32f, 25f, 23f, 20f }) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var names = new[] { "SEARCH", "CATEGORY", "PRIORITY", "SORT BY" };
        for (int i = 0; i < names.Length; i++) filters.Controls.Add(Theme.Label(names[i], 8, Theme.Muted, FontStyle.Bold), i, 0);
        Control[] inputs = [search, category, priority, sort];
        for (int i = 0; i < inputs.Length; i++) { inputs[i].Margin = new Padding(0, 0, 8, 0); filters.Controls.Add(inputs[i], i, 1); }
        priority.Items.AddRange(["All priorities", "Low", "Medium", "High"]); priority.SelectedIndex = 0;
        sort.Items.AddRange(["Due date", "Priority"]); sort.SelectedIndex = 0;
        category.AccessibleName = "Filter by category"; priority.AccessibleName = "Filter by priority"; sort.AccessibleName = "Sort tasks";
        var clear = new StyledButton("Reset") { Dock = DockStyle.Fill, Margin = Padding.Empty, Font = Theme.Font(9) };
        clear.Click += (_, _) => ResetFilters(); filters.Controls.Add(clear, 4, 1);
        search.TextChanged += (_, _) => RefreshView(); category.SelectedIndexChanged += (_, _) => RefreshView();
        priority.SelectedIndexChanged += (_, _) => RefreshView(); sort.SelectedIndexChanged += (_, _) => RefreshView();
        main.Controls.Add(filters, 0, 2);
    }

    private void BuildList()
    {
        var titleRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        listCount.TextAlign = ContentAlignment.MiddleRight;
        titleRow.Controls.Add(listTitle, 0, 0); titleRow.Controls.Add(listCount, 1, 0); main.Controls.Add(titleRow, 0, 3);
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Margin = Padding.Empty, AutoScroll = true };
        host.Controls.Add(grid); host.Controls.Add(empty); main.Controls.Add(host, 0, 4);
        emptyTitle.TextAlign = ContentAlignment.MiddleLeft; emptyDetail.TextAlign = ContentAlignment.MiddleLeft;
        emptyTitle.Dock = DockStyle.None; emptyDetail.Dock = DockStyle.None;
        empty.Controls.Add(emptyTitle); empty.Controls.Add(emptyDetail); empty.Controls.Add(emptyAction);
        empty.Resize += (_, _) =>
        {
            int s(int n) => (int)Math.Round(n * DeviceDpi / 96f);
            emptyTitle.SetBounds(s(20), s(16), Math.Max(0, empty.Width-s(40)), s(26));
            emptyDetail.SetBounds(s(20), s(44), Math.Max(0, empty.Width-s(40)), s(24));
            emptyAction.SetBounds(s(20), s(84), s(180), s(36));
        };
        empty.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.Rounded(new RectangleF(1,1,empty.Width-3,empty.Height-3),12*DeviceDpi/96f);
            using var fill = new SolidBrush(Color.White); using var border = new Pen(Theme.Border);
            e.Graphics.FillPath(fill,path); e.Graphics.DrawPath(border,path);
        };
        emptyTitle.BackColor = Color.White; emptyDetail.BackColor = Color.White; emptyAction.BackColor = Color.White;
        emptyAction.Click += (_, _) => { if (HasFilters) ResetFilters(); else EditTask(null); };
    }

    private bool HasFilters => !string.IsNullOrWhiteSpace(search.Text) || category.SelectedIndex > 0 || priority.SelectedIndex > 0;

    private void ResetFilters()
    {
        refreshing = true; search.Clear(); category.SelectedIndex = 0; priority.SelectedIndex = 0; sort.SelectedIndex = 0;
        refreshing = false; RefreshView();
    }

    private void RefreshCategories()
    {
        refreshing = true;
        string? selected = category.SelectedIndex > 0 ? category.SelectedItem?.ToString() : null;
        category.Items.Clear(); category.Items.Add("All categories"); category.Items.AddRange(data.Categories.Cast<object>().ToArray());
        category.SelectedIndex = 0;
        if (selected is not null && category.Items.Contains(selected)) category.SelectedItem = selected;
        refreshing = false;
    }

    internal void RefreshView()
    {
        if (refreshing) return;
        main.SuspendLayout();
        foreach (var (target, button) in navigation) { button.Selected = target == view; button.Invalidate(); }
        heading.Text = view == TaskView.AllTasks ? "All Tasks" : view.ToString();
        subtitle.Text = view switch
        {
            TaskView.Dashboard => DateTime.Today.ToString("dddd, dd MMMM yyyy") + "  •  Make today count.",
            TaskView.Today => "Pending tasks due today. One step at a time.",
            TaskView.Upcoming => "Plan ahead. Pending tasks due after today.",
            TaskView.Completed => "A record of your progress. Nicely done.",
            _ => "Every task, in one organized place."
        };
        bool dashboard = view == TaskView.Dashboard;
        overview.Visible = dashboard; main.RowStyles[1].Height = dashboard ? 136 * DeviceDpi / 96f : 0;
        int completed = data.Tasks.Count(t => t.IsCompleted);
        totalCard.Value = data.Tasks.Count; pendingCard.Value = data.Tasks.Count - completed;
        completedCard.Value = completed; overdueCard.Value = data.Tasks.Count(t => t.IsOverdue(DateTime.Today));
        totalCard.AccessibleName = $"Total tasks: {totalCard.Value}"; pendingCard.AccessibleName = $"Pending tasks: {pendingCard.Value}";
        completedCard.AccessibleName = $"Completed tasks: {completed}"; overdueCard.AccessibleName = $"Overdue tasks: {overdueCard.Value}";
        foreach (var card in new[] { totalCard, pendingCard, completedCard, overdueCard }) card.Invalidate();
        progress.Total = data.Tasks.Count; progress.Completed = completed; progress.AccessibleName = $"Overall completion: {(data.Tasks.Count == 0 ? 0 : Math.Round(100d * completed / data.Tasks.Count))} percent"; progress.Invalidate();
        TaskPriority? selectedPriority = priority.SelectedIndex > 0 ? Enum.Parse<TaskPriority>(priority.SelectedItem!.ToString()!) : null;
        var tasks = TaskQuery.Filter(data.Tasks, view, search.Text, category.SelectedIndex > 0 ? category.SelectedItem?.ToString() : null,
            selectedPriority, sort.SelectedIndex == 1 ? TaskSort.Priority : TaskSort.DueDate, DateTime.Today);
        grid.SetTasks(tasks);
        listTitle.Text = view switch { TaskView.Dashboard => "Your focus", TaskView.Today => "Today's tasks", TaskView.Upcoming => "Coming up", TaskView.Completed => "Finished tasks", _ => "Your tasks" };
        listCount.Text = $"{tasks.Count} {(tasks.Count == 1 ? "task" : "tasks")}" + (HasFilters ? " • filtered" : "");
        empty.Visible = tasks.Count == 0; grid.Visible = tasks.Count > 0;
        if (tasks.Count == 0)
        {
            empty.BringToFront();
            (string title, string detail) = view switch
            {
                TaskView.Dashboard => data.Tasks.Count == 0 ? ("Start with one task", "Add something you'd like to work on.") : ("All caught up", "No pending tasks. Enjoy the breathing room."),
                TaskView.Today => ("Today is clear", "You have no pending tasks due today."),
                TaskView.Upcoming => ("Nothing coming up", "Tasks due after today will appear here."),
                TaskView.Completed => ("No completed tasks yet", "Finish a task and see your progress here."),
                _ => ("Your task list is ready", "Create your first task to get started.")
            };
            emptyTitle.Text = HasFilters ? "No matching tasks" : title;
            emptyDetail.Text = HasFilters ? "Try another title, category, or priority." : detail;
            emptyAction.Text = HasFilters ? "Reset filters" : data.Tasks.Count == 0 ? "Create your first task" : "+  New task";
            emptyAction.Enabled = HasFilters || !readOnly;
        }
        main.ResumeLayout(true);
    }

    private void EditTask(TaskItem? task)
    {
        if (readOnly) { ShowReadOnly(); return; }
        using var dialog = new TaskDialog(data.Categories, task);
        while (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var next = data.Copy(); next.Categories = dialog.Categories;
            int index = next.Tasks.FindIndex(t => t.Id == dialog.Result.Id);
            if (index >= 0) next.Tasks[index] = dialog.Result; else next.Tasks.Add(dialog.Result);
            if (Commit(next, task is null ? "Task created" : "Task updated")) return;
            // Keep the user's input available for another save attempt.
            dialog.DialogResult = DialogResult.None;
        }
    }

    private void ToggleTask(TaskItem task)
    {
        if (readOnly) { ShowReadOnly(); return; }
        var next = data.Copy(); next.Tasks.Single(t => t.Id == task.Id).IsCompleted = !task.IsCompleted;
        Commit(next, task.IsCompleted ? "Task marked incomplete" : "Task completed — nice work!");
    }

    private void DeleteTask(TaskItem task)
    {
        if (readOnly) { ShowReadOnly(); return; }
        using var confirmation = new DeleteTaskDialog(task.Title);
        if (confirmation.ShowDialog(this) != DialogResult.OK) return;
        var next = data.Copy(); next.Tasks.RemoveAll(t => t.Id == task.Id); Commit(next, "Task deleted");
    }

    private void AddCategory()
    {
        using var dialog = new CategoryDialog(data.Categories);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var next = data.Copy(); next.Categories.Add(dialog.CategoryName); Commit(next, $"Category “{dialog.CategoryName}” created");
    }

    private bool Commit(AppData next, string message)
    {
        try
        {
            store.Save(next); data = next; RefreshCategories(); RefreshView();
            SetStatus(message + "  •  Saved at " + DateTime.Now.ToString("HH:mm"));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            SetStatus("Changes were not saved. Please try again.");
            MessageBox.Show(this, "Your change could not be saved, so the existing task list has been kept. Check free disk space and access to the data folder, then try again.\n\n" + ex.Message,
                "Unable to save", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void SetStatus(string text) { status.Text = text; status.AccessibleName = text; }
    private void ShowReadOnly() => MessageBox.Show(this, "Changes are disabled because the saved data could not be loaded safely. Please resolve the data file issue and restart the application.", "Read-only session", MessageBoxButtons.OK, MessageBoxIcon.Information);
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (IsHandleCreated) BeginInvoke(new Action(RefreshView));
    }
    protected override void Dispose(bool disposing) { if (disposing) dayTimer.Dispose(); base.Dispose(disposing); }
}
