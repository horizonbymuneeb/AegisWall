using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AegisWall.Core;

namespace AegisWall.UI
{
    public sealed class InstalledAppsForm : Form
    {
        private readonly List<InstalledApp> apps = new List<InstalledApp>();
        private readonly HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Panel rows;
        private FlatTextBox search;
        private SelectBox filter;
        private Label countLabel;
        private Label selectedLabel;
        private FlatButton allowButton;
        private string searchText = string.Empty;
        private string filterText = "All sources";

        public string ResultPath { get; private set; }

        public InstalledAppsForm()
        {
            Text = "Discover installed apps";
            ClientSize = new Size(860, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            Font = Theme.UiFont(9f);
            AutoScaleMode = AutoScaleMode.None;
            string iconDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons");
            if (Directory.Exists(iconDirectory)) { Icons.Initialize(iconDirectory); }

            Label title = new Label();
            title.Text = "Discover installed apps";
            title.Font = Theme.UiFont(17f, FontStyle.Bold);
            title.ForeColor = Theme.Strong;
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Scanned from Start Menu shortcuts, uninstall registry keys and App Paths. Select the apps you want to allow.";
            subtitle.Font = Theme.UiFont(8.5f);
            subtitle.ForeColor = Theme.Muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(26, 52);
            Controls.Add(subtitle);

            search = new FlatTextBox();
            search.Location = new Point(24, 84);
            search.Size = new Size(350, 34);
            search.TextChanged += delegate
            {
                searchText = search.Text;
                RebuildRows();
            };
            Controls.Add(search);

            filter = new SelectBox();
            filter.Font = Theme.UiFont(9f);
            filter.Items.Add("All sources");
            filter.Items.Add("Start Menu");
            filter.Items.Add("Registry");
            filter.Items.Add("App Paths");
            filter.Location = new Point(386, 84);
            filter.Size = new Size(170, 34);
            filter.SelectedIndex = 0;
            filter.SelectedIndexChanged += delegate
            {
                filterText = filter.SelectedItem;
                RebuildRows();
            };
            Controls.Add(filter);

            countLabel = new Label();
            countLabel.Text = "Scanning...";
            countLabel.Font = Theme.UiFont(8f);
            countLabel.ForeColor = Theme.Muted;
            countLabel.AutoSize = true;
            countLabel.Location = new Point(576, 92);
            Controls.Add(countLabel);

            rows = new Panel();
            rows.Location = new Point(24, 132);
            rows.Size = new Size(812, 384);
            rows.AutoScroll = true;
            rows.BackColor = Color.White;
            rows.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, rows.Width - 1, rows.Height - 1);
                }
            };
            Controls.Add(rows);

            selectedLabel = new Label();
            selectedLabel.Text = "0 selected";
            selectedLabel.Font = Theme.UiFont(8.5f, FontStyle.Bold);
            selectedLabel.ForeColor = Theme.Muted;
            selectedLabel.AutoSize = true;
            selectedLabel.Location = new Point(24, 536);
            Controls.Add(selectedLabel);

            allowButton = new FlatButton();
            allowButton.Text = "Allow selected";
            allowButton.GlyphData = Icons.Get("check");
            allowButton.BackColor = Theme.Accent;
            allowButton.ForeColor = Color.White;
            allowButton.HoverColor = Theme.AccentHover;
            allowButton.PressedColor = Theme.AccentPressed;
            allowButton.BorderColor = Color.Transparent;
            allowButton.Font = Theme.UiFont(9f, FontStyle.Bold);
            allowButton.Size = new Size(180, 40);
            allowButton.Location = new Point(540, 528);
            allowButton.Enabled = false;
            allowButton.Click += delegate { AllowSelected(); };
            Controls.Add(allowButton);

