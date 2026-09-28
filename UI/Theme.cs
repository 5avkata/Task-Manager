using System.Drawing.Drawing2D;

namespace TaskManager.UI;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(245, 247, 251);
    public static readonly Color Ink = Color.FromArgb(27, 39, 62);
    public static readonly Color Muted = Color.FromArgb(105, 118, 140);
    public static readonly Color Blue = Color.FromArgb(48, 103, 224);
    public static readonly Color PaleBlue = Color.FromArgb(234, 241, 255);
    public static readonly Color Border = Color.FromArgb(225, 231, 240);
    public static readonly Color Red = Color.FromArgb(190, 64, 64);
    public static readonly Color Green = Color.FromArgb(29, 132, 102);
    public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Label Label(string text, float size = 10, Color? color = null, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, Font = Font(size, style), ForeColor = color ?? Ink,
        AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        Margin = Padding.Empty, AutoEllipsis = true
    };
    public static RoundedComboBox Combo() => new();
    public static void PaintField(Graphics g, Rectangle bounds, float scale, bool focused, bool hovered)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var surface = Rounded(new RectangleF(1.5f*scale, 1.5f*scale, Math.Max(1,bounds.Width-3*scale), Math.Max(1,bounds.Height-3*scale)), 9*scale);
        using var white = new SolidBrush(Color.White); g.FillPath(white, surface);
        using var pen = new Pen(focused ? Blue : hovered ? Color.FromArgb(182,197,220) : Border, (focused ? 1.6f : 1)*scale);
        g.DrawPath(pen, surface);
    }
    public static void PaintCheck(Graphics g, RectangleF box, bool isChecked, bool focused)
    {
        float s = box.Width/19f; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Rounded(box, 5*s); using var brush = new SolidBrush(isChecked ? Blue : Color.White);
        using var border = new Pen(isChecked ? Blue : Color.FromArgb(173,187,207), 1.3f*s);
        g.FillPath(brush, shape); g.DrawPath(border, shape);
        if (isChecked) Icons.Draw(g, Glyph.Check, RectangleF.Inflate(box, -2*s,-2*s), Color.White);
        if (focused) { using var ring = Rounded(RectangleF.Inflate(box,3*s,3*s),7*s); using var pen = new Pen(Blue,1.4f*s); g.DrawPath(pen,ring); }
    }
    public static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class StyledButton : Button
{
    public bool Primary { get; set; }
    public bool Selected { get; set; }
    public bool Navigation { get; set; }
    private bool hovering;
    public StyledButton(string text, bool primary = false)
    {
        Text = text; Primary = primary; Height = 40; Cursor = Cursors.Hand;
        Font = Theme.Font(10, FontStyle.Bold); FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0; DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleName = text.Replace("&", "");
    }
    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var background = Navigation
            ? (Selected ? Color.FromArgb(44, 65, 102) : hovering ? Color.FromArgb(33, 49, 78) : BackColor)
            : Primary ? (hovering ? Color.FromArgb(37, 86, 194) : Theme.Blue) : hovering ? Theme.PaleBlue : Color.White;
        using var path = Theme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 9 * DeviceDpi / 96f);
        using var brush = new SolidBrush(background);
        e.Graphics.FillPath(brush, path);
        if (!Navigation && !Primary) { using var pen = new Pen(Theme.Border); e.Graphics.DrawPath(pen, path); }
        var color = !Enabled ? Theme.Muted : Primary || Navigation ? Color.White : Theme.Ink;
        TextRenderer.DrawText(e.Graphics, Text, Font,
            new Rectangle(Navigation ? 18 : 4, 0, Width - (Navigation ? 24 : 8), Height), color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (Navigation ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
        if (Focused && ShowFocusCues)
        {
            using var focus = Theme.Rounded(Rectangle.Inflate(ClientRectangle,-4,-4),6*DeviceDpi/96f);
            using var pen = new Pen(Primary || Navigation ? Color.FromArgb(190,214,255) : Theme.Blue, 1.8f*DeviceDpi/96f);
            e.Graphics.DrawPath(pen,focus);
        }
    }
}

internal sealed class StatCardRow : Panel
{
    public StatCardRow() { Dock = DockStyle.Fill; Margin = Padding.Empty; }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Controls.Count == 0) return;
        int gap = (int)Math.Round(12*DeviceDpi/96f);
        int available = Math.Max(0,ClientSize.Width-gap*(Controls.Count-1));
        int width = available/Controls.Count, remainder = available%Controls.Count, x = 0;
        for (int i = 0; i < Controls.Count; i++)
        {
            int nextWidth = width+(i < remainder ? 1 : 0);
            Controls[i].SetBounds(x,0,nextWidth,ClientSize.Height); x += nextWidth+gap;
        }
    }
}

internal sealed class StatCard : Control
{
    public string Caption { get; set; } = "";
    public int Value { get; set; }
    public Color Accent { get; set; } = Theme.Blue;
    public Glyph Icon { get; set; } = Glyph.Tasks;
    public StatCard()
    {
        DoubleBuffered = true; Dock = DockStyle.Fill; Margin = Padding.Empty;
        // The outline and right-aligned icon move when Width changes. Repaint
        // retained pixels too, not just the newly exposed strip of the control.
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 12 * s);
        using var white = new SolidBrush(Color.White); using var border = new Pen(Theme.Border);
        e.Graphics.FillPath(white, shape); e.Graphics.DrawPath(border, shape);
        using var labelFont = Theme.Font(9); using var valueFont = Theme.Font(26, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, Caption, labelFont, new Rectangle((int)(18*s), (int)(12*s), Width-(int)(58*s), (int)(22*s)), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, Value.ToString(), valueFont, new Rectangle((int)(15*s), (int)(33*s), Width-(int)(30*s), (int)(45*s)), Accent, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        Icons.Draw(e.Graphics, Icon, new RectangleF(Width-37*s,15*s,18*s,18*s),Accent);
    }
}

internal sealed class ProgressStrip : Control
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public ProgressStrip()
    {
        DoubleBuffered = true; Dock = DockStyle.Fill;
        // Clear the previous right-aligned caption and track on every resize.
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f;
        int percent = Total == 0 ? 0 : (int)Math.Round(100d * Completed / Total);
        using var font = Theme.Font(9, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, $"Overall progress   {percent}%", font, new Point(0, (int)(8*s)), Theme.Ink);
        TextRenderer.DrawText(e.Graphics, $"{Completed} of {Total} complete", Font, new Rectangle(Width/2, (int)(8*s), Width/2, (int)(24*s)), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(1, 33*s, Width - 3, 5*s);
        using var path = Theme.Rounded(track, 3*s); using var gray = new SolidBrush(Theme.Border); e.Graphics.FillPath(gray, path);
        if (percent > 0) { track.Width = Math.Max(7*s, track.Width * percent / 100); using var filled = Theme.Rounded(track, 3*s); using var blue = new SolidBrush(Theme.Blue); e.Graphics.FillPath(blue, filled); }
    }
}
