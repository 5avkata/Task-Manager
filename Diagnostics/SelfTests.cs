using System.Reflection;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.UI;

namespace TaskManager.Diagnostics;

internal static class SelfTests
{
    public static int Run()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "TaskManager-tests-" + Guid.NewGuid().ToString("N"));
        string results = Path.Combine(Environment.CurrentDirectory, "TestResults");
        var log = new List<string>();
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            log.Add("PASS: " + name); checks++;
        }
        try
        {
            Directory.CreateDirectory(temporary); Directory.CreateDirectory(results);
            var store = new TaskStore(temporary);
            var empty = store.Load();
            Check(empty.Data.Tasks.Count == 0 && empty.Warning is null, "Missing data starts cleanly");
            Check(empty.Data.Categories.SequenceEqual(new[] { "Personal", "School", "Work" }), "Default categories");
            var today = DateTime.Today;
            var data = new AppData
            {
                Categories = ["Personal", "School", "Work", "Creative"],
                Tasks =
                [
                    new() { Title = "Finish portfolio website", Description = "Polish the project descriptions and add final screenshots.", Category = "School", Priority = TaskPriority.High, DueDate = today.AddDays(-2) },
                    new() { Title = "Prepare for the math exam", Description = "Review chapter 4 and work through the practice questions.", Category = "School", Priority = TaskPriority.High, DueDate = today },
                    new() { Title = "Plan next week's goals", Description = "Choose three things to focus on.", Category = "Personal", Priority = TaskPriority.Medium, DueDate = today.AddDays(2) },
                    new() { Title = "Sketch a new app idea", Description = "Explore a simple habit tracker concept.", Category = "Creative", Priority = TaskPriority.Low, DueDate = today.AddDays(4) },
                    new() { Title = "Organize project files", Category = "Work", Priority = TaskPriority.Low, DueDate = today.AddDays(-1), IsCompleted = true },
                    new() { Title = "Read for twenty minutes", Category = "Personal", DueDate = today, IsCompleted = true }
                ]
            };
            store.Save(data);
            var loaded = store.Load();
            Check(loaded.Data.Tasks.Count == 6 && loaded.Data.Categories.Contains("Creative"), "JSON round trip and custom categories");
            Check(loaded.Data.Tasks[0].Id == data.Tasks[0].Id && loaded.Data.Tasks[0].Description == data.Tasks[0].Description && loaded.Data.Tasks[0].Priority == TaskPriority.High, "Fields and identities preserved");
            var changed = data.Copy(); changed.Tasks[0].Title = "Updated title"; changed.Tasks[1].IsCompleted = true;
            store.Save(changed);
            Check(store.Load().Data.Tasks[0].Title == "Updated title" && store.Load().Data.Tasks[1].IsCompleted, "Edit and completion persist");
            Check(File.Exists(store.BackupPath), "Atomic save retains backup");
            File.WriteAllText(store.FilePath, "{ broken json");
            var recovered = store.Load();
            Check(recovered.Warning is not null && recovered.Data.Tasks[0].Title == data.Tasks[0].Title, "Corrupt primary recovers previous snapshot");
            Check(Directory.GetFiles(temporary, "tasks.unreadable-*.json").Length == 1, "Corrupt primary preserved");
            store.Save(data);
            File.WriteAllText(store.FilePath, "{\"Version\":99,\"Tasks\":[],\"Categories\":[]}");
            Check(store.Load().ReadOnly && File.ReadAllText(store.FilePath).Contains("99"), "Future format remains protected");
            File.WriteAllText(store.FilePath, "{}");
            Check(store.Load().Warning is not null, "Incomplete JSON schema is rejected");
            var fresh = new TaskStore(Path.Combine(temporary, "no-backup")); Directory.CreateDirectory(fresh.DirectoryPath);
            File.WriteAllText(fresh.FilePath, "null");
            Check(fresh.Load().Data.Tasks.Count == 0 && Directory.GetFiles(fresh.DirectoryPath, "tasks.unreadable-*.json").Length == 1, "Invalid data without backup starts safely");
            store.Save(data);
            using (var locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                bool failed = false;
                try { store.Save(changed); } catch (IOException) { failed = true; }
                Check(failed, "Locked data file reports a save failure");
            }
            Check(store.Load().Data.Tasks[0].Title == data.Tasks[0].Title, "Failed save leaves original intact");
            Check(Directory.GetFiles(temporary, "*.tmp").Length == 0, "Temporary writes cleaned up");
            var removed = data.Copy(); removed.Tasks.RemoveAt(0); store.Save(removed);
            Check(store.Load().Data.Tasks.Count == 5, "Deletion persists");
            List<TaskItem> Query(TaskView view, string search = "", string? category = null, TaskPriority? priority = null, TaskSort sort = TaskSort.DueDate)
                => TaskQuery.Filter(data.Tasks, view, search, category, priority, sort, today);
            Check(Query(TaskView.Dashboard).Count == 4, "Dashboard contains pending tasks");
            Check(Query(TaskView.Today).Count == 1, "Today excludes completed tasks");
            Check(Query(TaskView.Upcoming).Count == 2, "Upcoming excludes today and overdue tasks");
            Check(Query(TaskView.Completed).Count == 2, "Completed view");
            Check(Query(TaskView.AllTasks).Count == 6, "All Tasks includes completed");
            Check(Query(TaskView.AllTasks, "  PORTFOLIO  ").Count == 1, "Search ignores case and surrounding whitespace");
            Check(Query(TaskView.AllTasks, category: "School", priority: TaskPriority.High).Count == 2, "Combined category and priority filters");
            Check(Query(TaskView.Dashboard, sort: TaskSort.Priority).First().Priority == TaskPriority.High, "High priority sorts first");
            Check(Query(TaskView.AllTasks).Take(4).All(t => !t.IsCompleted), "Pending tasks sort before completed");
            Check(data.Tasks.Count(t => t.IsOverdue(today)) == 1, "Overdue excludes completed and today's tasks");
            store.Save(data);
            using (var form = new MainForm(store))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
                form.Show(); Application.DoEvents();
                var grid = Field<TaskGrid>(form, "grid");
                Check(grid.Rows.Count == 4, "Main form renders pending tasks");
                SaveImage(form, Path.Combine(results, "dashboard.png"));
                VerifyDashboardResize(form,results,Check);
                Field<RoundedTextBox>(form, "search").Text = "no matching title"; Application.DoEvents();
                Check(Field<Panel>(form, "empty").Visible && grid.Rows.Count == 0, "Filtered empty state is displayed");
                Field<RoundedTextBox>(form, "search").Clear();
                var navigation = Field<Dictionary<TaskView, StyledButton>>(form, "navigation");
                navigation[TaskView.Completed].PerformClick(); Application.DoEvents();
                Check(grid.Rows.Count == 2 && !Field<TableLayoutPanel>(form, "overview").Visible, "Navigation switches list and summary layout");
                navigation[TaskView.AllTasks].PerformClick(); Application.DoEvents();
                Check(grid.Rows.Count == 6, "All Tasks navigation renders all rows");
                typeof(MainForm).GetMethod("ToggleTask", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [data.Tasks[0]]);
                Check(store.Load().Data.Tasks[0].IsCompleted, "UI completion action saves changes");
                navigation[TaskView.Dashboard].PerformClick(); form.Size = form.MinimumSize; Application.DoEvents();
                SaveImage(form, Path.Combine(results, "dashboard-minimum.png"));
                Check(grid.Width >= grid.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width), "Task columns fit without horizontal scrolling at minimum size");
                Check(grid.Height >= grid.ColumnHeadersHeight + 76 * form.DeviceDpi / 96f, "Minimum size shows a complete task row");
                log.Add($"Rendering checked at {form.DeviceDpi} DPI ({100 * form.DeviceDpi / 96}% scaling).");
                form.Size = new Size(1920,1040); Application.DoEvents();
                SaveImage(form,Path.Combine(results,"dashboard-1920.png"));
                var cards = new[] { "totalCard","pendingCard","completedCard","overdueCard" }.Select(name => Field<StatCard>(form,name)).ToList();
                Check(cards.Max(c => c.Width)-cards.Min(c => c.Width) <= 1 && cards.Select(c => c.Height).Distinct().Count() == 1,"Statistic cards have equal dimensions");
                navigation[TaskView.AllTasks].PerformClick();
                var categoryFilter = Field<RoundedComboBox>(form,"category");
                var priorityFilter = Field<RoundedComboBox>(form,"priority");
                categoryFilter.SelectedItem = "School"; priorityFilter.SelectedItem = "High";
                Check(grid.Rows.Count == 2,"Rounded category and priority filters work together");
                Field<RoundedTextBox>(form,"search").Text = "math";
                Check(grid.Rows.Count == 1 && ((TaskItem)grid.Rows[0].Tag!).Title.Contains("math"),"Rounded search field updates the task list");
                SaveImage(form,Path.Combine(results,"search-active.png"));
                Call(form,"ResetFilters");
                var sorting = Field<RoundedComboBox>(form,"sort"); sorting.SelectedItem = "Priority";
                Check(((TaskItem)grid.Rows[0].Tag!).Priority == TaskPriority.High,"Rounded sorting dropdown orders visible tasks");
                categoryFilter.OpenDropDown(); Application.DoEvents();
                Check(categoryFilter.IsDropDownOpen,"Rounded dropdown opens options");
                var options = Field<ListBox>(categoryFilter,"options");
                var menu = Field<ToolStripDropDown>(categoryFilter,"popup");
                using (var bitmap = new Bitmap(menu.Width,menu.Height)) { menu.DrawToBitmap(bitmap,menu.ClientRectangle); bitmap.Save(Path.Combine(results,"category-dropdown.png")); }
                options.SelectedIndex = options.Items.IndexOf("Work");
                Call(options,"OnKeyDown",new KeyEventArgs(Keys.Enter));
                Check(categoryFilter.SelectedItem?.ToString() == "Work" && !categoryFilter.IsDropDownOpen && grid.Rows.Count == 1,"Dropdown Enter selects a category and filters tasks");
                categoryFilter.OpenDropDown(); Application.DoEvents();
                options = Field<ListBox>(categoryFilter,"options"); options.SelectedIndex = 0;
                Call(options,"OnKeyDown",new KeyEventArgs(Keys.Escape));
                Check(categoryFilter.SelectedItem?.ToString() == "Work","Dropdown Escape cancels a pending selection");
                categoryFilter.AccessibilityObject.Value = "Personal";
                Check(categoryFilter.SelectedItem?.ToString() == "Personal","Dropdown exposes an accessible selectable value");
                Call(form,"ResetFilters");

                RunModal(() => Call(form,"EditTask",new object?[] { null }), dialog =>
                {
                    var editor = (UI.TaskDialog)dialog;
                    Field<RoundedTextBox>(editor,"titleInput").Text = "UI workflow task";
                    Field<RoundedTextBox>(editor,"descriptionInput").Text = "Created through the redesigned editor.";
                    Field<RoundedComboBox>(editor,"categoryInput").SelectedItem = "Creative";
                    Field<RoundedComboBox>(editor,"priorityInput").SelectedItem = "Low";
                    Field<RoundedDatePicker>(editor,"dueInput").Value = today.AddDays(7);
                    ((Button)editor.AcceptButton!).PerformClick();
                });
                var created = store.Load().Data.Tasks.Single(t => t.Title == "UI workflow task");
                Check(created.Category == "Creative" && created.Priority == TaskPriority.Low && created.DueDate == today.AddDays(7),"Create task UI persists all selected fields");
                int CreatedRow() => grid.Rows.Cast<DataGridViewRow>().Single(r => ((TaskItem)r.Tag!).Id == created.Id).Index;
                RunModal(() => ClickCellAction(grid,CreatedRow(),5), dialog =>
                {
                    var editor = (UI.TaskDialog)dialog;
                    Field<RoundedTextBox>(editor,"titleInput").Text = "Edited workflow task";
                    ((Button)editor.AcceptButton!).PerformClick();
                });
                Check(store.Load().Data.Tasks.Single(t => t.Id == created.Id).Title == "Edited workflow task","Painted edit icon opens editor and saves changes");
                ClickCellAction(grid,CreatedRow(),0);
                Check(store.Load().Data.Tasks.Single(t => t.Id == created.Id).IsCompleted,"Painted checkbox click completes a task");
                grid.Rows[CreatedRow()].Cells[0].AccessibilityObject.DoDefaultAction();
                Check(!store.Load().Data.Tasks.Single(t => t.Id == created.Id).IsCompleted,"Accessible checkbox action marks a task incomplete");
                RunModal(() => ClickCellAction(grid,CreatedRow(),6), dialog =>
                {
                    SaveImage(dialog,Path.Combine(results,"delete-confirmation.png"));
                    ((Button)dialog.CancelButton!).PerformClick();
                });
                Check(store.Load().Data.Tasks.Any(t => t.Id == created.Id),"Cancelling deletion preserves the task");
                RunModal(() => grid.Rows[CreatedRow()].Cells[6].AccessibilityObject.DoDefaultAction(), dialog =>
                {
                    Descendants(dialog).OfType<Button>().Single(b => b.DialogResult == DialogResult.OK).PerformClick();
                });
                Check(store.Load().Data.Tasks.All(t => t.Id != created.Id),"Accessible delete action requires confirmation and persists removal");
                Check(store.Load().Data.Tasks.Count == 6 && store.Load().Data.Categories.SequenceEqual(data.Categories),"UI workflow preserves existing tasks and categories");
                var many = data.Copy();
                many.Tasks = Enumerable.Range(1,120).Select(i => new TaskItem { Title = $"Study session {i:000}", Description = "Review notes and practice exercises.", Category = "School", Priority = TaskPriority.Medium, DueDate = today.AddDays(i) }).ToList();
                Call(form,"Commit",many,"Layout test"); navigation[TaskView.Dashboard].PerformClick();
                grid.FirstDisplayedScrollingRowIndex = 115; grid.CurrentCell = grid.Rows[119].Cells[5]; grid.Focus(); Application.DoEvents();
                Check(grid.Rows[119].Displayed && grid.CurrentCell.RowIndex == 119,"Long task lists scroll to the last task with keyboard focus intact");
                SaveImage(form,Path.Combine(results,"long-list-focus.png"));
                form.Close();
            }
            using (var editor = new UI.TaskDialog(data.Categories))
            {
                editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-20000, -20000); editor.ShowInTaskbar = false;
                editor.Show(); Application.DoEvents();
                ((Button)editor.AcceptButton!).PerformClick();
                Check(editor.DialogResult != DialogResult.OK && Field<Label>(editor, "validation").Text.Length > 0, "Blank title validation keeps editor open");
                SaveImage(editor, Path.Combine(results, "task-editor.png"));
                var categoryField = Field<RoundedComboBox>(editor,"categoryInput");
                Check(categoryField.Height >= 38*editor.DeviceDpi/96f && categoryField.Bottom <= categoryField.Parent!.ClientSize.Height,"Nested editor dropdown is fully visible");
                var dateField = Field<RoundedDatePicker>(editor,"dueInput");
                dateField.OpenCalendar(); Application.DoEvents();
                var datePopup = Field<ToolStripDropDown>(dateField,"popup");
                var calendar = (MonthCalendar)((ToolStripControlHost)datePopup.Items[0]).Control;
                calendar.SetDate(today.AddDays(3)); Call(calendar,"OnKeyDown",new KeyEventArgs(Keys.Enter));
                Check(dateField.Value == today.AddDays(3) && !datePopup.Visible,"Date popup selects a date with Enter");
                Call(dateField,"OnKeyDown",new KeyEventArgs(Keys.Right));
                Check(dateField.Value == today.AddDays(4),"Date field supports keyboard day adjustment");
                Field<RoundedTextBox>(editor, "titleInput").Text = "  New task  ";
                ((Button)editor.AcceptButton!).PerformClick();
                Check(editor.Result.Title == "New task" && editor.DialogResult == DialogResult.OK, "Editor returns validated trimmed task");
                editor.Close();
            }
            using (var category = new CategoryDialog(data.Categories))
            {
                category.StartPosition = FormStartPosition.Manual; category.Location = new Point(-20000, -20000); category.ShowInTaskbar = false;
                category.Show(); Application.DoEvents();
                var input = Field<RoundedTextBox>(category,"input"); input.Text = "school";
                ((Button)category.AcceptButton!).PerformClick();
                Check(category.DialogResult != DialogResult.OK, "Duplicate categories rejected case-insensitively");
                SaveImage(category,Path.Combine(results,"new-category.png"));
                input.Text = "Fitness"; ((Button)category.AcceptButton!).PerformClick();
                Check(category.CategoryName == "Fitness" && category.DialogResult == DialogResult.OK, "Custom category accepted");
                category.Close();
            }
            using (var emptyForm = new MainForm(new TaskStore(Path.Combine(temporary,"empty-ui"))))
            {
                emptyForm.StartPosition = FormStartPosition.Manual; emptyForm.Location = new Point(-20000,-20000); emptyForm.ShowInTaskbar = false;
                emptyForm.Show(); Application.DoEvents();
                var navigation = Field<Dictionary<TaskView,StyledButton>>(emptyForm,"navigation");
                var messages = new HashSet<string>();
                foreach (var view in Enum.GetValues<TaskView>())
                {
                    navigation[view].PerformClick(); messages.Add(Field<Label>(emptyForm,"emptyTitle").Text);
                    SaveImage(emptyForm,Path.Combine(results,"empty-"+view+".png"));
                }
                Check(messages.Count == 5,"Each navigation section has a distinct empty-state message");
                emptyForm.Size = emptyForm.MinimumSize; navigation[TaskView.Dashboard].PerformClick(); Application.DoEvents();
                var emptyPanel = Field<Panel>(emptyForm,"empty");
                Check(emptyPanel.Height <= emptyPanel.Parent!.ClientSize.Height,"Compact empty state fits the minimum dashboard size");
                emptyForm.Close();
            }
            log.Add($"SUCCESS: {checks} checks passed.");
            File.WriteAllLines(Path.Combine(results, "self-test.txt"), log); return 0;
        }
        catch (Exception ex)
        {
            log.Add(ex.ToString()); Directory.CreateDirectory(results); File.WriteAllLines(Path.Combine(results, "self-test.txt"), log); return 1;
        }
        finally { try { Directory.Delete(temporary, true); } catch (IOException) { } }
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void VerifyDashboardResize(MainForm form,string results,Action<bool,string> check)
    {
        Control[] painted = [Field<StatCard>(form,"totalCard"),Field<StatCard>(form,"pendingCard"),Field<StatCard>(form,"completedCard"),Field<StatCard>(form,"overdueCard"),Field<ProgressStrip>(form,"progress")];
        var normalBounds = form.Bounds;
        var damage = painted.ToDictionary(c => c,c => Rectangle.Empty);
        InvalidateEventHandler record = (sender,e) =>
        {
            var control = (Control)sender!;
            damage[control] = damage[control].IsEmpty ? e.InvalidRect : Rectangle.Union(damage[control],e.InvalidRect);
        };
        foreach (var control in painted) control.Invalidated += record;
        try
        {
            // A full screenshot requests a fresh paint and can hide this bug.
            // Check the damage requested by the real resize before forcing a repaint.
            foreach (int delta in new[] { 240,-120,80,-200 })
            {
                foreach (var control in painted) damage[control] = Rectangle.Empty;
                form.Width += delta;
                foreach (var control in painted)
                    check(damage[control].Contains(control.ClientRectangle),$"Resize {delta:+0;-0} invalidates the entire {control.GetType().Name} surface ({control.Name}{control.AccessibleName})");
                Application.DoEvents();
            }
            for (int cycle = 0; cycle < 2; cycle++)
            {
                form.WindowState = FormWindowState.Maximized; Application.DoEvents();
                var cards = painted.OfType<StatCard>().OrderBy(c => c.Left).ToList();
                check(form.WindowState == FormWindowState.Maximized && cards.All(c => c.Parent!.ClientRectangle.Contains(c.Bounds)) && cards.Zip(cards.Skip(1)).All(pair => pair.First.Right < pair.Second.Left),"Maximized statistic cards stay inside their row without overlap");
                check(cards.Max(c => c.Width)-cards.Min(c => c.Width) <= 1,"Maximized statistic cards retain equal widths");
                if (cycle == 0) SaveImage(form,Path.Combine(results,"dashboard-maximized.png"));
                form.WindowState = FormWindowState.Normal; form.Bounds = normalBounds; Application.DoEvents();
                check(painted.Select(c => c.Parent).Distinct().All(parent => parent is not null) && cards[0].Parent!.Controls.Count == 4,"Restore keeps exactly four statistic controls");
            }
            SaveImage(form,Path.Combine(results,"dashboard-restored.png"));
        }
        finally
        {
            foreach (var control in painted) control.Invalidated -= record;
            form.WindowState = FormWindowState.Normal; form.Bounds = normalBounds;
        }
    }
    private static void Call(object instance,string name,params object?[] arguments) => instance.GetType().GetMethod(name,BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance,arguments);
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static void ClickCellAction(TaskGrid grid,int row,int column)
    {
        int x = grid.Columns[column].Width/2, y = (int)(33*grid.DeviceDpi/96f);
        Call(grid,"OnCellMouseClick",new DataGridViewCellMouseEventArgs(column,row,x,y,new MouseEventArgs(MouseButtons.Left,1,x,y,0)));
    }
    private static void RunModal(Action open,Action<Form> interact)
    {
        Exception? failure = null; bool handled = false; int ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_,_) =>
        {
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Modal);
            if (dialog is null && ++ticks < 100) return;
            timer.Stop();
            if (dialog is null) { failure = new InvalidOperationException("Expected dialog did not open."); return; }
            try { handled = true; interact(dialog); }
            catch (Exception ex) { failure = ex; dialog.DialogResult = DialogResult.Cancel; }
        };
        timer.Start(); open(); timer.Stop();
        if (failure is not null) throw failure;
        if (!handled) throw new InvalidOperationException("Modal action was not exercised.");
    }
    private static void SaveImage(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
