using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace AegisWall.UI
{
    public class FlatTextBox : Control
    {
        private readonly TextBox inner;
        private bool hover;
        public new event EventHandler TextChanged;

        public FlatTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 34;
            TabStop = false;

            inner = new TextBox();
            inner.BorderStyle = BorderStyle.None;
            inner.BackColor = Color.White;
            inner.ForeColor = Theme.Text;
            inner.Font = Theme.UiFont(9.5f);
            inner.TabStop = true;
            inner.TextChanged += delegate
            {
                Invalidate();
                if (TextChanged != null) { TextChanged(this, EventArgs.Empty); }
            };
            inner.GotFocus += delegate { Invalidate(); };
            inner.LostFocus += delegate { Invalidate(); };
            Controls.Add(inner);
        }

        public new string Text
        {
            get { return inner.Text; }
            set { inner.Text = value ?? string.Empty; }
        }

        public TextBox Inner
        {
            get { return inner; }
        }

        public bool ReadOnly
        {
            get { return inner.ReadOnly; }
            set { inner.ReadOnly = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutInner();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (inner != null) { inner.Font = Font; LayoutInner(); }
        }

        private void LayoutInner()
        {
            if (inner == null) { return; }
            int height = inner.PreferredHeight;
            inner.SetBounds(12, Math.Max(0, (Height - height) / 2), Math.Max(0, Width - 24), height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width <= 0 || Height <= 0) { return; }

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = Enabled ? Color.White : Theme.GraySoft;
            Color border = !Enabled ? Theme.Border : (inner.Focused ? Theme.Accent : (hover ? Theme.BorderStrong : Theme.Border));
            using (GraphicsPath path = Shapes.RoundRect(rect, 9f))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            inner.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (inner != null)
            {
                inner.Enabled = Enabled;
                inner.BackColor = Enabled ? Color.White : Theme.GraySoft;
            }
            Invalidate();
        }
    }

    public class SelectBox : Control
    {
        private readonly List<string> items = new List<string>();
        private int selectedIndex = -1;
        private bool hover;
        private ToolStripDropDown dropDown;
        private ListBox listBox;

        public event EventHandler SelectedIndexChanged;

        public SelectBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 34;
            Font = Theme.UiFont(9.5f);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public List<string> Items
        {
            get { return items; }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                int next = value;
                if (next < -1) { next = -1; }
                if (next >= items.Count) { next = items.Count - 1; }
                if (selectedIndex != next)
                {
                    selectedIndex = next;
                    Invalidate();
                    if (SelectedIndexChanged != null) { SelectedIndexChanged(this, EventArgs.Empty); }
                }
            }
        }

        public string SelectedItem
        {
            get { return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : string.Empty; }
            set
            {
                int index = items.IndexOf(value);
                SelectedIndex = index;
            }
        }

        public string SelectedText
        {
            get { return SelectedItem; }
        }

        public void Add(string item)
        {
            items.Add(item);
            if (selectedIndex < 0) { selectedIndex = 0; }
            Invalidate();
        }

        public void Clear()
        {
            items.Clear();
            selectedIndex = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width <= 0 || Height <= 0) { return; }

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = Enabled ? Color.White : Theme.GraySoft;
            Color border = !Enabled ? Theme.Border : (dropDown != null && dropDown.Visible ? Theme.Accent : (hover ? Theme.BorderStrong : Theme.Border));
            using (GraphicsPath path = Shapes.RoundRect(rect, 9f))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            string text = SelectedItem;
            if (string.IsNullOrEmpty(text)) { text = "Select..."; }
            Color textColor = string.IsNullOrEmpty(SelectedItem) ? Theme.Faint : (Enabled ? Theme.Text : Theme.Faint);
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(12, 0, Width - 44, Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            using (Pen chevron = new Pen(Enabled ? Theme.Muted : Theme.Faint, 1.6f))
            {
                chevron.StartCap = LineCap.Round;
                chevron.EndCap = LineCap.Round;
                float x = Width - 22f;
                float y = Height / 2f;
                e.Graphics.DrawLines(chevron, new PointF[] { new PointF(x - 4f, y - 2f), new PointF(x, y + 2f), new PointF(x + 4f, y - 2f) });
            }
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Enabled) { ShowDropDown(); }
            base.OnMouseDown(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        private void ShowDropDown()
        {
            if (items.Count == 0) { return; }
            if (dropDown == null)
            {
                dropDown = new ToolStripDropDown();
                dropDown.AutoSize = false;
                dropDown.Padding = new Padding(0);
                dropDown.BackColor = Color.White;
                dropDown.DropShadowEnabled = true;
                dropDown.Closed += delegate { Invalidate(); };

                listBox = new ListBox();
                listBox.BorderStyle = BorderStyle.None;
                listBox.DrawMode = DrawMode.OwnerDrawFixed;
                listBox.ItemHeight = 30;
                listBox.Font = Theme.UiFont(9.5f);
                listBox.BackColor = Color.White;
                listBox.ForeColor = Theme.Text;
                listBox.DrawItem += DrawItem;
                listBox.MouseClick += delegate
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        SelectedIndex = listBox.SelectedIndex;
                        dropDown.Close();
                    }
                };
                listBox.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter && listBox.SelectedIndex >= 0)
                    {
                        SelectedIndex = listBox.SelectedIndex;
                        dropDown.Close();
                    }
                    else if (e.KeyCode == Keys.Escape)
                    {
                        dropDown.Close();
                    }
                };

                ToolStripControlHost host = new ToolStripControlHost(listBox);
                host.Margin = Padding.Empty;
                host.Padding = Padding.Empty;
                host.AutoSize = false;
                dropDown.Items.Add(host);
            }

            listBox.BeginUpdate();
            listBox.Items.Clear();
            foreach (string item in items) { listBox.Items.Add(item); }
            listBox.EndUpdate();

            int height = Math.Min(270, Math.Max(34, (items.Count * 30) + 4));
            listBox.Size = new Size(Math.Max(120, Width - 2), height);
            dropDown.Size = new Size(Math.Max(120, Width), height + 2);
            listBox.SelectedIndex = selectedIndex;
            dropDown.Show(this, new Point(0, Height + 4));
            listBox.Focus();
        }

        private void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) { return; }
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Theme.AccentSoft : (e.Index == selectedIndex ? Theme.GraySoft : Color.White);
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, Convert.ToString(listBox.Items[e.Index], CultureInfo.InvariantCulture),
                Theme.UiFont(9.5f), new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
                selected ? Theme.Accent : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (dropDown != null) { dropDown.Dispose(); dropDown = null; }
                listBox = null;
            }
            base.Dispose(disposing);
        }
    }

    public class NumberBox : Control
    {
        private readonly TextBox inner;
        private decimal value;
        private decimal minimum;
        private decimal maximum = 100;
        private decimal increment = 1;
        private bool hover;

        public event EventHandler ValueChanged;

        public NumberBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 34;
            TabStop = false;

            inner = new TextBox();
            inner.BorderStyle = BorderStyle.None;
            inner.BackColor = Color.White;
            inner.ForeColor = Theme.Text;
            inner.Font = Theme.UiFont(9.5f);
            inner.TabStop = true;
            inner.TextAlign = HorizontalAlignment.Left;
            inner.TextChanged += delegate
            {
                decimal parsed;
                if (decimal.TryParse(inner.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                {
                    decimal next = Clamp(parsed);
                    if (next != value)
                    {
                        value = next;
                        if (ValueChanged != null) { ValueChanged(this, EventArgs.Empty); }
                    }
                }
                Invalidate();
            };
            inner.LostFocus += delegate { inner.Text = Format(value); };
            inner.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Up) { Value += increment; e.Handled = true; }
                else if (e.KeyCode == Keys.Down) { Value -= increment; e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { inner.Text = Format(value); e.Handled = true; }
            };
            Controls.Add(inner);
        }

        public decimal Value
        {
            get { return value; }
            set
            {
                decimal next = Clamp(value);
                if (this.value == next && inner.Text == Format(next)) { return; }
                this.value = next;
                inner.Text = Format(next);
                Invalidate();
                if (ValueChanged != null) { ValueChanged(this, EventArgs.Empty); }
            }
        }

        public decimal Minimum
        {
            get { return minimum; }
            set { minimum = value; if (maximum < minimum) { maximum = minimum; } Value = this.value; }
        }

        public decimal Maximum
        {
            get { return maximum; }
            set { maximum = value; if (minimum > maximum) { minimum = maximum; } Value = this.value; }
        }

        public decimal Increment
        {
            get { return increment; }
            set { increment = value <= 0 ? 1 : value; }
        }

        private decimal Clamp(decimal input)
        {
            if (input < minimum) { return minimum; }
            if (input > maximum) { return maximum; }
            return input;
        }

        private static string Format(decimal input)
        {
            return input.ToString("0", CultureInfo.InvariantCulture);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (inner != null)
            {
                int height = inner.PreferredHeight;
                inner.SetBounds(12, Math.Max(0, (Height - height) / 2), Math.Max(0, Width - 54), height);
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (inner != null) { inner.Font = Font; OnResize(EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width <= 0 || Height <= 0) { return; }

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = Enabled ? Color.White : Theme.GraySoft;
            Color border = !Enabled ? Theme.Border : (inner.Focused ? Theme.Accent : (hover ? Theme.BorderStrong : Theme.Border));
            using (GraphicsPath path = Shapes.RoundRect(rect, 9f))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            using (Pen divider = new Pen(Theme.Border, 1f))
            {
                e.Graphics.DrawLine(divider, Width - 38, 7, Width - 38, Height - 8);
            }

            using (Pen chevron = new Pen(Enabled ? Theme.Muted : Theme.Faint, 1.6f))
            {
                chevron.StartCap = LineCap.Round;
                chevron.EndCap = LineCap.Round;
                float x = Width - 19f;
                e.Graphics.DrawLines(chevron, new PointF[] { new PointF(x - 4f, 12f), new PointF(x, 8f), new PointF(x + 4f, 12f) });
                e.Graphics.DrawLines(chevron, new PointF[] { new PointF(x - 4f, 22f), new PointF(x, 26f), new PointF(x + 4f, 22f) });
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Enabled && e.X >= Width - 38)
            {
                if (e.Y < Height / 2) { Value += increment; }
                else { Value -= increment; }
            }
            else
            {
                inner.Focus();
            }
            base.OnMouseDown(e);
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
            if (inner != null)
            {
                inner.Enabled = Enabled;
                inner.BackColor = Enabled ? Color.White : Theme.GraySoft;
            }
            Invalidate();
        }
    }
}
