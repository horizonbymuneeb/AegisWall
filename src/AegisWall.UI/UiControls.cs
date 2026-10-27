using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

public static class Shapes
{
    public static GraphicsPath RoundRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float maxRadius = Math.Min(rect.Width, rect.Height) / 2f;
        if (radius > maxRadius) { radius = maxRadius; }
        if (radius <= 0f)
        {
            path.AddRectangle(rect);
            return path;
        }
        float diameter = radius * 2f;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Blend(Color color, Color backColor, float amount)
    {
        if (amount < 0f) { amount = 0f; }
        if (amount > 1f) { amount = 1f; }
        return Color.FromArgb(
            (int)(color.A + (backColor.A - color.A) * amount),
            (int)(color.R + (backColor.R - color.R) * amount),
            (int)(color.G + (backColor.G - color.G) * amount),
            (int)(color.B + (backColor.B - color.B) * amount));
    }
}

public static class IconRenderer
{
    private static readonly Dictionary<string, GraphicsPath> Cache = new Dictionary<string, GraphicsPath>();
    private static readonly object Gate = new object();

    private static GraphicsPath BuildPath(string data)
    {
        GraphicsPath cached;
        lock (Gate)
        {
            if (Cache.TryGetValue(data, out cached)) { return cached; }

            System.Windows.Media.Geometry geometry = System.Windows.Media.Geometry.Parse(data);
            System.Windows.Media.PathGeometry flat = geometry.GetFlattenedPathGeometry(0.01, System.Windows.Media.ToleranceType.Absolute);
            GraphicsPath path = new GraphicsPath();

            foreach (System.Windows.Media.PathFigure figure in flat.Figures)
            {
                PointF current = new PointF((float)figure.StartPoint.X, (float)figure.StartPoint.Y);
                path.StartFigure();
                foreach (System.Windows.Media.PathSegment segment in figure.Segments)
                {
                    System.Windows.Media.PolyLineSegment poly = segment as System.Windows.Media.PolyLineSegment;
                    if (poly != null)
                    {
                        foreach (System.Windows.Point point in poly.Points)
                        {
                            PointF next = new PointF((float)point.X, (float)point.Y);
                            path.AddLine(current, next);
                            current = next;
                        }
                        continue;
                    }

                    System.Windows.Media.LineSegment line = segment as System.Windows.Media.LineSegment;
                    if (line != null)
                    {
                        PointF next = new PointF((float)line.Point.X, (float)line.Point.Y);
                        path.AddLine(current, next);
                        current = next;
                    }
                }
                if (figure.IsClosed) { path.CloseFigure(); }
            }

            Cache[data] = path;
            return path;
        }
    }

    public static void Draw(Graphics graphics, string data, RectangleF box, Color color, float strokeWidth)
    {
        if (string.IsNullOrEmpty(data) || box.Width <= 0f || box.Height <= 0f) { return; }

        GraphicsPath source;
        try
        {
            source = BuildPath(data);
        }
        catch
        {
            return;
        }

        float scale = Math.Min(box.Width, box.Height) / 24f;
        using (GraphicsPath path = (GraphicsPath)source.Clone())
        using (Matrix transform = new Matrix())
        using (Pen pen = new Pen(color, Math.Max(1f, strokeWidth * scale)))
        {
            transform.Translate(box.X, box.Y);
            transform.Scale(scale, scale);
            path.Transform(transform);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawPath(pen, path);
        }
    }
}

public class ToggleSwitch : Control
{
    private bool isChecked;
    private bool hover;
    private Color onColor = Color.FromArgb(37, 99, 235);
    private Color onHoverColor = Color.FromArgb(29, 78, 216);
    private Color offColor = Color.FromArgb(203, 213, 225);
    private Color offHoverColor = Color.FromArgb(148, 163, 184);
    private Color knobColor = Color.White;

