using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace AegisWall.UI
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool selfTest = false;
            string page = "dashboard";
            string output = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--selftest", StringComparison.OrdinalIgnoreCase)) { selfTest = true; }
                else if (string.Equals(args[i], "--page", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { page = args[++i]; }
                else if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { output = args[++i]; }
            }

            string iconDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons");
            if (!Directory.Exists(iconDirectory))
            {
                iconDirectory = Path.Combine(Environment.CurrentDirectory, "assets", "icons");
            }
            Icons.Initialize(iconDirectory);

            if (selfTest)
            {
                RunSelfTest(page, output);
                return;
            }

            Application.Run(new MainForm(false, page));
        }

        private static void RunSelfTest(string page, string output)
        {
            MainForm form = new MainForm(true, page);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(40, 40);
            form.TopMost = true;
            form.Shown += delegate
            {
                Timer timer = new Timer();
                timer.Interval = 1600;
                timer.Tick += delegate
                {
                    timer.Stop();
                    timer.Dispose();
                    try
                    {
                        form.Refresh();
                        Application.DoEvents();
                        Rectangle bounds = new Rectangle(form.PointToScreen(Point.Empty), form.ClientSize);
                        using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                        {
                            using (Graphics graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                            }

                            string path = output;
                            if (string.IsNullOrEmpty(path))
                            {
                                string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
                                path = Path.Combine(assets, string.Equals(page, "dashboard", StringComparison.OrdinalIgnoreCase)
                                    ? "screenshot.png"
                                    : "screenshot-" + page + ".png");
                            }
                            string directory = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
                            bitmap.Save(path, ImageFormat.Png);
                            Console.WriteLine("Screenshot: " + path);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Screenshot failed: " + ex.Message);
                    }

                    form.Close();
                };
                timer.Start();
            };

            Application.Run(form);
        }
    }
}