            FlatButton cancel = new FlatButton();
            cancel.Text = "Cancel";
            cancel.BackColor = Color.White;
            cancel.ForeColor = Theme.Text;
            cancel.HoverColor = Theme.GraySoft;
            cancel.PressedColor = Theme.Border;
            cancel.BorderColor = Theme.Border;
            cancel.Font = Theme.UiFont(9f);
            cancel.Size = new Size(110, 40);
            cancel.Location = new Point(730, 528);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            Shown += delegate
            {
                Cursor = Cursors.WaitCursor;
                try
                {
                    apps.AddRange(InstalledAppScanner.Scan());
                }
                catch
                {
                }
                Cursor = Cursors.Default;
                countLabel.Text = apps.Count + " apps found";
                RebuildRows();
            };
        }

        private void RebuildRows()
        {
            if (rows == null) { return; }
            rows.SuspendLayout();
            while (rows.Controls.Count > 0)
            {
                Control old = rows.Controls[0];
                rows.Controls.RemoveAt(0);
                old.Dispose();
            }

            int y = 0;
            foreach (InstalledApp app in apps)
            {
                if (!Matches(app))
                {
                    continue;
                }
                AddRow(app, y);
                y += 52;
            }

            if (y == 0)
            {
                Label empty = new Label();
                empty.Text = "No apps match this filter.";
                empty.Font = Theme.UiFont(9f);
                empty.ForeColor = Theme.Muted;
                empty.AutoSize = true;
                empty.Location = new Point(16, 18);
                rows.Controls.Add(empty);
            }
            rows.ResumeLayout();
        }

        private bool Matches(InstalledApp app)
        {
            if (!string.IsNullOrEmpty(searchText))
            {
                string haystack = (app.Name ?? string.Empty) + " " + (app.Path ?? string.Empty) + " " + (app.Source ?? string.Empty);
                if (haystack.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(filterText) && !string.Equals(filterText, "All sources", StringComparison.OrdinalIgnoreCase))
            {
                if ((app.Source ?? string.Empty).IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        private void AddRow(InstalledApp app, int y)
        {
            Panel row = new Panel();
            row.BackColor = Color.White;
            row.Location = new Point(0, y);
            row.Size = new Size(792, 52);
            row.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
                }
            };
            rows.Controls.Add(row);

            AppIcon icon = new AppIcon();
            icon.AppPath = app.Path;
            icon.SetEnabledState(true);
            icon.Size = new Size(30, 30);
            icon.Location = new Point(14, 11);
            row.Controls.Add(icon);

            Label name = new Label();
            name.Text = app.Name;
            name.Font = Theme.UiFont(9.5f, FontStyle.Bold);
            name.ForeColor = Theme.Text;
            name.BackColor = Color.White;
            name.AutoSize = false;
            name.AutoEllipsis = true;
            name.Location = new Point(54, 7);
            name.Size = new Size(470, 20);
            row.Controls.Add(name);

            Label path = new Label();
            path.Text = app.Path;
            path.Font = Theme.UiFont(7.5f);
            path.ForeColor = Theme.Muted;
            path.BackColor = Color.White;
            path.AutoSize = false;
            path.AutoEllipsis = true;
            path.Location = new Point(54, 27);
            path.Size = new Size(470, 18);
            row.Controls.Add(path);

            Pill source = new Pill();
            source.Font = Theme.UiFont(7f, FontStyle.Bold);
            source.Text = app.Source;
            source.FillColor = Theme.GraySoft;
            source.TextColor = Theme.Muted;
            source.DotColor = Theme.Faint;
            source.Location = new Point(540, 15);
            source.Size = source.GetPreferredSize(Size.Empty);
            row.Controls.Add(source);

            string key = PathUtil.Key(app.Path);
            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = selected.Contains(key);
            toggle.Location = new Point(736, 15);
            toggle.Size = new Size(40, 22);
            toggle.CheckedChanged += delegate
            {
                if (toggle.Checked) { selected.Add(key); }
                else { selected.Remove(key); }
                UpdateSelected();
            };
            row.Controls.Add(toggle);
        }

        private void UpdateSelected()
        {
            selectedLabel.Text = selected.Count + " selected";
            allowButton.Enabled = selected.Count > 0;
        }

        private void AllowSelected()
        {
            List<AllowedApp> result = new List<AllowedApp>();
            foreach (InstalledApp app in apps)
            {
                if (!selected.Contains(PathUtil.Key(app.Path)))
                {
                    continue;
                }
                AllowedApp allowed = new AllowedApp();
                allowed.Name = app.Name;
                allowed.Path = app.Path;
                allowed.Outbound = true;
                allowed.Inbound = false;
                allowed.Enabled = true;
                allowed.Notes = "Discovered from " + app.Source;
                allowed.AddedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                result.Add(allowed);
            }

            if (result.Count == 0)
            {
                return;
            }

            string path = Path.Combine(Path.GetTempPath(), "AegisWall-discovered.json");
            File.WriteAllText(path, Json.Serialize(result), new System.Text.UTF8Encoding(false));
            ResultPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}