    public event EventHandler CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(46, 26);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        TabStop = false;
    }

    public bool Checked
    {
        get { return isChecked; }
        set
        {
            if (isChecked != value)
            {
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) { CheckedChanged(this, EventArgs.Empty); }
            }
        }
    }

    public Color OnColor
    {
        get { return onColor; }
        set { onColor = value; Invalidate(); }
    }

    public Color OnHoverColor
    {
        get { return onHoverColor; }
        set { onHoverColor = value; Invalidate(); }
    }

    public Color OffColor
    {
        get { return offColor; }
        set { offColor = value; Invalidate(); }
    }

    public Color OffHoverColor
    {
        get { return offHoverColor; }
        set { offHoverColor = value; Invalidate(); }
    }

    public Color KnobColor
    {
        get { return knobColor; }
        set { knobColor = value; Invalidate(); }
    }

    protected override void OnClick(EventArgs e)
    {
        if (!Enabled) { return; }
        Checked = !isChecked;
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 4 || Height <= 4) { return; }

        float trackWidth = Width - 4f;
        float trackHeight = Height - 4f;
        float x = 2f;
        float y = 2f;

        Color track = !Enabled
            ? Color.FromArgb(226, 232, 240)
            : (isChecked ? (hover ? onHoverColor : onColor) : (hover ? offHoverColor : offColor));

        using (GraphicsPath path = Shapes.RoundRect(new RectangleF(x, y, trackWidth, trackHeight), trackHeight / 2f))
        using (SolidBrush brush = new SolidBrush(track))
        {
            e.Graphics.FillPath(brush, path);
        }

        float knobSize = trackHeight - 6f;
        float knobX = isChecked ? (x + trackWidth - knobSize - 3f) : (x + 3f);
        Color knob = Enabled ? knobColor : Color.FromArgb(248, 250, 252);
        using (SolidBrush knobBrush = new SolidBrush(knob))
        using (Pen outline = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
        {
            RectangleF knobRect = new RectangleF(knobX, y + 3f, knobSize, knobSize);
            e.Graphics.FillEllipse(knobBrush, knobRect);
            e.Graphics.DrawEllipse(outline, knobRect);
        }
    }
}

public class CardPanel : Panel
{
    private int cornerRadius = 14;
    private Color fillColor = Color.White;
    private Color borderColor = Color.FromArgb(229, 231, 235);

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public int CornerRadius
    {
        get { return cornerRadius; }
        set { cornerRadius = value; Invalidate(); }
    }

    public Color FillColor
    {
        get { return fillColor; }
        set { fillColor = value; Invalidate(); }
    }

    public Color BorderColor
    {
        get { return borderColor; }
        set { borderColor = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 0 || Height <= 0) { return; }
        RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (GraphicsPath path = Shapes.RoundRect(rect, cornerRadius))
        using (SolidBrush brush = new SolidBrush(fillColor))
        using (Pen pen = new Pen(borderColor, 1f))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

public class ProgressLine : Control
{
    private int progressValue;
    private bool marquee;
    private int marqueePos;

    public ProgressLine()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 4;
    }

    public int Value
    {
        get { return progressValue; }
        set
        {
            int v = value;
            if (v < 0) { v = 0; }
            if (v > 100) { v = 100; }
            progressValue = v;
            Invalidate();
        }
    }

    public bool Marquee
    {
        get { return marquee; }
        set { marquee = value; Invalidate(); }
    }

    public void Step()
    {
        marqueePos = (marqueePos + 4) % 100;
        if (marquee) { Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 0 || Height <= 0) { return; }
        Rectangle track = new Rectangle(0, 0, Width, Height);
        using (GraphicsPath path = Shapes.RoundRect(track, Height / 2f))
        using (SolidBrush brush = new SolidBrush(Color.FromArgb(229, 231, 235)))
        {
            e.Graphics.FillPath(brush, path);
        }

        if (marquee)
        {
            int segment = Math.Max(60, Width / 5);
            int x = (int)((Width + segment) * (marqueePos / 100.0)) - segment;
            Rectangle fill = new Rectangle(x, 0, segment, Height);
            GraphicsPath clip = Shapes.RoundRect(track, Height / 2f);
            GraphicsState state = e.Graphics.Save();
            e.Graphics.SetClip(clip);
            using (GraphicsPath fillPath = Shapes.RoundRect(fill, Height / 2f))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(37, 99, 235)))
            {
                e.Graphics.FillPath(brush, fillPath);
            }
            e.Graphics.Restore(state);
            clip.Dispose();
        }
        else if (progressValue > 0)
        {
            int width = (int)(Width * (progressValue / 100.0));
            if (width > 0)
            {
                Rectangle fill = new Rectangle(0, 0, width, Height);
                using (GraphicsPath path = Shapes.RoundRect(fill, Height / 2f))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
        }
    }
}

