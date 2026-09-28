using System.Drawing.Drawing2D;
using System.Globalization;

namespace TaskManager.UI;

// A native text editor keeps IME, selection, clipboard and accessibility support.
// Its reusable outer surface provides padding, rounded corners and a focus ring.
internal sealed class RoundedTextBox : UserControl
{
    private readonly HintTextBox editor = new() { BorderStyle = BorderStyle.None, BackColor = Color.White, Margin = Padding.Empty };
    private bool searchIcon;
    public bool SearchIcon { get => searchIcon; set { searchIcon = value; PerformLayout(); Invalidate(); } }
    public bool Multiline { get => editor.Multiline; set { editor.Multiline = value; editor.AcceptsReturn = value; editor.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None; PerformLayout(); } }
    public int MaxLength { get => editor.MaxLength; set => editor.MaxLength = value; }
    public string PlaceholderText { get => editor.Hint; set { editor.Hint = value; editor.Invalidate(); } }
    public int SelectionStart { get => editor.SelectionStart; set => editor.SelectionStart = value; }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get => editor?.Text ?? ""; set { if (editor is not null) editor.Text = value ?? ""; } }
    public void Clear() => editor.Clear();
    public void SelectAll() => editor.SelectAll();
    public RoundedTextBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Font = Theme.Font(); BackColor = Theme.Background; Height = 40; Margin = Padding.Empty;
        TabStop = false; Controls.Add(editor);
        editor.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        editor.GotFocus += (_, _) => { editor.Invalidate(); Invalidate(); }; editor.LostFocus += (_, _) => { editor.Invalidate(); Invalidate(); };
        editor.Font = Font;
        Click += (_, _) => editor.Focus();
    }
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); editor.Focus(); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (editor is not null) { editor.Font = Font; PerformLayout(); } }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (editor is null) return;
        int s(int n) => (int)Math.Round(n * DeviceDpi / 96f);
        int x = s(SearchIcon ? 38 : 13), y = Multiline ? s(11) : Math.Max(s(5), (Height - editor.PreferredHeight) / 2);
        editor.SetBounds(x, y, Math.Max(1, Width - x - s(13)), Multiline ? Math.Max(1, Height - y * 2) : editor.PreferredHeight);
        editor.AccessibleName = AccessibleName;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.PaintField(e.Graphics, ClientRectangle, DeviceDpi / 96f, ContainsFocus, false);
        if (SearchIcon) Icons.Draw(e.Graphics, Glyph.Search, new RectangleF(13 * DeviceDpi / 96f, (Height - 17 * DeviceDpi / 96f) / 2, 17 * DeviceDpi / 96f, 17 * DeviceDpi / 96f), Theme.Muted);
    }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); PerformLayout(); }

    // Native cue banners are absent from WM_PRINT output. Paint the hint after
    // native text rendering so screenshots and the live control agree.
    private sealed class HintTextBox : TextBox
    {
        public string Hint { get; set; } = "";
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is not (0x000F or 0x0317 or 0x0318) || TextLength != 0 || Focused || Hint.Length == 0) return;
            using var g = m.Msg == 0x000F ? CreateGraphics() : m.WParam != IntPtr.Zero ? Graphics.FromHdc(m.WParam) : null;
            if (g is null) return;
            TextRenderer.DrawText(g,Hint,Font,new Rectangle(0,0,ClientSize.Width,PreferredHeight),Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
}

