using TaskManager.Models;

namespace TaskManager.UI;

internal sealed class TaskDialog : Form
{
    private readonly RoundedTextBox titleInput = new() { Dock = DockStyle.Fill, MaxLength = 160, PlaceholderText = "What needs to get done?", AccessibleName = "Task title" };
    private readonly RoundedTextBox descriptionInput = new() { Dock = DockStyle.Fill, Multiline = true, MaxLength = 4000, PlaceholderText = "Add a few details...", AccessibleName = "Task description" };
    private readonly RoundedComboBox categoryInput = Theme.Combo();
    private readonly RoundedComboBox priorityInput = Theme.Combo();
    private readonly RoundedDatePicker dueInput = new() { AccessibleName = "Due date" };
    private readonly ModernCheckBox completedInput = new() { Text = "Mark as completed", Dock = DockStyle.Fill };
    private readonly Label validation = Theme.Label("", 9, Theme.Red);
    private readonly TaskItem original;
    public TaskItem Result { get; private set; }
    public List<string> Categories { get; }

    public TaskDialog(IEnumerable<string> categories, TaskItem? task = null)
    {
        SuspendLayout();
        original = task?.Copy() ?? new TaskItem(); Result = original.Copy(); Categories = [.. categories];
        Text = task is null ? "Create task" : "Edit task";
        AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Font(); BackColor = Theme.Background; ForeColor = Theme.Ink;
        ClientSize = new Size(580,448); MinimumSize = new Size(560,480);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; ShowIcon = false;
        MinimizeBox = false; MaximizeBox = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24,18,24,18), ColumnCount = 1, RowCount = 11, AutoScroll = true };
        foreach (int height in new[] { 36,24,40,28,68,28,40,28,40,30,44 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        layout.Controls.Add(Theme.Label(task is null ? "Create a task" : "Edit task",18,style:FontStyle.Bold),0,0);
        layout.Controls.Add(Theme.Label("Title *",9,style:FontStyle.Bold),0,1); layout.Controls.Add(titleInput,0,2);
        layout.Controls.Add(Theme.Label("Description (optional)",9,Theme.Muted),0,3); layout.Controls.Add(descriptionInput,0,4);

        var labels = TwoColumns(); labels.Controls.Add(Theme.Label("Category",9,style:FontStyle.Bold),0,0); labels.Controls.Add(Theme.Label("Priority",9,style:FontStyle.Bold),1,0); layout.Controls.Add(labels,0,5);
        var fields = TwoColumns();
        var categoryRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0,0,12,0) };
        categoryRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        categoryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); categoryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,66));
        categoryInput.AccessibleName = "Task category"; priorityInput.AccessibleName = "Task priority";
        var addCategory = new StyledButton("+ New") { Dock = DockStyle.Fill, Margin = new Padding(6,0,0,0), Font = Theme.Font(9) };
        categoryRow.Controls.Add(categoryInput,0,0); categoryRow.Controls.Add(addCategory,1,0);
        fields.Controls.Add(categoryRow,0,0); fields.Controls.Add(priorityInput,1,0); layout.Controls.Add(fields,0,6);
        layout.Controls.Add(Theme.Label("Due date",9,style:FontStyle.Bold),0,7);
        var dueRow = TwoColumns(); dueInput.Margin = new Padding(0,0,12,0); dueRow.Controls.Add(dueInput,0,0); dueRow.Controls.Add(completedInput,1,0); layout.Controls.Add(dueRow,0,8);
        layout.Controls.Add(validation,0,9);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        var save = new StyledButton("&Save task",true) { Width = 128, Height = 40, Margin = new Padding(8,0,0,0) };
        var cancel = new StyledButton("Cancel") { Width = 100, Height = 40, Margin = Padding.Empty, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons,0,10);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        categoryInput.Items.AddRange(Categories.Cast<object>()); categoryInput.SelectedItem = original.Category;
        if (categoryInput.SelectedIndex < 0) categoryInput.SelectedIndex = 0;
        priorityInput.Items.AddRange(Enum.GetNames<TaskPriority>()); priorityInput.SelectedItem = original.Priority.ToString();
        titleInput.Text = original.Title; descriptionInput.Text = original.Description;
        dueInput.Value = original.DueDate; completedInput.Checked = original.IsCompleted;
        addCategory.Click += (_,_) =>
        {
            using var dialog = new CategoryDialog(Categories);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            Categories.Add(dialog.CategoryName); categoryInput.Items.Add(dialog.CategoryName); categoryInput.SelectedItem = dialog.CategoryName;
        };
        titleInput.TextChanged += (_,_) => { if (!string.IsNullOrWhiteSpace(titleInput.Text)) validation.Text = ""; };
        save.Click += (_,_) =>
        {
            if (string.IsNullOrWhiteSpace(titleInput.Text)) { validation.Text = "Please enter a task title."; titleInput.Focus(); return; }
            Result = original.Copy(); Result.Title = titleInput.Text.Trim(); Result.Description = descriptionInput.Text.Trim();
            Result.Category = categoryInput.SelectedItem?.ToString() ?? "Personal";
            Result.Priority = Enum.Parse<TaskPriority>(priorityInput.SelectedItem!.ToString()!);
            Result.DueDate = dueInput.Value.Date; Result.IsCompleted = completedInput.Checked;
            DialogResult = DialogResult.OK;
        };
        Shown += (_,_) =>
        {
            FitToScreen(this); titleInput.Focus(); titleInput.SelectionStart = titleInput.Text.Length;
        };
        ResumeLayout(true);
    }
    private static TableLayoutPanel TwoColumns()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        return row;
    }
    internal static void FitToScreen(Form form)
    {
        if (form.StartPosition == FormStartPosition.Manual) return;
        var area = Screen.FromControl(form).WorkingArea;
        form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width,area.Width),Math.Min(form.MinimumSize.Height,area.Height));
        form.Size = new Size(Math.Min(form.Width,area.Width),Math.Min(form.Height,area.Height));
        form.Location = new Point(Math.Clamp(form.Left,area.Left,area.Right-form.Width),Math.Clamp(form.Top,area.Top,area.Bottom-form.Height));
    }
}