public class NavButton : Control
{
    private bool active;
    private bool hover;
    private string glyphData = "";

    public NavButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 40;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.75f);
        TabStop = false;
    }

    public string GlyphData
    {
        get { return glyphData; }
        set { glyphData = value; Invalidate(); }
    }

    public bool Active
    {
        get { return active; }
        set
        {
            if (active != value)
            {
                active = value;
                Invalidate();
            }
        }
    }

    public Color ActiveBackColor = Color.FromArgb(239, 246, 255);
    public Color HoverBackColor = Color.FromArgb(243, 244, 246);
    public Color ActiveForeColor = Color.FromArgb(29, 78, 216);
    public Color NormalForeColor = Color.FromArgb(71, 85, 105);
    public Color ActiveIconColor = Color.FromArgb(37, 99, 235);
    public Color NormalIconColor = Color.FromArgb(100, 116, 139);

    protected override void OnMouseEnter(EventArgs e)
    {
        hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 0 || Height <= 0) { return; }

        Color foreground = !Enabled ? Color.FromArgb(148, 163, 175) : (active ? ActiveForeColor : NormalForeColor);
        Color iconColor = !Enabled ? Color.FromArgb(148, 163, 175) : (active ? ActiveIconColor : NormalIconColor);

        if (active || (hover && Enabled))
        {
            using (GraphicsPath path = Shapes.RoundRect(new RectangleF(0f, 0f, Width, Height), 9f))
            using (SolidBrush brush = new SolidBrush(active ? ActiveBackColor : HoverBackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        if (active)
        {
            using (GraphicsPath path = Shapes.RoundRect(new RectangleF(0f, (Height - 20f) / 2f, 3f, 20f), 1.5f))
            using (SolidBrush brush = new SolidBrush(ActiveIconColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        float iconSize = 18f;
        IconRenderer.Draw(e.Graphics, glyphData, new RectangleF(16f, (Height - iconSize) / 2f, iconSize, iconSize), iconColor, 2f);

        int textLeft = 44;
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft - 10), Height), foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

public class FlatButton : Control
{
    private bool hover;
    private bool pressed;
    private string glyphData = "";
    private int cornerRadius = 9;

    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.FromArgb(37, 99, 235);
        ForeColor = Color.White;
        HoverColor = Color.FromArgb(29, 78, 216);
        PressedColor = Color.FromArgb(30, 64, 175);
        BorderColor = Color.Transparent;
        Font = new Font("Segoe UI", 9f);
        Cursor = Cursors.Hand;
        Height = 36;
        TabStop = false;
    }

    public string GlyphData
    {
        get { return glyphData; }
        set { glyphData = value; Invalidate(); }
    }

    public int CornerRadius
    {
        get { return cornerRadius; }
        set { cornerRadius = value; Invalidate(); }
    }

    public Color HoverColor { get; set; }
    public Color PressedColor { get; set; }
    public Color BorderColor { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 0 || Height <= 0) { return; }

        Color fill;
        Color foreground;
        Color border;
        if (!Enabled)
        {
            fill = Color.FromArgb(243, 244, 246);
            foreground = Color.FromArgb(148, 163, 175);
            border = Color.FromArgb(229, 231, 235);
        }
        else
        {
            fill = pressed ? PressedColor : (hover ? HoverColor : BackColor);
            foreground = ForeColor;
            border = BorderColor;
        }

        RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (GraphicsPath path = Shapes.RoundRect(rect, cornerRadius))
        using (SolidBrush brush = new SolidBrush(fill))
        {
            e.Graphics.FillPath(brush, path);
            if (border.A > 0)
            {
                using (Pen pen = new Pen(border, 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        int iconSize = 16;
        int gap = 8;
        bool hasIcon = !string.IsNullOrEmpty(glyphData);
        Size textSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int contentWidth = textSize.Width + (hasIcon ? iconSize + gap : 0);
        int startX = Math.Max(6, (Width - contentWidth) / 2);

        if (hasIcon)
        {
            IconRenderer.Draw(e.Graphics, glyphData, new RectangleF(startX, (Height - iconSize) / 2f, iconSize, iconSize), foreground, 2f);
            startX += iconSize + gap;
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(startX, 0, Math.Max(0, Width - startX - 4), Height), foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

public class IconBox : Control
{
    private string glyphData = "";
    private Color boxColor = Color.FromArgb(239, 246, 255);
    private Color iconColor = Color.FromArgb(37, 99, 235);
    private int cornerRadius = 9;
    private float iconInset = 8f;

    public IconBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(34, 34);
    }

    public string GlyphData
    {
        get { return glyphData; }
        set { glyphData = value; Invalidate(); }
    }

    public Color BoxColor
    {
        get { return boxColor; }
        set { boxColor = value; Invalidate(); }
    }

    public Color IconColor
    {
        get { return iconColor; }
        set { iconColor = value; Invalidate(); }
    }

    public int CornerRadius
    {
        get { return cornerRadius; }
        set { cornerRadius = value; Invalidate(); }
    }

    public float IconInset
    {
        get { return iconInset; }
        set { iconInset = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 0 || Height <= 0) { return; }

        if (boxColor.A > 0)
        {
            using (GraphicsPath path = Shapes.RoundRect(new RectangleF(0f, 0f, Width, Height), cornerRadius))
            using (SolidBrush brush = new SolidBrush(boxColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        float inset = Math.Max(0f, iconInset);
        float size = Math.Max(0f, Math.Min(Width, Height) - inset * 2f);
        IconRenderer.Draw(e.Graphics, glyphData, new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size), iconColor, 2f);
    }
}

public class Pill : Control
{
    private Color fillColor = Color.FromArgb(239, 246, 255);
    private Color textColor = Color.FromArgb(29, 78, 216);
    private Color dotColor = Color.Transparent;
    private string glyphData = "";
    private Color glyphColor = Color.Transparent;
    private int horizontalPadding = 9;

    public Pill()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        Height = 22;
    }

    public Color FillColor
    {
        get { return fillColor; }
        set { fillColor = value; Invalidate(); }
    }

    public Color TextColor
    {
        get { return textColor; }
        set { textColor = value; Invalidate(); }
    }

    public Color DotColor
    {
        get { return dotColor; }
        set { dotColor = value; Recalculate(); }
    }

    public string GlyphData
    {
        get { return glyphData; }
        set { glyphData = value; Recalculate(); }
    }

    public Color GlyphColor
    {
        get { return glyphColor; }
        set { glyphColor = value; Recalculate(); }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size textSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int leading = HasLeading() ? 13 : 0;
        return new Size(textSize.Width + horizontalPadding * 2 + leading, 22);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Recalculate();
    }

    private bool HasLeading()
    {
        return !string.IsNullOrEmpty(glyphData) || dotColor.A > 0;
    }

    private void Recalculate()
    {
        Size = GetPreferredSize(Size.Empty);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width <= 0 || Height <= 0) { return; }

        using (GraphicsPath path = Shapes.RoundRect(new RectangleF(0f, 0f, Width, Height), Height / 2f))
        using (SolidBrush brush = new SolidBrush(fillColor))
        {
            e.Graphics.FillPath(brush, path);
        }

        Size textSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int leading = HasLeading() ? 13 : 0;
        int contentWidth = textSize.Width + leading;
        int startX = Math.Max(horizontalPadding, (Width - contentWidth) / 2);

        if (!string.IsNullOrEmpty(glyphData))
        {
            IconRenderer.Draw(e.Graphics, glyphData, new RectangleF(startX - 1f, (Height - 12f) / 2f, 12f, 12f), glyphColor, 2f);
        }
        else if (dotColor.A > 0)
        {
            using (SolidBrush dot = new SolidBrush(dotColor))
            {
                e.Graphics.FillEllipse(dot, startX, (Height - 6f) / 2f, 6f, 6f);
            }
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(startX + leading, 0, Math.Max(0, Width - startX - leading - 4), Height), textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

public static class NativeInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    public static double TotalMemoryGB()
    {
        MEMORYSTATUSEX status = new MEMORYSTATUSEX();
        status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        if (GlobalMemoryStatusEx(ref status))
        {
            return status.ullTotalPhys / 1073741824.0;
        }
        return 0.0;
    }
}