internal sealed class RoundedComboBox : Control
{
    private int selectedIndex = -1;
    private bool hovered;
    private ToolStripDropDown? popup;
    private ListBox? options;
    private string typeAhead = "";
    private DateTime lastTyped;
    public List<object> Items { get; } = [];
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value) { Invalidate(); return; }
            selectedIndex = value; Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public object? SelectedItem { get => selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null; set => SelectedIndex = value is null ? -1 : Items.IndexOf(value); }
    public bool IsDropDownOpen => popup?.Visible == true;
    public RoundedComboBox()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true; Font = Theme.Font(); Height = 40; BackColor = Theme.Background; Cursor = Cursors.Hand; Dock = DockStyle.Fill; Margin = Padding.Empty;
        AccessibleRole = AccessibleRole.ComboBox;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f;
        Theme.PaintField(e.Graphics, ClientRectangle, s, Focused || IsDropDownOpen, hovered);
        TextRenderer.DrawText(e.Graphics, SelectedItem?.ToString() ?? "Choose...", Font, new Rectangle((int)(13*s), 0, Math.Max(1, Width-(int)(43*s)), Height), Enabled ? Theme.Ink : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        Icons.Draw(e.Graphics, Glyph.Chevron, new RectangleF(Width-27*s, (Height-14*s)/2, 14*s, 14*s), Theme.Muted);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); OpenDropDown(); } }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is Keys.F4 or Keys.Space or Keys.Enter || keyData == (Keys.Alt | Keys.Down)) { OpenDropDown(); return true; }
        if (keyData == Keys.Escape && IsDropDownOpen) { popup!.Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Items.Count == 0) return;
        if (e.KeyCode == Keys.Down) SelectedIndex = Math.Min(Items.Count-1, SelectedIndex+1);
        else if (e.KeyCode == Keys.Up) SelectedIndex = Math.Max(0, SelectedIndex-1);
        else if (e.KeyCode == Keys.Home) SelectedIndex = 0;
        else if (e.KeyCode == Keys.End) SelectedIndex = Items.Count-1;
        else return;
        e.Handled = true; e.SuppressKeyPress = true;
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e); if (char.IsControl(e.KeyChar)) return;
        if ((DateTime.Now - lastTyped).TotalSeconds > 1) typeAhead = "";
        typeAhead += e.KeyChar; lastTyped = DateTime.Now;
        int index = Items.FindIndex(item => (item.ToString() ?? "").StartsWith(typeAhead, StringComparison.CurrentCultureIgnoreCase));
        if (index >= 0) SelectedIndex = index;
        e.Handled = true;
    }
    public void OpenDropDown()
    {
        if (!Enabled || Items.Count == 0) return;
        if (IsDropDownOpen) { popup!.Close(); return; }
        popup?.Dispose();
        int s(int n) => (int)Math.Round(n * DeviceDpi / 96f);
        options = new ListBox { BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = s(36), IntegralHeight = false, Font = Font, BackColor = Color.White, ForeColor = Theme.Ink, AccessibleName = AccessibleName + " options" };
        options.Items.AddRange(Items.ToArray()); options.SelectedIndex = SelectedIndex;
        int widest = Items.Max(item => TextRenderer.MeasureText(item.ToString(), Font).Width) + s(44);
        options.Size = new Size(Math.Max(Width-s(8), Math.Min(s(360), widest)), Math.Min(8, Items.Count) * options.ItemHeight);
        options.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(selected ? Theme.PaleBlue : Color.White); e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, options.Items[e.Index].ToString(), Font, Rectangle.Inflate(e.Bounds, -s(10), 0), selected ? Theme.Blue : Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        options.MouseMove += (_, e) => { int i = options.IndexFromPoint(e.Location); if (i >= 0) options.SelectedIndex = i; };
        options.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left && options.IndexFromPoint(e.Location) >= 0) CommitSelection(); };
        options.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { CommitSelection(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { popup!.Close(); e.Handled = true; }
            else if (e.KeyCode == Keys.Tab) { CommitSelection(); Parent?.SelectNextControl(this, !e.Shift, true, true, true); e.Handled = true; }
        };
        var host = new ToolStripControlHost(options) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = options.Size };
        popup = new RoundedPopup { Padding = new Padding(s(6)), BackColor = Color.White, DropShadowEnabled = false, AutoClose = true };
        popup.Items.Add(host);
        popup.Closed += (_, e) => { Invalidate(); if (e.CloseReason is ToolStripDropDownCloseReason.Keyboard or ToolStripDropDownCloseReason.ItemClicked) Focus(); };
        popup.Show(this, new Point(0, Height + s(4))); options.Focus(); Invalidate();
    }
    private void CommitSelection()
    {
        if (options?.SelectedIndex >= 0) SelectedIndex = options.SelectedIndex;
        popup?.Close(ToolStripDropDownCloseReason.ItemClicked);
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new PickerAccessibility(this);
    private sealed class PickerAccessibility(RoundedComboBox owner) : ControlAccessibleObject(owner)
    {
        public override string? Value { get => owner.SelectedItem?.ToString() ?? ""; set { int i = owner.Items.FindIndex(item => item.ToString() == value); if (i >= 0) owner.SelectedIndex = i; } }
        public override string DefaultAction => "Open options";
        public override AccessibleStates State => base.State | (owner.IsDropDownOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
        public override void DoDefaultAction() => owner.OpenDropDown();
    }
    protected override void Dispose(bool disposing) { if (disposing) popup?.Dispose(); base.Dispose(disposing); }
}

internal sealed class RoundedDatePicker : Control
{
    private DateTime date = DateTime.Today;
    private ToolStripDropDown? popup;
    public DateTime Value { get => date; set { date = value.Date; Invalidate(); AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1); } }
    public RoundedDatePicker()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true; Font = Theme.Font(); Height = 40; Dock = DockStyle.Fill; Cursor = Cursors.Hand; Margin = Padding.Empty;
        AccessibleRole = AccessibleRole.DropList; AccessibleDescription = "Press Enter to choose a date. Left and right change the day; Page Up and Page Down change the month; Home selects today.";
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi/96f;
        Theme.PaintField(e.Graphics, ClientRectangle, s, Focused || popup?.Visible == true, false);
        TextRenderer.DrawText(e.Graphics, Value.ToString("ddd, dd MMM yyyy", CultureInfo.GetCultureInfo("en-US")), Font, new Rectangle((int)(13*s), 0, Width-(int)(46*s), Height), Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        Icons.Draw(e.Graphics, Glyph.Calendar, new RectangleF(Width-31*s, (Height-18*s)/2, 18*s, 18*s), Theme.Muted);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); OpenCalendar(); } }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Home or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        try
        {
            DateTime next = e.KeyCode switch { Keys.Left => Value.AddDays(-1), Keys.Right => Value.AddDays(1), Keys.PageUp => Value.AddMonths(-1), Keys.PageDown => Value.AddMonths(1), Keys.Home => DateTime.Today, _ => Value };
            if (next.Year is >= 1753 and <= 9998) Value = next;
        }
        catch (ArgumentOutOfRangeException) { }
        if (e.KeyCode is Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home) { e.Handled = true; e.SuppressKeyPress = true; }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is Keys.Enter or Keys.Space or Keys.F4 || keyData == (Keys.Alt | Keys.Down)) { OpenCalendar(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    public void OpenCalendar()
    {
        if (popup?.Visible == true) { popup.Close(); return; }
        popup?.Dispose();
        var calendar = new MonthCalendar { MaxSelectionCount = 1, MinDate = new DateTime(1753, 1, 1), MaxDate = new DateTime(9998, 12, 31), AccessibleName = "Choose due date" };
        calendar.SetDate(Value);
        popup = new RoundedPopup { Padding = new Padding(6), BackColor = Color.White, DropShadowEnabled = false };
        popup.Items.Add(new ToolStripControlHost(calendar) { Margin = Padding.Empty, Padding = Padding.Empty });
        calendar.DateSelected += (_, e) => { Value = e.Start; popup.Close(ToolStripDropDownCloseReason.ItemClicked); };
        calendar.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Value = calendar.SelectionStart; popup.Close(ToolStripDropDownCloseReason.ItemClicked); e.Handled = true; }
            if (e.KeyCode == Keys.Escape) { popup.Close(ToolStripDropDownCloseReason.Keyboard); e.Handled = true; }
        };
        popup.Closed += (_, e) => { Invalidate(); if (e.CloseReason is ToolStripDropDownCloseReason.Keyboard or ToolStripDropDownCloseReason.ItemClicked) Focus(); };
        popup.Show(this, new Point(0, Height+4)); calendar.Focus(); Invalidate();
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new DateAccessibility(this);
    private sealed class DateAccessibility(RoundedDatePicker owner) : ControlAccessibleObject(owner)
    {
        public override string? Value { get => owner.Value.ToLongDateString(); set { if (DateTime.TryParse(value, out var next) && next.Year is >= 1753 and <= 9998) owner.Value = next; } }
        public override string DefaultAction => "Choose date";
        public override void DoDefaultAction() => owner.OpenCalendar();
    }
    protected override void Dispose(bool disposing) { if (disposing) popup?.Dispose(); base.Dispose(disposing); }
}

internal sealed class ModernCheckBox : CheckBox
{
    public ModernCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); AutoSize = false; Font = Theme.Font(); Height = 36; Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); float s = DeviceDpi/96f;
        var box = new RectangleF(3*s, (Height-19*s)/2, 19*s, 19*s);
        Theme.PaintCheck(e.Graphics, box, Checked, Focused);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)(31*s), 0, Width-(int)(32*s), Height), Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

internal sealed class RoundedPopup : ToolStripDropDown
{
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width < 2 || Height < 2) return;
        using var shape = Theme.Rounded(new RectangleF(0,0,Width,Height),8*DeviceDpi/96f);
        var previous = Region; Region = new Region(shape); previous?.Dispose();
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.White); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Rounded(new RectangleF(0.5f,0.5f,Width-1,Height-1),8*DeviceDpi/96f);
        using var border = new Pen(Theme.Border); e.Graphics.DrawPath(border,shape);
    }
    protected override void OnPaint(PaintEventArgs e) { }
}