internal sealed class CategoryDialog : Form
{
    private readonly RoundedTextBox input = new() { Dock = DockStyle.Fill, MaxLength = 32, PlaceholderText = "For example, Fitness", AccessibleName = "Category name" };
    public string CategoryName { get; private set; } = "";
    public CategoryDialog(IEnumerable<string> categories)
    {
        SuspendLayout();
        Text = "New category"; AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420,232); FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false;
        BackColor = Theme.Background; Font = Theme.Font();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24,18,24,18), ColumnCount = 1, RowCount = 5 };
        foreach (int h in new[] { 36,28,40,32,42 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        var error = Theme.Label("",9,Theme.Red);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        var add = new StyledButton("&Create category",true) { Width = 148, Height = 40, Margin = new Padding(8,0,0,0) };
        var cancel = new StyledButton("Cancel") { Width = 92, Height = 40, Margin = Padding.Empty, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(add); buttons.Controls.Add(cancel);
        layout.Controls.Add(Theme.Label("New category",18,style:FontStyle.Bold),0,0);
        layout.Controls.Add(Theme.Label("Give your tasks a place to belong.",9,Theme.Muted),0,1);
        layout.Controls.Add(input,0,2); layout.Controls.Add(error,0,3); layout.Controls.Add(buttons,0,4); Controls.Add(layout); AcceptButton = add; CancelButton = cancel;
        var existing = categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        input.TextChanged += (_,_) => error.Text = "";
        add.Click += (_,_) =>
        {
            string name = input.Text.Trim();
            if (name.Length == 0) { error.Text = "Enter a name for your category."; input.Focus(); return; }
            if (existing.Contains(name)) { error.Text = "This category already exists."; input.Focus(); return; }
            CategoryName = name; DialogResult = DialogResult.OK;
        };
        Shown += (_,_) => { TaskDialog.FitToScreen(this); input.Focus(); };
        ResumeLayout(true);
    }
}

internal sealed class DeleteTaskDialog : Form
{
    public DeleteTaskDialog(string title)
    {
        SuspendLayout(); Text = "Delete task"; AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440,208); FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false;
        Font = Theme.Font(); BackColor = Theme.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24,18,24,18), RowCount = 4, ColumnCount = 1 };
        foreach (int h in new[] { 36,34,44,42 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        layout.Controls.Add(Theme.Label("Delete this task?",18,style:FontStyle.Bold),0,0);
        layout.Controls.Add(Theme.Label(title,10,style:FontStyle.Bold),0,1);
        layout.Controls.Add(Theme.Label("This will permanently remove it from your list.",9,Theme.Muted),0,2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        var delete = new StyledButton("Delete task",true) { Width = 120, Height = 40, DialogResult = DialogResult.OK, Margin = new Padding(8,0,0,0) };
        var cancel = new StyledButton("Keep task") { Width = 108, Height = 40, DialogResult = DialogResult.Cancel, Margin = Padding.Empty };
        buttons.Controls.Add(delete); buttons.Controls.Add(cancel); layout.Controls.Add(buttons,0,3); Controls.Add(layout);
        AcceptButton = cancel; CancelButton = cancel;
        Shown += (_,_) => { TaskDialog.FitToScreen(this); cancel.Focus(); };
        ResumeLayout(true);
    }
}
