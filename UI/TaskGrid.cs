using System.Drawing.Drawing2D;
using TaskManager.Models;

namespace TaskManager.UI;

// Keep native scrolling and accessibility; paint continuous rounded task rows.
internal sealed class TaskGrid : DataGridView
{
    public event Action<TaskItem>? ToggleRequested;
    public event Action<TaskItem>? EditRequested;
    public event Action<TaskItem>? DeleteRequested;
    private readonly Font titleFont = Theme.Font(10, FontStyle.Bold);
    private readonly Font detailFont = Theme.Font(9);
    private readonly Font headerFont = Theme.Font(8, FontStyle.Bold);
    private List<TaskItem> tasks = [];
    private int hoveredRow = -1, hoveredColumn = -1;

    public TaskGrid()
    {
        Dock = DockStyle.Fill; BackgroundColor = Theme.Background; BorderStyle = BorderStyle.None;
        DoubleBuffered = true; AllowUserToAddRows = false; AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false; AllowUserToResizeColumns = false;
        AutoGenerateColumns = false; RowHeadersVisible = false; ReadOnly = true;
        MultiSelect = false; SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        CellBorderStyle = DataGridViewCellBorderStyle.None; EnableHeadersVisualStyles = false;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 32;
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Background, ForeColor = Theme.Muted, Font = headerFont, SelectionBackColor = Theme.Background };
        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Background, ForeColor = Theme.Ink, Font = detailFont, SelectionBackColor = Theme.Background, SelectionForeColor = Theme.Ink };
        Columns.Add(new DataGridViewCheckBoxColumn { Name = "Done", HeaderText = "", Width = 44, ToolTipText = "Completion status" });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Task", HeaderText = "TASK", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "CATEGORY", Width = 88 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "PRIORITY", Width = 86 });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Due", HeaderText = "DUE DATE", Width = 116 });
        Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "", Width = 40, Text = "Edit", UseColumnTextForButtonValue = true });
        Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Width = 44, Text = "Delete", UseColumnTextForButtonValue = true });
        foreach (DataGridViewColumn column in Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        Columns[0].CellTemplate = new TaskCheckCell();
        Columns[5].CellTemplate = new TaskActionCell();
        Columns[6].CellTemplate = new TaskActionCell();
        AccessibleName = "Task list";
        AccessibleDescription = "Use arrow keys to select a task. Enter edits; Space toggles completion; Delete requests deletion. Tab moves between actions.";
        CellMouseEnter += (_, e) => UpdateHover(e.RowIndex,e.ColumnIndex);
        CellMouseLeave += (_, _) => UpdateHover(-1,-1);
        CellMouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.RowIndex >= tasks.Count) return;
            var bounds = GetCellDisplayRectangle(e.ColumnIndex,e.RowIndex,false);
            if (e.ColumnIndex is 0 or 5 or 6 && ActionBounds(new Rectangle(0,0,bounds.Width,bounds.Height)).Contains(e.Location)) ActivateAction(e.RowIndex,e.ColumnIndex);
        };
        CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex is >= 1 and <= 4) EditRequested?.Invoke(tasks[e.RowIndex]); };
    }

    private void UpdateHover(int row, int column)
    {
        int previous = hoveredRow; hoveredRow = row; hoveredColumn = column;
        if (previous >= 0 && previous < Rows.Count) InvalidateRow(previous);
        if (row >= 0 && row < Rows.Count) InvalidateRow(row);
        Cursor = row >= 0 && column is 0 or 5 or 6 ? Cursors.Hand : Cursors.Default;
    }
    internal void ActivateAction(int row, int column)
    {
        if (row < 0 || row >= tasks.Count) return;
        if (column == 0) ToggleRequested?.Invoke(tasks[row]);
        else if (column == 5) EditRequested?.Invoke(tasks[row]);
        else if (column == 6) DeleteRequested?.Invoke(tasks[row]);
    }
    public void SetTasks(List<TaskItem> items)
    {
        Guid? selected = CurrentRow?.Tag is TaskItem task ? task.Id : null;
        int scroll = FirstDisplayedScrollingRowIndex;
        int selectedColumn = CurrentCell?.ColumnIndex ?? 1;
        tasks = items; hoveredRow = hoveredColumn = -1; Rows.Clear();
        foreach (var item in items)
        {
            int index = Rows.Add(item.IsCompleted,item.Title,item.Category,item.Priority.ToString(),item.DueDate.ToString("dd MMM yyyy"));
            var row = Rows[index]; row.Tag = item; row.Height = (int)(74*DeviceDpi/96f);
            row.Cells[1].ToolTipText = item.Title + (item.Description.Length > 0 ? "\n" + item.Description : "");
            row.Cells[2].ToolTipText = item.Category;
            row.Cells[0].ToolTipText = item.IsCompleted ? "Mark incomplete" : "Mark completed";
            row.Cells[4].ToolTipText = (item.IsOverdue(DateTime.Today) ? "Overdue — " : "") + item.DueDate.ToLongDateString();
            row.Cells[5].ToolTipText = "Edit task"; row.Cells[6].ToolTipText = "Delete task";
        }
        if (items.Count > 0)
        {
            CurrentCell = Rows[Math.Max(0,items.FindIndex(t => t.Id == selected))].Cells[selectedColumn];
            if (scroll >= 0) FirstDisplayedScrollingRowIndex = Math.Min(scroll,items.Count-1);
        }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyDpiMetrics(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); ApplyDpiMetrics(); }
    private void ApplyDpiMetrics()
    {
        if (Columns.Count != 7) return;
        float s = DeviceDpi/96f; int[] widths = [44,0,88,86,116,40,44];
        for (int i = 0; i < widths.Length; i++) if (widths[i] > 0) Columns[i].Width = (int)Math.Round(widths[i]*s);
        Columns[1].MinimumWidth = (int)(150*s); ColumnHeadersHeight = (int)(32*s);
        foreach (DataGridViewRow row in Rows) row.Height = (int)(74*s);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (CurrentRow?.Tag is TaskItem item)
        {
            if (keyData == Keys.Enter)
            {
                if (CurrentCell.ColumnIndex is 0 or 5 or 6) ActivateAction(CurrentRow.Index,CurrentCell.ColumnIndex);
                else EditRequested?.Invoke(item);
                return true;
            }
            if (keyData == Keys.Space) { ToggleRequested?.Invoke(item); return true; }
            if (keyData == Keys.Delete) { DeleteRequested?.Invoke(item); return true; }
        }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private Rectangle ActionBounds(Rectangle cell)
    {
        int size = (int)(30*DeviceDpi/96f), gap = (int)(8*DeviceDpi/96f);
        return new Rectangle(cell.X+(cell.Width-size)/2,cell.Y+(cell.Height-gap-size)/2,size,size);
    }
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.ColumnIndex < 0) return;
        var g = e.Graphics!; float s = DeviceDpi/96f;
        int px(int n) => (int)Math.Round(n*s);
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        var state = g.Save(); g.SetClip(e.CellBounds,CombineMode.Intersect);
        using (var background = new SolidBrush(Theme.Background)) g.FillRectangle(background,e.CellBounds);
        if (e.RowIndex < 0)
        {
            var header = e.CellBounds; header.Inflate(-px(10),0);
            TextRenderer.DrawText(g,e.FormattedValue?.ToString() ?? "",headerFont,header,Theme.Muted,flags);
            g.Restore(state); e.Handled = true; return;
        }
        if (e.RowIndex >= tasks.Count) { g.Restore(state); e.Handled = true; return; }
        var task = tasks[e.RowIndex];
        bool selected = CurrentRow?.Index == e.RowIndex, keyboardFocus = selected && Focused;
        bool hovered = hoveredRow == e.RowIndex;
        var row = GetRowDisplayRectangle(e.RowIndex,false);
        var surface = new RectangleF(row.X+1.5f*s,row.Y+1.5f*s,row.Width-4*s,row.Height-9*s);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Rounded(surface,9*s))
        using (var brush = new SolidBrush(keyboardFocus ? Color.FromArgb(246,249,255) : hovered ? Color.FromArgb(250,252,255) : Color.White))
        using (var pen = new Pen(keyboardFocus ? Theme.Blue : selected ? Color.FromArgb(190,208,239) : Theme.Border,keyboardFocus ? 1.5f*s : s))
        { g.FillPath(brush,path); g.DrawPath(pen,path); }
        var box = e.CellBounds; box.Inflate(-px(10),0); box.Height -= px(8);
        Color textColor = task.IsCompleted ? Theme.Muted : Theme.Ink;
        if (e.ColumnIndex == 0)
        {
            var target = ActionBounds(e.CellBounds);
            Theme.PaintCheck(g,new RectangleF(target.X+5*s,target.Y+5*s,19*s,19*s),task.IsCompleted,keyboardFocus && CurrentCell?.ColumnIndex == 0);
        }
        else if (e.ColumnIndex == 1)
        {
            bool description = !string.IsNullOrWhiteSpace(task.Description);
            TextRenderer.DrawText(g,task.Title,titleFont,new Rectangle(box.X,box.Y+px(description ? 10 : 20),box.Width,px(23)),textColor,flags);
            if (description) TextRenderer.DrawText(g,task.Description.ReplaceLineEndings(" "),detailFont,new Rectangle(box.X,box.Y+px(34),box.Width,px(22)),Theme.Muted,flags);
        }
        else if (e.ColumnIndex == 2) TextRenderer.DrawText(g,task.Category,detailFont,box,Theme.Muted,flags);
        else if (e.ColumnIndex == 3)
        {
            Color ink = task.Priority switch { TaskPriority.High => Theme.Red, TaskPriority.Medium => Color.FromArgb(143,100,28), _ => Theme.Green };
            Color fill = task.Priority switch { TaskPriority.High => Color.FromArgb(253,238,238), TaskPriority.Medium => Color.FromArgb(252,245,229), _ => Color.FromArgb(232,245,239) };
            var pill = new RectangleF(box.X,box.Y+(box.Height-25*s)/2,Math.Min(box.Width,68*s),25*s);
            using var shape = Theme.Rounded(pill,7*s); using var brush = new SolidBrush(fill); g.FillPath(brush,shape);
            TextRenderer.DrawText(g,task.Priority.ToString(),detailFont,Rectangle.Round(pill),ink,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        else if (e.ColumnIndex == 4)
        {
            bool overdue = task.IsOverdue(DateTime.Today);
            string date = task.DueDate.Date == DateTime.Today ? "Today" : task.DueDate.ToString("dd MMM yyyy");
            TextRenderer.DrawText(g,date,detailFont,new Rectangle(box.X,box.Y+px(overdue ? 10 : 20),box.Width,px(23)),overdue ? Theme.Red : textColor,flags);
            if (overdue) TextRenderer.DrawText(g,"Overdue",detailFont,new Rectangle(box.X,box.Y+px(33),box.Width,px(20)),Theme.Red,flags);
        }
        else if (e.ColumnIndex is 5 or 6)
        {
            bool actionHovered = hovered && hoveredColumn == e.ColumnIndex;
            var target = ActionBounds(e.CellBounds);
            using var path = Theme.Rounded(target,7*s);
            using var fill = new SolidBrush(actionHovered ? e.ColumnIndex == 6 ? Color.FromArgb(253,235,235) : Theme.PaleBlue : Color.FromArgb(246,248,251));
            g.FillPath(fill,path);
            Icons.Draw(g,e.ColumnIndex == 5 ? Glyph.Edit : Glyph.Delete,RectangleF.Inflate(target,-7*s,-7*s),actionHovered ? e.ColumnIndex == 6 ? Theme.Red : Theme.Blue : Theme.Muted);
            if (keyboardFocus && CurrentCell?.ColumnIndex == e.ColumnIndex) { using var pen = new Pen(Theme.Blue,1.5f*s); g.DrawPath(pen,path); }
        }
        g.Restore(state); e.Handled = true;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { titleFont.Dispose(); detailFont.Dispose(); headerFont.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class TaskActionCell : DataGridViewButtonCell
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new ActionAccessibility(this);
        private sealed class ActionAccessibility(TaskActionCell owner) : DataGridViewButtonCellAccessibleObject(owner)
        {
            public override string? Name { get => owner.ColumnIndex == 5 ? "Edit task" : "Delete task"; set { } }
            public override void DoDefaultAction() => (owner.DataGridView as TaskGrid)?.ActivateAction(owner.RowIndex,owner.ColumnIndex);
        }
    }
    private sealed class TaskCheckCell : DataGridViewCheckBoxCell
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new CheckAccessibility(this);
        private sealed class CheckAccessibility(TaskCheckCell owner) : DataGridViewCheckBoxCellAccessibleObject(owner)
        {
            public override string? Name { get => owner.Value is true ? "Mark incomplete" : "Mark completed"; set { } }
            public override void DoDefaultAction() => (owner.DataGridView as TaskGrid)?.ActivateAction(owner.RowIndex,0);
        }
    }
}
