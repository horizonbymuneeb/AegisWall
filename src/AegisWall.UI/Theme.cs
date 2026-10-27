using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AegisWall.UI
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(246, 247, 249);
        public static readonly Color Sidebar = Color.White;
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(229, 231, 235);
        public static readonly Color BorderStrong = Color.FromArgb(209, 213, 219);
        public static readonly Color Text = Color.FromArgb(17, 24, 39);
        public static readonly Color Strong = Color.FromArgb(15, 23, 42);
        public static readonly Color Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color Faint = Color.FromArgb(156, 163, 175);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentPressed = Color.FromArgb(30, 64, 175);
        public static readonly Color AccentSoft = Color.FromArgb(239, 246, 255);
        public static readonly Color Green = Color.FromArgb(21, 128, 61);
        public static readonly Color GreenSoft = Color.FromArgb(240, 253, 244);
        public static readonly Color Orange = Color.FromArgb(180, 83, 9);
        public static readonly Color OrangeSoft = Color.FromArgb(255, 247, 237);
        public static readonly Color Red = Color.FromArgb(220, 38, 38);
        public static readonly Color RedSoft = Color.FromArgb(254, 242, 242);
        public static readonly Color GraySoft = Color.FromArgb(243, 244, 246);

        public static System.Drawing.Font UiFont(float size)
        {
            return new System.Drawing.Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static System.Drawing.Font UiFont(float size, FontStyle style)
        {
            return new System.Drawing.Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static void StyleInput(TextBox box)
        {
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.White;
            box.ForeColor = Text;
            box.Font = UiFont(9.5f);
        }
    }

    public static class Icons
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex PathPattern = new Regex("\\sd=\"([^\"]+)\"", RegexOptions.Compiled);
        private static string iconDirectory;

        public static void Initialize(string directory)
        {
            iconDirectory = directory;
        }

        public static string Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            string cached;
            if (Cache.TryGetValue(name, out cached))
            {
                return cached;
            }

            string result = string.Empty;
            try
            {
                string path = Path.Combine(iconDirectory ?? string.Empty, name + ".svg");
                if (File.Exists(path))
                {
                    string svg = File.ReadAllText(path);
                    MatchCollection matches = PathPattern.Matches(svg);
                    List<string> parts = new List<string>();
                    foreach (Match match in matches)
                    {
                        parts.Add(match.Groups[1].Value);
                    }
                    result = string.Join(" ", parts.ToArray());
                }
            }
            catch
            {
                result = string.Empty;
            }

            Cache[name] = result;
            return result;
        }
    }
}
