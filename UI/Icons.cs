using System.Drawing.Drawing2D;

namespace TaskManager.UI;

internal enum Glyph { Search, Chevron, Edit, Delete, Calendar, Check, Tasks, Clock }

internal static class Icons
{
    // Small vector glyphs stay crisp at different monitor scales.
    public static void Draw(Graphics graphics, Glyph glyph, RectangleF bounds, Color color)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y); graphics.ScaleTransform(bounds.Width/24f, bounds.Height/24f);
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (glyph)
        {
            case Glyph.Search: graphics.DrawEllipse(pen, 3, 3, 12, 12); graphics.DrawLine(pen, 14, 14, 21, 21); break;
            case Glyph.Chevron: graphics.DrawLines(pen, [new PointF(6,9), new PointF(12,15), new PointF(18,9)]); break;
            case Glyph.Check: graphics.DrawLines(pen, [new PointF(5,12), new PointF(10,17), new PointF(19,7)]); break;
            case Glyph.Edit:
                graphics.DrawPolygon(pen, [new PointF(5,15), new PointF(16,4), new PointF(20,8), new PointF(9,19), new PointF(4,20)]);
                graphics.DrawLine(pen, 14,6,18,10); break;
            case Glyph.Delete:
                graphics.DrawLine(pen, 4,6,20,6); graphics.DrawLine(pen, 9,3,15,3);
                graphics.DrawLines(pen, [new PointF(6,6),new PointF(7,21),new PointF(17,21),new PointF(18,6)]);
                graphics.DrawLine(pen,10,10,10,17); graphics.DrawLine(pen,14,10,14,17); break;
            case Glyph.Calendar:
                graphics.DrawRectangle(pen, 3,5,18,16); graphics.DrawLine(pen,3,10,21,10); graphics.DrawLine(pen,8,2,8,7); graphics.DrawLine(pen,16,2,16,7);
                graphics.DrawLine(pen,8,15,10,15); graphics.DrawLine(pen,14,15,16,15); break;
            case Glyph.Clock: graphics.DrawEllipse(pen,3,3,18,18); graphics.DrawLines(pen, [new PointF(12,7),new PointF(12,12),new PointF(16,14)]); break;
            case Glyph.Tasks:
                graphics.DrawRectangle(pen,4,3,16,18); graphics.DrawLine(pen,8,8,16,8); graphics.DrawLine(pen,8,12,16,12); graphics.DrawLine(pen,8,16,13,16); break;
        }
        graphics.Restore(state);
    }
}
