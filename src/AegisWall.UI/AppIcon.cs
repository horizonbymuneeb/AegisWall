using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AegisWall.UI
{
    public class AppIcon : Control
    {
        private Image image;
        private string appPath = string.Empty;
        private Color boxColor;
        private Color fallbackColor;

        public AppIcon()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(30, 30);
            boxColor = Theme.AccentSoft;
            fallbackColor = Theme.Accent;
        }

        public string AppPath
        {
            get { return appPath; }
            set
            {
                appPath = value ?? string.Empty;
                LoadImage();
                Invalidate();
            }
        }

        public void SetEnabledState(bool enabled)
        {
            boxColor = enabled ? Theme.AccentSoft : Theme.GraySoft;
            fallbackColor = enabled ? Theme.Accent : Theme.Faint;
            Invalidate();
        }

        private void LoadImage()
        {
            if (image != null)
            {
                image.Dispose();
                image = null;
            }

            try
            {
                if (!string.IsNullOrEmpty(appPath) && File.Exists(appPath))
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(appPath))
                    {
                        if (icon != null)
                        {
                            image = icon.ToBitmap();
                        }
                    }
                }
            }
            catch
            {
                image = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width <= 0 || Height <= 0) { return; }

            using (GraphicsPath path = Shapes.RoundRect(new RectangleF(0f, 0f, Width, Height), 8f))
            using (SolidBrush brush = new SolidBrush(boxColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            if (image != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.DrawImage(image, new Rectangle(5, 5, Math.Max(1, Width - 10), Math.Max(1, Height - 10)));
            }
            else
            {
                IconRenderer.Draw(e.Graphics, Icons.Get("package"), new RectangleF(7f, 7f, Math.Max(1f, Width - 14f), Math.Max(1f, Height - 14f)), fallbackColor, 2f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && image != null)
            {
                image.Dispose();
                image = null;
            }
            base.Dispose(disposing);
        }
    }
}
