using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using AegisWall.Core;

namespace AegisWall.UI
{
    public sealed class MainForm : Form
    {
        private const int PageWidth = 820;

        private readonly bool demo;
        private readonly string initialPage;
        private Panel sidebar;
        private Panel header;
        private Panel content;
        private Label titleLabel;
        private Label subtitleLabel;
        private Label heroStateLabel;
        private Label heroBodyLabel;
        private Label allowedValueLabel;
        private Label blockedValueLabel;
        private Label lockdownValueLabel;
        private Label timerValueLabel;
        private Label keyValueLabel;
        private Label keySubLabel;
        private Label countdownLabel;
        private Label countdownReasonLabel;
        private Label monitorLabel;
        private Label filterCountLabel;
        private Label serviceStartedLabel;
        private Label lastTestLabel;
        private LastCommandResult lastCommand;
        private Pill statusPill;
        private ProgressLine countdownProgress;
        private Timer refreshTimer;
        private Timer uiDebounce;
        private readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NavButton> navButtons = new Dictionary<string, NavButton>(StringComparer.OrdinalIgnoreCase);
        private string currentPage;
        private StatusSnapshot status;
        private AegisConfig config;
        private List<AuditEntry> audit;
        private bool loading;
        private string appSearch = string.Empty;
        private string appFilter = "All access";
        private string logSearch = string.Empty;
        private string logFilter = "All events";
        private string enrollProvider = "yubikey";
        private FlatTextBox usbLabelBox;
        private FlatButton yubiButton;
        private FlatButton usbButton;

        public MainForm(bool demo, string initialPage)
        {
            this.demo = demo;
            this.initialPage = string.IsNullOrEmpty(initialPage) ? "dashboard" : initialPage;
            Text = "AegisWall";
            ClientSize = demo ? new Size(1120, 880) : new Size(1120, 760);
            MinimumSize = new Size(1120, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.UiFont(9f);
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;

            LoadData();
            BuildShell();
            BuildPages();
            ShowPage(this.initialPage);

            if (!demo)
            {
                refreshTimer = new Timer();
                refreshTimer.Interval = 3000;
                refreshTimer.Tick += delegate
                {
                    if (loading) { return; }
                    LoadData();
                    UpdateLive();
                };
                refreshTimer.Start();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && refreshTimer != null)
            {
                refreshTimer.Dispose();
                refreshTimer = null;
            }
            if (disposing && uiDebounce != null)
            {
                uiDebounce.Dispose();
                uiDebounce = null;
            }
            base.Dispose(disposing);
        }

        private void LoadData()
        {
            if (demo)
            {
                status = DemoData.Status(initialPage);
                config = DemoData.Config();
                audit = DemoData.Audit();
                lastCommand = DemoData.LastCommand();
            }
            else
            {
                status = StatusStore.Read();
                config = ConfigStore.Load();
                audit = AuditLog.Read(80);
                lastCommand = null;
                try
                {
                    if (File.Exists(ConfigStore.LastCommandPath))
                    {
                        lastCommand = Json.Deserialize<LastCommandResult>(File.ReadAllText(ConfigStore.LastCommandPath));
                    }
                }
                catch
                {
                    lastCommand = null;
                }
                if (status == null)
                {
                    status = StatusSnapshot.NotInstalled();
                }
            }

            if (status == null)
            {
                status = StatusSnapshot.NotInstalled();
            }
            if (config == null)
            {
                config = AegisConfig.Default();
            }
            if (config.AllowedApps == null)
            {
                config.AllowedApps = new List<AllowedApp>();
            }
            if (audit == null)
            {
                audit = new List<AuditEntry>();
            }
        }

        private void BuildShell()
        {
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 224;
            sidebar.BackColor = Theme.Sidebar;
            sidebar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
                }
            };

            IconBox logo = new IconBox();
            logo.GlyphData = Icons.Get("shield-check");
            logo.BoxColor = Theme.Accent;
            logo.IconColor = Color.White;
            logo.IconInset = 9f;
            logo.CornerRadius = 10;
            logo.Size = new Size(36, 36);
            logo.Location = new Point(20, 22);
            sidebar.Controls.Add(logo);

            Label brand = NewLabel(sidebar, "AegisWall", 13.5f, FontStyle.Bold, Theme.Strong, 66, 17, 0, 0, false);
            NewLabel(sidebar, "Hardware-anchored firewall", 7.5f, FontStyle.Regular, Theme.Muted, 66, 42, 0, 0, false);

            Panel divider = new Panel();
            divider.BackColor = Theme.Border;
            divider.Location = new Point(20, 78);
            divider.Size = new Size(184, 1);
            sidebar.Controls.Add(divider);

            string[,] items = new string[,]
            {
                { "dashboard", "Dashboard", "dashboard" },
                { "apps", "Applications", "package" },
                { "lockdown", "Lockdown", "lock" },
                { "key", "Hardware key", "key" },
                { "logs", "Audit log", "file-text" },
                { "lab", "Test lab", "activity" },
                { "settings", "Settings", "sliders" }
            };

            for (int i = 0; i < items.GetLength(0); i++)
            {
                string pageKey = items[i, 0];
                NavButton button = new NavButton();
                button.Text = items[i, 1];
                button.GlyphData = Icons.Get(items[i, 2]);
                button.Location = new Point(16, 96 + (i * 44));
                button.Size = new Size(192, 40);
                button.Click += delegate { ShowPage(pageKey); };
                sidebar.Controls.Add(button);
                navButtons[pageKey] = button;
            }

            Label version = NewLabel(sidebar, "v" + ProtectionEngine.Version, 7.5f, FontStyle.Regular, Theme.Faint, 20, 0, 0, 0, false);
            version.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            version.Location = new Point(20, sidebar.Height - 82);

            Label made = NewLabel(sidebar, "Made with " + (char)0x2665 + " by Muneeb Anjum", 7.5f, FontStyle.Regular, Theme.Muted, 20, 0, 0, 0, false);
            made.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            made.Location = new Point(20, sidebar.Height - 62);

            LinkLabel github = new LinkLabel();
            github.Text = "@horizonbymuneeb";
            github.Font = Theme.UiFont(7.5f);
            github.LinkColor = Theme.Accent;
            github.ActiveLinkColor = Theme.AccentHover;
            github.AutoSize = true;
            github.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            github.Location = new Point(20, sidebar.Height - 42);
            github.LinkClicked += delegate { OpenUrl("https://github.com/horizonbymuneeb"); };
            sidebar.Controls.Add(github);

            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 94;
            header.BackColor = Theme.Bg;

            titleLabel = NewLabel(header, "Dashboard", 18f, FontStyle.Bold, Theme.Strong, 24, 17, 0, 0, false);
            subtitleLabel = NewLabel(header, "Protection at a glance.", 9f, FontStyle.Regular, Theme.Muted, 24, 50, 0, 0, false);

            statusPill = new Pill();
            statusPill.Font = Theme.UiFont(7.5f, FontStyle.Bold);
            statusPill.Text = "Protected";
            statusPill.FillColor = Theme.GreenSoft;
            statusPill.TextColor = Theme.Green;
            statusPill.DotColor = Theme.Green;
            statusPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            statusPill.Location = new Point(header.Width - 140, 26);
            header.Controls.Add(statusPill);

            FlatButton refresh = new FlatButton();
            refresh.Text = "Refresh";
            refresh.GlyphData = Icons.Get("refresh-cw");
            refresh.BackColor = Color.White;
            refresh.ForeColor = Theme.Text;
            refresh.HoverColor = Theme.GraySoft;
            refresh.PressedColor = Theme.Border;
            refresh.BorderColor = Theme.Border;
            refresh.Font = Theme.UiFont(9f);
            refresh.Size = new Size(102, 34);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Location = new Point(header.Width - 254, 24);
            refresh.Click += delegate { ReloadAll(); };
            header.Controls.Add(refresh);

            header.Resize += delegate { PositionHeader(); };
            PositionHeader();

            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Theme.Bg;
            content.Padding = new Padding(24, 6, 24, 24);
            content.AutoScroll = true;

            Panel main = new Panel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Theme.Bg;
            main.Controls.Add(content);
            main.Controls.Add(header);
            Controls.Add(main);
            Controls.Add(sidebar);
        }

        private void PositionHeader()
        {
            if (statusPill == null || header == null) { return; }
            statusPill.Size = statusPill.GetPreferredSize(Size.Empty);
            statusPill.Left = header.Width - statusPill.Width - 24;
            Control refresh = null;
            foreach (Control control in header.Controls)
            {
                FlatButton button = control as FlatButton;
                if (button != null && button.Text == "Refresh") { refresh = button; break; }
            }
            if (refresh != null)
            {
                refresh.Left = statusPill.Left - refresh.Width - 12;
            }
        }

        private void BuildPages()
        {
            bool previousLoading = loading;
            loading = true;
            try
            {
            while (content.Controls.Count > 0)
            {
                Control old = content.Controls[0];
                content.Controls.RemoveAt(0);
                old.Dispose();
            }
            pages.Clear();
            heroStateLabel = null;
            heroBodyLabel = null;
            allowedValueLabel = null;
            timerValueLabel = null;
            keyValueLabel = null;
            keySubLabel = null;
            countdownLabel = null;
            countdownReasonLabel = null;
            countdownProgress = null;
            monitorLabel = null;

            BuildDashboard();
            BuildApps();
            BuildLockdown();
            BuildKey();
            BuildLogs();
            BuildTestLab();
            BuildSettings();
            UpdateLive();
            }
            finally
            {
                loading = previousLoading;
            }
        }

        private Panel AddPage(string key)
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.AutoScroll = true;
            page.BackColor = Theme.Bg;
            page.Visible = false;
            page.Padding = new Padding(0, 0, 0, 18);
            content.Controls.Add(page);
            pages[key] = page;
            return page;
        }

        private CardPanel AddCard(Control parent, int x, int y, int width, int height)
        {
            CardPanel card = new CardPanel();
            card.FillColor = Color.White;
            card.BorderColor = Theme.Border;
            card.CornerRadius = 14;
            card.Location = new Point(x, y);
            card.Size = new Size(width, height);
            parent.Controls.Add(card);
            return card;
        }

        private Label NewLabel(Control parent, string text, float size, FontStyle style, Color color, int x, int y, int width, int height, bool onCard)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = Theme.UiFont(size, style);
            label.ForeColor = color;
            label.BackColor = onCard ? Color.White : Color.Transparent;
            label.Location = new Point(x, y);
            if (width <= 0)
            {
                label.AutoSize = true;
            }
            else
            {
                label.AutoSize = false;
                label.Size = new Size(width, height <= 0 ? 20 : height);
                label.AutoEllipsis = false;
            }
            parent.Controls.Add(label);
            return label;
        }

        private void AddIcon(Control parent, string name, int x, int y, int size, Color box, Color foreground, float inset)
        {
            IconBox icon = new IconBox();
            icon.GlyphData = Icons.Get(name);
            icon.BoxColor = box;
            icon.IconColor = foreground;
            icon.IconInset = inset;
            icon.CornerRadius = 9;
            icon.Size = new Size(size, size);
            icon.Location = new Point(x, y);
            parent.Controls.Add(icon);
        }

        private FlatButton AddButton(Control parent, string text, string glyph, int x, int y, int width, int height, Color back, Color fore, Color hover, Color border, EventHandler click)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.GlyphData = Icons.Get(glyph);
            button.BackColor = back;
            button.ForeColor = fore;
            button.HoverColor = hover;
            button.PressedColor = hover;
            button.BorderColor = border;
            button.Font = Theme.UiFont(9f, FontStyle.Bold);
            button.Size = new Size(width, height);
            button.Location = new Point(x, y);
            if (click != null) { button.Click += click; }
            parent.Controls.Add(button);
            return button;
        }

        private void AddPill(Control parent, string text, Color fill, Color fore, Color dot, int x, int y)
        {
            Pill pill = new Pill();
            pill.Font = Theme.UiFont(7.5f, FontStyle.Bold);
            pill.Text = text;
            pill.FillColor = fill;
            pill.TextColor = fore;
            pill.DotColor = dot;
            pill.Location = new Point(x, y);
            pill.Size = pill.GetPreferredSize(Size.Empty);
            parent.Controls.Add(pill);
        }

        private void BuildDashboard()
        {
            Panel page = AddPage("dashboard");
            CardPanel hero = AddCard(page, 0, 0, PageWidth, 164);
            AddIcon(hero, "shield-check", 26, 28, 54, Theme.AccentSoft, Theme.Accent, 11f);

            heroStateLabel = NewLabel(hero, "Protected", 20f, FontStyle.Bold, Theme.Strong, 100, 22, 0, 0, true);
            heroBodyLabel = NewLabel(hero, "Only allowlisted applications can reach the network.", 9.5f, FontStyle.Regular, Theme.Muted, 100, 60, 470, 42, true);
            AddPill(hero, "YubiKey connected", Theme.GreenSoft, Theme.Green, Theme.Green, 100, 112);

            AddButton(hero, config.Armed ? "Disarm" : "Arm protection", config.Armed ? "shield-off" : "shield-check",
                630, 28, 160, 40,
                config.Armed ? Color.White : Theme.Accent,
                config.Armed ? Theme.Text : Color.White,
                config.Armed ? Theme.GraySoft : Theme.AccentHover,
                config.Armed ? Theme.Border : Color.Transparent,
                delegate { RunCli(config.Armed ? "disarm" : "arm"); });

            AddButton(hero, "Lock network", "wifi-off", 630, 80, 160, 36, Theme.RedSoft, Theme.Red,
                Color.FromArgb(254, 226, 226), Color.FromArgb(254, 202, 202),
                delegate { RunCli("lockdown " + config.LockdownMinutes.ToString(CultureInfo.InvariantCulture)); });

            int statWidth = 195;
            AddStat(page, 0, 180, statWidth, "package", status.AllowedApps.ToString(CultureInfo.InvariantCulture),
                "Allowed apps", "Only these can reach the network.", out allowedValueLabel);
            AddStat(page, 208, 180, statWidth, "wifi-off", status.BlockedAttempts.ToString(CultureInfo.InvariantCulture),
                "Blocked attempts", "Denied connection attempts.", out blockedValueLabel);
            AddStat(page, 416, 180, statWidth, "lock", status.LockdownCount.ToString(CultureInfo.InvariantCulture),
                "Lockdowns", "Punishment timers fired.", out lockdownValueLabel);
            AddStat(page, 624, 180, statWidth, "key",
                status.KeyEnrolled ? (status.KeyPresent ? "Connected" : "Not detected") : "Not enrolled",
                "Hardware key", string.IsNullOrEmpty(status.KeySerial) ? "No key enrolled." : "Serial " + status.KeySerial,
                out keyValueLabel, out keySubLabel);

            CardPanel posture = AddCard(page, 0, 304, 400, 300);
            NewLabel(posture, "Protection checks", 11f, FontStyle.Bold, Theme.Strong, 24, 20, 0, 0, true);
            NewLabel(posture, "Live posture of the service and policy.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 0, 0, true);
            AddCheckRow(posture, 70, "WFP filters applied", status.FiltersApplied, status.FilterCount + " filters");
            AddCheckRow(posture, 100, "Policy signature verified", status.ConfigVerified, status.ConfigVerified ? "Signed by vault key" : "Unverified");
            AddCheckRow(posture, 130, "Trigger monitor", status.MonitorAvailable, status.MonitorAvailable ? status.TriggerMode : "Unavailable");
            AddCheckRow(posture, 160, "Hardware key", status.KeyEnrolled && status.KeyPresent, status.KeyPresent ? "Present" : "Not detected");
            AddCheckRow(posture, 190, "Watchdog task", status.WatchdogEnabled, status.WatchdogEnabled ? "Enabled" : "Disabled");
            AddCheckRow(posture, 220, "Kernel driver", status.KernelDriverEnabled, status.KernelDriverEnabled ? "Installed" : "Phase 2 roadmap");
            AddCheckRow(posture, 250, "WDAC policy", !string.IsNullOrEmpty(status.WdacPolicyPath), string.IsNullOrEmpty(status.WdacPolicyPath) ? "Not configured" : "Configured");

            CardPanel activity = AddCard(page, 420, 304, 400, 300);
            NewLabel(activity, "Recent activity", 11f, FontStyle.Bold, Theme.Strong, 24, 20, 0, 0, true);
            NewLabel(activity, "Hash-chained events from the service.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 0, 0, true);

            if (audit.Count == 0)
            {
                NewLabel(activity, "No events yet. Arm AegisWall to begin.", 9f, FontStyle.Regular, Theme.Muted, 24, 82, 0, 0, true);
            }
            else
            {
                int count = Math.Min(4, audit.Count);
                for (int i = 0; i < count; i++)
                {
                    AddActivityRow(activity, 78 + (i * 48), audit[audit.Count - 1 - i], 400);
                }
            }

            NewLabel(page, "Persistent WFP filters remain fail-closed if the service stops while armed. Use the audit log to review every decision.",
                8f, FontStyle.Regular, Theme.Muted, 0, 620, PageWidth, 34, false);
        }

        private void AddStat(Control parent, int x, int y, int width, string icon, string value, string label, string sub, out Label valueLabel)
        {
            Label ignored;
            AddStat(parent, x, y, width, icon, value, label, sub, out valueLabel, out ignored);
        }

        private void AddStat(Control parent, int x, int y, int width, string icon, string value, string label, string sub, out Label valueLabel, out Label subLabel)
        {
            CardPanel card = AddCard(parent, x, y, width, 104);
            AddIcon(card, icon, 18, 18, 36, Theme.AccentSoft, Theme.Accent, 7f);
            valueLabel = NewLabel(card, value, 17f, FontStyle.Bold, Theme.Strong, 66, 13, 0, 0, true);
            NewLabel(card, label, 9f, FontStyle.Bold, Theme.Text, 66, 45, 0, 0, true);
            subLabel = NewLabel(card, sub, 7.5f, FontStyle.Regular, Theme.Muted, 18, 78, width - 36, 18, true);
        }

        private void AddCheckRow(Control parent, int y, string label, bool ok, string detail)
        {
            AddIcon(parent, ok ? "check" : "alert-triangle", 24, y, 22, ok ? Theme.GreenSoft : Theme.OrangeSoft, ok ? Theme.Green : Theme.Orange, 5f);
            NewLabel(parent, label, 8.5f, FontStyle.Bold, Theme.Text, 54, y - 2, 0, 0, true);
            if (!string.IsNullOrEmpty(detail))
            {
                NewLabel(parent, detail, 7.5f, FontStyle.Regular, Theme.Muted, 54, y + 13, 320, 15, true);
            }
        }

        private void AddActivityRow(Control parent, int y, AuditEntry entry)
        {
            AddActivityRow(parent, y, entry, PageWidth);
        }

        private void AddActivityRow(Control parent, int y, AuditEntry entry, int width)
        {
            string icon = "activity";
            Color box = Theme.AccentSoft;
            Color fore = Theme.Accent;
            if (entry.Event.StartsWith("lockdown", StringComparison.OrdinalIgnoreCase))
            {
                icon = "wifi-off";
                box = Theme.RedSoft;
                fore = Theme.Red;
            }
            else if (entry.Event.StartsWith("key", StringComparison.OrdinalIgnoreCase))
            {
                icon = "key";
                box = Theme.OrangeSoft;
                fore = Theme.Orange;
            }
            else if (entry.Event.StartsWith("allow", StringComparison.OrdinalIgnoreCase))
            {
                icon = "check";
                box = Theme.GreenSoft;
                fore = Theme.Green;
            }

            AddIcon(parent, icon, 24, y + 4, 30, box, fore, 6f);
            NewLabel(parent, EventTitle(entry.Event), 9.5f, FontStyle.Bold, Theme.Text, 66, y, 0, 0, true);
            string detail = entry.Detail ?? string.Empty;
            int maxChars = Math.Max(32, (width - 190) / 5);
            if (detail.Length > maxChars) { detail = detail.Substring(0, maxChars - 3) + "..."; }
            NewLabel(parent, detail, 8f, FontStyle.Regular, Theme.Muted, 66, y + 20, width - 170, 18, true);
            NewLabel(parent, LocalTime(entry.Utc), 8f, FontStyle.Regular, Theme.Faint, width - 110, y + 8, 100, 18, true);
        }

        private void BuildApps()
        {
            Panel page = AddPage("apps");
            CardPanel top = AddCard(page, 0, 0, PageWidth, 142);
            NewLabel(top, "Application allowlist", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(top, "Strict outbound and inbound allowlist.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 0, 0, true);

            AddButton(top, "Add app", "plus", 640, 22, 156, 38, Theme.Accent, Color.White, Theme.AccentHover, Color.Transparent,
                delegate { AddApp(); });
            AddButton(top, "Import", "folder-open", 520, 22, 108, 38, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { ImportApps(); });
            AddButton(top, "Discover", "folder-open", 300, 22, 100, 38, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { DiscoverApps(); });
            AddButton(top, "Export", "file-text", 410, 22, 100, 38, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { ExportApps(); });

            NewLabel(top, "Search", 7.5f, FontStyle.Bold, Theme.Muted, 24, 64, 0, 0, true);
            FlatTextBox search = new FlatTextBox();
            search.Text = appSearch;
            search.Location = new Point(24, 82);
            search.Size = new Size(296, 34);
            search.TextChanged += delegate
            {
                if (loading) { return; }
                appSearch = search.Text;
                DebounceRebuild();
            };
            top.Controls.Add(search);

            NewLabel(top, "Access filter", 7.5f, FontStyle.Bold, Theme.Muted, 336, 64, 0, 0, true);
            SelectBox filter = new SelectBox();
            filter.Font = Theme.UiFont(9f);
            filter.Items.Add("All access");
            filter.Items.Add("Outbound");
            filter.Items.Add("Inbound");
            filter.Items.Add("Both");
            filter.Items.Add("Disabled");
            filter.Location = new Point(336, 82);
            filter.Size = new Size(160, 34);
            filter.SelectedItem = appFilter;
            if (filter.SelectedIndex < 0) { filter.SelectedIndex = 0; }
            filter.SelectedIndexChanged += delegate
            {
                if (loading) { return; }
                appFilter = filter.SelectedItem;
                DebounceRebuild();
            };
            top.Controls.Add(filter);

            List<AllowedApp> shown = FilteredApps();
            int count = shown.Count;
            int cardHeight = 58 + Math.Max(1, count) * 68 + 14;
            CardPanel list = AddCard(page, 0, 156, PageWidth, cardHeight);
            NewLabel(list, "Application", 8f, FontStyle.Bold, Theme.Muted, 62, 18, 0, 0, true);
            NewLabel(list, "Out", 8f, FontStyle.Bold, Theme.Muted, 500, 18, 0, 0, true);
            NewLabel(list, "In", 8f, FontStyle.Bold, Theme.Muted, 556, 18, 0, 0, true);
            NewLabel(list, "On", 8f, FontStyle.Bold, Theme.Muted, 612, 18, 0, 0, true);
            NewLabel(list, "Action", 8f, FontStyle.Bold, Theme.Muted, 720, 18, 0, 0, true);

            if (count == 0)
            {
                NewLabel(list, "No applications match this filter. Add an app or clear the search.", 9f, FontStyle.Regular, Theme.Muted, 24, 70, 0, 0, true);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    AddAppRow(list, 52 + (i * 68), shown[i]);
                }
            }

            NewLabel(page, "Discover scans Start Menu, uninstall registry and App Paths. Path-based allowlisting is enforced by WFP.",
                8f, FontStyle.Regular, Theme.Muted, 0, 156 + cardHeight + 12, PageWidth, 32, false);
        }

        private void AddAppRow(Control parent, int y, AllowedApp app)
        {
            Panel row = new Panel();
            row.BackColor = Color.White;
            row.Location = new Point(1, y);
            row.Size = new Size(PageWidth - 2, 68);
            row.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
                }
            };
            parent.Controls.Add(row);

            Color nameColor = app.Enabled ? Theme.Text : Theme.Faint;
            AddIcon(row, "package", 18, 19, 30, app.Enabled ? Theme.AccentSoft : Theme.GraySoft, app.Enabled ? Theme.Accent : Theme.Faint, 6f);
            NewLabel(row, app.Name, 9.5f, FontStyle.Bold, nameColor, 60, 8, 410, 20, true);
            Label path = NewLabel(row, app.Path, 7.5f, FontStyle.Regular, Theme.Muted, 60, 28, 410, 18, true);
            path.AutoEllipsis = true;
            string notes = app.Notes ?? string.Empty;
            if (app.BlockedCount > 0)
            {
                notes = (notes.Length > 0 ? notes + "  |  " : string.Empty) + app.BlockedCount + " blocked";
            }
            if (!string.IsNullOrEmpty(notes))
            {
                NewLabel(row, notes, 7.5f, FontStyle.Regular, app.BlockedCount > 0 ? Theme.Red : Theme.Faint, 60, 46, 410, 16, true);
            }

            string pathValue = app.Path;
            ToggleSwitch outbound = new ToggleSwitch();
            outbound.Checked = app.Outbound;
            outbound.Location = new Point(500, 24);
            outbound.Size = new Size(40, 22);
            outbound.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("update-app " + Quote(pathValue) + " --outbound " + (outbound.Checked ? "true" : "false"));
            };
            row.Controls.Add(outbound);

            ToggleSwitch inbound = new ToggleSwitch();
            inbound.Checked = app.Inbound;
            inbound.Location = new Point(556, 24);
            inbound.Size = new Size(40, 22);
            inbound.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("update-app " + Quote(pathValue) + " --inbound " + (inbound.Checked ? "true" : "false"));
            };
            row.Controls.Add(inbound);

            ToggleSwitch enabled = new ToggleSwitch();
            enabled.Checked = app.Enabled;
            enabled.Location = new Point(612, 24);
            enabled.Size = new Size(40, 22);
            enabled.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("update-app " + Quote(pathValue) + " --enabled " + (enabled.Checked ? "true" : "false"));
            };
            row.Controls.Add(enabled);

            AddButton(row, "Remove", "trash-2", 720, 19, 78, 30, Color.White, Theme.Red, Theme.RedSoft, Theme.Border,
                delegate { RunCli("remove " + Quote(pathValue)); });
        }

        private List<AllowedApp> FilteredApps()
        {
            List<AllowedApp> result = new List<AllowedApp>();
            string search = (appSearch ?? string.Empty).Trim();
            foreach (AllowedApp app in config.AllowedApps)
            {
                if (search.Length > 0)
                {
                    string haystack = (app.Name ?? string.Empty) + " " + (app.Path ?? string.Empty) + " " + (app.Notes ?? string.Empty);
                    if (haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                if (string.Equals(appFilter, "Outbound", StringComparison.OrdinalIgnoreCase) && !app.Outbound) { continue; }
                if (string.Equals(appFilter, "Inbound", StringComparison.OrdinalIgnoreCase) && !app.Inbound) { continue; }
                if (string.Equals(appFilter, "Both", StringComparison.OrdinalIgnoreCase) && !(app.Outbound && app.Inbound)) { continue; }
                if (string.Equals(appFilter, "Disabled", StringComparison.OrdinalIgnoreCase) && app.Enabled) { continue; }
                result.Add(app);
            }
            return result;
        }

        private void DiscoverApps()
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: discovery is disabled in screenshots.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (InstalledAppsForm dialog = new InstalledAppsForm())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dialog.ResultPath))
                {
                    RunCli("import " + Quote(dialog.ResultPath));
                }
            }
        }

        private void ImportApps()
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: import is disabled in screenshots.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Import AegisWall allowlist";
                dialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RunCli("import " + Quote(dialog.FileName));
                }
            }
        }

        private void ExportApps()
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: export is disabled in screenshots.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export AegisWall allowlist";
                dialog.Filter = "JSON files (*.json)|*.json";
                dialog.FileName = "aegiswall-allowlist.json";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RunCli("export " + Quote(dialog.FileName));
                }
            }
        }

        private void BuildLockdown()
        {
            Panel page = AddPage("lockdown");
            CardPanel controls = AddCard(page, 0, 0, PageWidth, 300);
            NewLabel(controls, "Punishment timer", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(controls, "When a blocked process tries to connect, the whole network is cut for this duration.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 700, 18, true);

            NumberBox minutes = new NumberBox();
            minutes.Minimum = 1;
            minutes.Maximum = 1440;
            minutes.Value = Math.Max(1, Math.Min(1440, config.LockdownMinutes));
            minutes.Font = Theme.UiFont(9.5f);
            minutes.Location = new Point(24, 76);
            minutes.Size = new Size(96, 34);
            controls.Controls.Add(minutes);
            NewLabel(controls, "minutes", 8.5f, FontStyle.Regular, Theme.Muted, 128, 82, 0, 0, true);

            int[] presets = new int[] { 15, 30, 60 };
            for (int i = 0; i < presets.Length; i++)
            {
                int value = presets[i];
                AddButton(controls, value.ToString(CultureInfo.InvariantCulture), "", 190 + (i * 62), 76, 54, 28,
                    Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                    delegate { minutes.Value = value; SaveMinutes((int)minutes.Value); });
            }

            NewLabel(controls, "Trigger", 8.5f, FontStyle.Bold, Theme.Text, 24, 128, 0, 0, true);
            SelectBox mode = new SelectBox();
            mode.Font = Theme.UiFont(9f);
            mode.Items.Add("User apps only");
            mode.Items.Add("All processes");
            mode.Items.Add("Manual only");
            mode.Location = new Point(96, 123);
            mode.Size = new Size(220, 34);
            if (string.Equals(config.TriggerMode, "All", StringComparison.OrdinalIgnoreCase)) { mode.SelectedIndex = 1; }
            else if (string.Equals(config.TriggerMode, "Manual", StringComparison.OrdinalIgnoreCase)) { mode.SelectedIndex = 2; }
            else { mode.SelectedIndex = 0; }
            mode.SelectedIndexChanged += delegate
            {
                if (loading) { return; }
                string trigger = mode.SelectedIndex == 1 ? "All" : (mode.SelectedIndex == 2 ? "Manual" : "UserAppsOnly");
                RunCli("set " + Quote("{\"triggerMode\":\"" + trigger + "\"}"));
            };
            controls.Controls.Add(mode);

            NewLabel(controls, "Grace period", 8.5f, FontStyle.Bold, Theme.Text, 24, 178, 0, 0, true);
            NumberBox grace = new NumberBox();
            grace.Minimum = 0;
            grace.Maximum = 3600;
            grace.Value = Math.Max(0, Math.Min(3600, config.GraceSeconds));
            grace.Font = Theme.UiFont(9.5f);
            grace.Location = new Point(118, 172);
            grace.Size = new Size(96, 34);
            controls.Controls.Add(grace);
            NewLabel(controls, "seconds before key removal locks the network", 8f, FontStyle.Regular, Theme.Muted, 224, 178, 300, 18, true);

            ToggleSwitch exclude = new ToggleSwitch();
            exclude.Checked = config.ExcludeSystemProcesses;
            exclude.Location = new Point(24, 222);
            exclude.Size = new Size(40, 22);
            exclude.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("set " + Quote("{\"excludeSystemProcesses\":" + (exclude.Checked ? "true" : "false") + "}"));
            };
            controls.Controls.Add(exclude);
            NewLabel(controls, "Exclude Windows components", 8.5f, FontStyle.Bold, Theme.Text, 76, 220, 0, 0, true);
            NewLabel(controls, "Ignore system noise when triggering.", 7.5f, FontStyle.Regular, Theme.Muted, 76, 236, 0, 0, true);

            ToggleSwitch keyLock = new ToggleSwitch();
            keyLock.Checked = config.LockdownOnKeyRemoval;
            keyLock.Location = new Point(300, 222);
            keyLock.Size = new Size(40, 22);
            keyLock.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("set " + Quote("{\"lockdownOnKeyRemoval\":" + (keyLock.Checked ? "true" : "false") + "}"));
            };
            controls.Controls.Add(keyLock);
            NewLabel(controls, "Lockdown on key removal", 8.5f, FontStyle.Bold, Theme.Text, 352, 220, 0, 0, true);
            NewLabel(controls, "Start a timer after the grace period.", 7.5f, FontStyle.Regular, Theme.Muted, 352, 236, 0, 0, true);

            SelectBox removal = new SelectBox();
            removal.Font = Theme.UiFont(8.5f);
            removal.Items.Add("Block all traffic");
            removal.Items.Add("Start lockdown");
            removal.Location = new Point(600, 220);
            removal.Size = new Size(196, 34);
            removal.SelectedIndex = string.Equals(config.KeyRemovalAction, "Lockdown", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            removal.SelectedIndexChanged += delegate
            {
                if (loading) { return; }
                string action = removal.SelectedIndex == 1 ? "Lockdown" : "BlockAll";
                RunCli("set " + Quote("{\"keyRemovalAction\":\"" + action + "\"}"));
            };
            controls.Controls.Add(removal);

            AddButton(controls, "Save timer", "", 640, 76, 156, 34, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { SaveMinutes((int)minutes.Value); });
            AddButton(controls, "Save grace", "", 640, 172, 156, 34, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { RunCli("set " + Quote("{\"graceSeconds\":" + ((int)grace.Value).ToString(CultureInfo.InvariantCulture) + "}")); });
            AddButton(controls, "Lock network now", "wifi-off", 640, 122, 156, 38, Theme.Red, Color.White,
                Color.FromArgb(185, 28, 28), Color.Transparent,
                delegate { RunCli("lockdown " + ((int)minutes.Value).ToString(CultureInfo.InvariantCulture)); });

            NewLabel(controls, "Automatic triggers require the Filtering Platform audit policy. Manual lockdown always works.", 8f, FontStyle.Regular, Theme.Muted, 24, 270, 590, 24, true);

            CardPanel countdown = AddCard(page, 0, 316, PageWidth, 160);
            NewLabel(countdown, "Active lockdown", 10.5f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            countdownLabel = NewLabel(countdown, "No active lockdown", 30f, FontStyle.Bold, Theme.Strong, 24, 48, 0, 0, true);
            countdownReasonLabel = NewLabel(countdown, "The network is open to allowlisted applications.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 106, 740, 22, true);
            countdownProgress = new ProgressLine();
            countdownProgress.Location = new Point(24, 132);
            countdownProgress.Size = new Size(772, 6);
            countdown.Controls.Add(countdownProgress);
            AddPill(countdown, "Auto-unlock", Theme.AccentSoft, Theme.Accent, Theme.Accent, 680, 18);
            monitorLabel = NewLabel(countdown, "Trigger monitor active", 7.5f, FontStyle.Regular, Theme.Green, 24, 144, 0, 0, true);

            CardPanel history = AddCard(page, 0, 492, PageWidth, 180);
            NewLabel(history, "Recent triggers and blocks", 10.5f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            int shown = 0;
            for (int i = audit.Count - 1; i >= 0 && shown < 3; i--)
            {
                string evt = audit[i].Event ?? string.Empty;
                if (evt.StartsWith("lockdown", StringComparison.OrdinalIgnoreCase)
                    || evt.StartsWith("blocked", StringComparison.OrdinalIgnoreCase)
                    || evt.StartsWith("key.removed", StringComparison.OrdinalIgnoreCase))
                {
                    AddActivityRow(history, 58 + (shown * 40), audit[i], PageWidth);
                    shown++;
                }
            }
            if (shown == 0)
            {
                NewLabel(history, "No lockdowns or blocked attempts recorded yet.", 9f, FontStyle.Regular, Theme.Muted, 24, 62, 0, 0, true);
            }

            NewLabel(page, "During a lockdown, loopback and DHCP remain available; every external connection is blocked at the WFP layer.",
                8f, FontStyle.Regular, Theme.Muted, 0, 684, PageWidth, 30, false);
        }

        private void SaveMinutes(int minutes)
        {
            RunCli("set " + Quote("{\"lockdownMinutes\":" + minutes.ToString(CultureInfo.InvariantCulture) + "}"));
        }

        private void BuildKey()
        {
            Panel page = AddPage("key");
            bool usbEnrolled = string.Equals(config.KeyProvider, "usb", StringComparison.OrdinalIgnoreCase);
            enrollProvider = usbEnrolled ? "usb" : "yubikey";

            CardPanel keyCard = AddCard(page, 0, 0, PageWidth, 240);
            AddIcon(keyCard, "key", 26, 28, 54, Theme.AccentSoft, Theme.Accent, 11f);
            NewLabel(keyCard, usbEnrolled ? "USB security key" : "YubiKey", 16f, FontStyle.Bold, Theme.Strong, 100, 24, 0, 0, true);
            NewLabel(keyCard, status.KeyEnrolled ? (status.KeyPresent ? "Connected and enrolled" : "Enrolled, not detected") : "No key enrolled",
                9.5f, FontStyle.Bold, status.KeyPresent ? Theme.Green : Theme.Orange, 100, 58, 0, 0, true);
            NewLabel(keyCard, string.IsNullOrEmpty(status.KeySerial) ? "Serial unavailable" : "Serial " + status.KeySerial,
                8.5f, FontStyle.Regular, Theme.Muted, 100, 82, 0, 0, true);
            NewLabel(keyCard, usbEnrolled
                    ? "DPAPI machine-bound key file on a USB drive. The file will not work on another PC."
                    : "Slot 2 HMAC-SHA1 challenge-response. The secret never leaves the key.",
                8.5f, FontStyle.Regular, Theme.Muted, 100, 108, 500, 20, true);

            AddButton(keyCard, "Enroll key", "plus", 630, 28, 160, 40, Theme.Accent, Color.White, Theme.AccentHover, Color.Transparent,
                delegate { EnrollSelected(); });
            AddButton(keyCard, "Unlock now", "key", 630, 80, 160, 36, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { RunCli("unlock"); });
            AddButton(keyCard, "Key status", "fingerprint", 630, 124, 160, 36, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { RunCli("key-status"); });

            NewLabel(keyCard, "Enrollment source", 8f, FontStyle.Bold, Theme.Muted, 100, 148, 0, 0, true);
            yubiButton = AddButton(keyCard, "YubiKey", "key", 100, 166, 96, 30, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { enrollProvider = "yubikey"; UpdateProviderButtons(); });
            usbButton = AddButton(keyCard, "USB key", "usb", 204, 166, 96, 30, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { enrollProvider = "usb"; UpdateProviderButtons(); });

            NewLabel(keyCard, "USB volume label", 8f, FontStyle.Bold, Theme.Muted, 316, 148, 0, 0, true);
            usbLabelBox = new FlatTextBox();
            usbLabelBox.Text = string.IsNullOrEmpty(config.UsbVolumeLabel) ? "AEGISWALL" : config.UsbVolumeLabel;
            usbLabelBox.Location = new Point(316, 166);
            usbLabelBox.Size = new Size(150, 34);
            keyCard.Controls.Add(usbLabelBox);
            UpdateProviderButtons();

            CardPanel recovery = AddCard(page, 0, 256, PageWidth, 170);
            AddIcon(recovery, "alert-triangle", 26, 26, 42, Theme.OrangeSoft, Theme.Orange, 9f);
            NewLabel(recovery, "Recovery code", 12f, FontStyle.Bold, Theme.Strong, 84, 24, 0, 0, true);
            NewLabel(recovery, "The recovery code is shown once at enrollment. Store it offline. It can disarm AegisWall if the YubiKey or USB key is lost.",
                8.5f, FontStyle.Regular, Theme.Muted, 84, 52, 680, 44, true);
            AddButton(recovery, "Rotate code", "refresh-cw", 540, 112, 120, 38, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { RotateRecovery(); });
            AddButton(recovery, "Instructions", "file-text", 672, 112, 118, 38, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { ShowRecoveryHelp(); });

            CardPanel security = AddCard(page, 0, 442, PageWidth, 190);
            NewLabel(security, "How the key anchor works", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(security, "1. YubiKey: HMAC-SHA1 slot 2. USB key: DPAPI machine-bound key file on the drive.", 8.5f, FontStyle.Regular, Theme.Text, 24, 50, 750, 20, true);
            NewLabel(security, "2. The key response unwraps a random vault key that signs the policy file.", 8.5f, FontStyle.Regular, Theme.Text, 24, 74, 750, 20, true);
            NewLabel(security, "3. If the key is missing or the signature fails, persistent filters block all external traffic.", 8.5f, FontStyle.Regular, Theme.Text, 24, 98, 750, 20, true);
            NewLabel(security, "4. USB key files are tied to this PC. A copied file will not unlock another machine.", 8.5f, FontStyle.Regular, Theme.Text, 24, 122, 750, 20, true);

            NewLabel(page, "AegisWall cannot make a Windows administrator mathematically unable to remove a driver. UEFI-locked WDAC and a signed kernel driver are the next hardening phase.",
                8f, FontStyle.Regular, Theme.Muted, 0, 648, PageWidth, 36, false);
        }

        private void UpdateProviderButtons()
        {
            bool usb = string.Equals(enrollProvider, "usb", StringComparison.OrdinalIgnoreCase);
            if (yubiButton != null)
            {
                yubiButton.BackColor = usb ? Color.White : Theme.Accent;
                yubiButton.ForeColor = usb ? Theme.Text : Color.White;
                yubiButton.BorderColor = usb ? Theme.Border : Color.Transparent;
                yubiButton.HoverColor = usb ? Theme.GraySoft : Theme.AccentHover;
            }
            if (usbButton != null)
            {
                usbButton.BackColor = usb ? Theme.Accent : Color.White;
                usbButton.ForeColor = usb ? Color.White : Theme.Text;
                usbButton.BorderColor = usb ? Color.Transparent : Theme.Border;
                usbButton.HoverColor = usb ? Theme.AccentHover : Theme.GraySoft;
            }
            if (usbLabelBox != null)
            {
                usbLabelBox.Enabled = usb;
                usbLabelBox.BackColor = usb ? Color.White : Theme.GraySoft;
            }
        }

        private void EnrollSelected()
        {
            if (string.Equals(enrollProvider, "usb", StringComparison.OrdinalIgnoreCase))
            {
                string label = usbLabelBox == null ? "AEGISWALL" : usbLabelBox.Text.Trim();
                if (label.Length == 0) { label = "AEGISWALL"; }
                RunCli("enroll --usb --volume " + Quote(label));
            }
            else
            {
                RunCli("enroll --yubikey");
            }
        }

        private void RotateRecovery()
        {
            MessageBox.Show(this,
                "A new recovery code will be generated. The elevated console will print it once.\n\nSave it offline before closing the console.",
                "Rotate recovery code", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RunCli("rotate-recovery");
        }

        private void ShowRecoveryHelp()
        {
            MessageBox.Show(this,
                "Recovery code\n\n" +
                "At enrollment, AegisWall prints a Crockford base32 code. Save it offline.\n\n" +
                "To disarm without the YubiKey:\n" +
                "  aegiswall disarm --recovery <code>\n\n" +
                "If the code is lost, run uninstall.ps1 as administrator and use reset --force. That removes the filters, but the old policy is gone.",
                "AegisWall recovery", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildLogs()
        {
            Panel page = AddPage("logs");
            CardPanel card = AddCard(page, 0, 0, PageWidth, 620);
            NewLabel(card, "Audit log", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(card, "Every service decision is appended to a SHA-256 hash chain.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 42, 0, 0, true);

            long broken = demo ? 0 : AuditLog.VerifyChain();
            AddPill(card, broken == 0 ? "Chain valid" : "Chain broken", broken == 0 ? Theme.GreenSoft : Theme.RedSoft,
                broken == 0 ? Theme.Green : Theme.Red, broken == 0 ? Theme.Green : Theme.Red, 690, 18);

            NewLabel(card, "Search", 7.5f, FontStyle.Bold, Theme.Muted, 24, 64, 0, 0, true);
            FlatTextBox search = new FlatTextBox();
            search.Text = logSearch;
            search.Location = new Point(24, 82);
            search.Size = new Size(280, 34);
            search.TextChanged += delegate
            {
                if (loading) { return; }
                logSearch = search.Text;
                DebounceRebuild();
            };
            card.Controls.Add(search);

            NewLabel(card, "Event filter", 7.5f, FontStyle.Bold, Theme.Muted, 316, 64, 0, 0, true);
            SelectBox filter = new SelectBox();
            filter.Font = Theme.UiFont(9f);
            filter.Items.Add("All events");
            filter.Items.Add("Blocked");
            filter.Items.Add("Lockdown");
            filter.Items.Add("Key");
            filter.Items.Add("Admin");
            filter.Location = new Point(316, 82);
            filter.Size = new Size(150, 34);
            filter.SelectedItem = logFilter;
            if (filter.SelectedIndex < 0) { filter.SelectedIndex = 0; }
            filter.SelectedIndexChanged += delegate
            {
                if (loading) { return; }
                logFilter = filter.SelectedItem;
                DebounceRebuild();
            };
            card.Controls.Add(filter);

            AddButton(card, "Verify chain", "check", 480, 80, 120, 32, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { VerifyLogs(); });
            AddButton(card, "Export CSV", "file-text", 612, 80, 160, 32, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { ExportLogs(); });

            NewLabel(card, "Time", 7.5f, FontStyle.Bold, Theme.Muted, 24, 126, 0, 0, true);
            NewLabel(card, "Event", 7.5f, FontStyle.Bold, Theme.Muted, 120, 126, 0, 0, true);
            NewLabel(card, "Actor", 7.5f, FontStyle.Bold, Theme.Muted, 280, 126, 0, 0, true);
            NewLabel(card, "Detail", 7.5f, FontStyle.Bold, Theme.Muted, 380, 126, 0, 0, true);

            Panel rows = new Panel();
            rows.Location = new Point(24, 146);
            rows.Size = new Size(772, 438);
            rows.AutoScroll = true;
            rows.BackColor = Color.White;
            card.Controls.Add(rows);

            List<AuditEntry> shown = FilteredAudit();
            if (shown.Count == 0)
            {
                NewLabel(rows, "No events match this filter. Arm AegisWall to begin.", 9f, FontStyle.Regular, Theme.Muted, 0, 20, 0, 0, true);
            }
            else
            {
                for (int i = shown.Count - 1; i >= 0; i--)
                {
                    AddAuditRow(rows, (shown.Count - 1 - i) * 44, shown[i], 752);
                }
            }
        }

        private void AddAuditRow(Control parent, int y, AuditEntry entry, int width)
        {
            Panel row = new Panel();
            row.BackColor = Color.White;
            row.Location = new Point(0, y);
            row.Size = new Size(width, 44);
            row.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
                }
            };
            parent.Controls.Add(row);

            NewLabel(row, LocalTime(entry.Utc), 8f, FontStyle.Regular, Theme.Faint, 0, 12, 90, 18, true);
            NewLabel(row, EventTitle(entry.Event), 8.5f, FontStyle.Bold, Theme.Text, 96, 12, 170, 18, true);
            NewLabel(row, entry.Actor, 8f, FontStyle.Regular, Theme.Muted, 270, 12, 100, 18, true);
            Label detail = NewLabel(row, entry.Detail, 8f, FontStyle.Regular, Theme.Muted, 376, 12, width - 380, 18, true);
            detail.AutoEllipsis = true;
        }

        private List<AuditEntry> FilteredAudit()
        {
            List<AuditEntry> result = new List<AuditEntry>();
            string search = (logSearch ?? string.Empty).Trim();
            foreach (AuditEntry entry in audit)
            {
                if (search.Length > 0)
                {
                    string haystack = (entry.Event ?? string.Empty) + " " + (entry.Detail ?? string.Empty) + " " + (entry.Actor ?? string.Empty);
                    if (haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                string evt = entry.Event ?? string.Empty;
                if (string.Equals(logFilter, "Blocked", StringComparison.OrdinalIgnoreCase) && !evt.StartsWith("blocked", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (string.Equals(logFilter, "Lockdown", StringComparison.OrdinalIgnoreCase) && !evt.StartsWith("lockdown", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (string.Equals(logFilter, "Key", StringComparison.OrdinalIgnoreCase) && !evt.StartsWith("key", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (string.Equals(logFilter, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(entry.Actor, "admin", StringComparison.OrdinalIgnoreCase)) { continue; }
                result.Add(entry);
            }
            return result;
        }

        private void VerifyLogs()
        {
            long broken = demo ? 0 : AuditLog.VerifyChain();
            if (broken == 0)
            {
                MessageBox.Show(this, "Audit chain is valid. Every entry links to the previous hash.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "Audit chain is broken at sequence " + broken + ".", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExportLogs()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export audit log";
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.FileName = "aegiswall-audit.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                System.Text.StringBuilder csv = new System.Text.StringBuilder();
                csv.AppendLine("Seq,Utc,Event,Actor,Detail");
                foreach (AuditEntry entry in FilteredAudit())
                {
                    csv.Append(entry.Seq.ToString(CultureInfo.InvariantCulture)).Append(',');
                    csv.Append(Csv(entry.Utc)).Append(',');
                    csv.Append(Csv(entry.Event)).Append(',');
                    csv.Append(Csv(entry.Actor)).Append(',');
                    csv.AppendLine(Csv(entry.Detail));
                }
                File.WriteAllText(dialog.FileName, csv.ToString(), new System.Text.UTF8Encoding(false));
                MessageBox.Show(this, "Audit log exported.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static string Csv(string value)
        {
            string text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private void BuildTestLab()
        {
            Panel page = AddPage("lab");
            CardPanel controls = AddCard(page, 0, 0, PageWidth, 190);
            NewLabel(controls, "Test lab", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(controls, "Safe containment tests. WFP filters stay enforced.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 700, 18, true);

            ToggleSwitch testMode = new ToggleSwitch();
            testMode.Checked = config.TestMode;
            testMode.Location = new Point(24, 78);
            testMode.Size = new Size(46, 26);
            testMode.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("set " + Quote("{\"testMode\":" + (testMode.Checked ? "true" : "false") + "}"));
            };
            controls.Controls.Add(testMode);
            NewLabel(controls, "Test mode", 9.5f, FontStyle.Bold, Theme.Text, 80, 76, 0, 0, true);
            NewLabel(controls, "Shortens lockdown and tags audit events as TEST.", 7.5f, FontStyle.Regular, Theme.Muted, 80, 96, 360, 18, true);

            NumberBox seconds = new NumberBox();
            seconds.Minimum = 15;
            seconds.Maximum = 3600;
            seconds.Value = Math.Max(15, Math.Min(3600, config.TestLockdownSeconds));
            seconds.Font = Theme.UiFont(9.5f);
            seconds.Location = new Point(24, 128);
            seconds.Size = new Size(96, 34);
            controls.Controls.Add(seconds);
            NewLabel(controls, "test lockdown seconds", 8f, FontStyle.Regular, Theme.Muted, 128, 136, 180, 18, true);
            AddButton(controls, "Save", "", 320, 128, 76, 34, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { RunCli("set " + Quote("{\"testLockdownSeconds\":" + ((int)seconds.Value).ToString(CultureInfo.InvariantCulture) + "}")); });

            AddButton(controls, "Run beacon test", "activity", 520, 70, 276, 40, Theme.Accent, Color.White, Theme.AccentHover, Color.Transparent,
                delegate { RunBeaconTest(); });
            AddButton(controls, "Export report", "file-text", 520, 120, 276, 36, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { ExportTestReport(); });
            NewLabel(controls, "Test mode never disables filters. Use an isolated VM for real malware.", 8f, FontStyle.Regular, Theme.Orange, 24, 164, 480, 18, true);

            CardPanel checklist = AddCard(page, 0, 206, PageWidth, 310);
            NewLabel(checklist, "Test checklist", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(checklist, "Expected results for the spare-laptop malware lab.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 700, 18, true);

            bool beaconBlocked = lastCommand != null && !string.IsNullOrEmpty(lastCommand.Data)
                && lastCommand.Data.IndexOf("\"blocked\":true", StringComparison.OrdinalIgnoreCase) >= 0;
            bool lockdownSeen = status.LockdownCount > 0 || string.Equals(status.State, "Lockdown", StringComparison.OrdinalIgnoreCase);
            AddLabRow(checklist, 68, "Unapproved beacon blocked", beaconBlocked ? "Last run: blocked by WFP" : "Run the beacon test", beaconBlocked ? "PASS" : "WAIT", beaconBlocked ? Theme.Green : Theme.Orange);
            AddLabRow(checklist, 104, "Lockdown triggered", status.LockdownCount + " lockdown event(s) recorded", lockdownSeen ? "PASS" : "WAIT", lockdownSeen ? Theme.Green : Theme.Orange);
            AddLabRow(checklist, 140, "Service stop keeps filters", "Manual: Stop-Service AegisWall, then run the beacon", "MANUAL", Theme.Muted);
            AddLabRow(checklist, 176, "Key removal grace", "Manual: remove key, reinsert within 30s", "MANUAL", Theme.Muted);
            AddLabRow(checklist, 212, "Reboot persistence", "Manual: reboot without the key", "MANUAL", Theme.Muted);
            AddLabRow(checklist, 248, "Config tamper detection", "Manual: edit config.json, then reload", "MANUAL", Theme.Muted);
            lastTestLabel = NewLabel(checklist, LastTestText(), 8f, FontStyle.Regular, Theme.Muted, 24, 278, 760, 24, true);

            CardPanel commands = AddCard(page, 0, 532, PageWidth, 150);
            NewLabel(commands, "Commands", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(commands, "AegisWall.Cli.exe test-mode on", 8.5f, FontStyle.Regular, Theme.Text, 24, 48, 740, 18, true);
            NewLabel(commands, "AegisWall.Cli.exe test-beacon", 8.5f, FontStyle.Regular, Theme.Text, 24, 70, 740, 18, true);
            NewLabel(commands, "AegisWall.Cli.exe test-report C:\\Temp\\aegiswall-report.json", 8.5f, FontStyle.Regular, Theme.Text, 24, 92, 740, 18, true);
            NewLabel(commands, "Portable: Run-AegisWall-Cli.ps1 test-beacon", 8f, FontStyle.Regular, Theme.Muted, 24, 116, 740, 18, true);

            NewLabel(page, "Test mode only shortens the punishment timer; it does not disable filters or open the network.",
                8f, FontStyle.Regular, Theme.Muted, 0, 696, PageWidth, 30, false);
        }

        private void AddLabRow(Control parent, int y, string title, string detail, string statusText, Color statusColor)
        {
            Color fill = string.Equals(statusText, "PASS", StringComparison.OrdinalIgnoreCase) ? Theme.GreenSoft
                : (string.Equals(statusText, "MANUAL", StringComparison.OrdinalIgnoreCase) ? Theme.GraySoft : Theme.OrangeSoft);
            AddIcon(parent, string.Equals(statusText, "PASS", StringComparison.OrdinalIgnoreCase) ? "check" : (string.Equals(statusText, "MANUAL", StringComparison.OrdinalIgnoreCase) ? "activity" : "alert-triangle"),
                24, y, 24, fill, statusColor, 5f);
            NewLabel(parent, title, 9f, FontStyle.Bold, Theme.Text, 54, y - 2, 0, 0, true);
            NewLabel(parent, detail, 7.5f, FontStyle.Regular, Theme.Muted, 54, y + 13, 560, 18, true);
            AddPill(parent, statusText, fill, statusColor, statusColor, 700, y - 1);
        }

        private string LastTestText()
        {
            if (lastCommand == null)
            {
                return "No test run yet. Enable test mode, then run the beacon test.";
            }
            return lastCommand.Message + "  |  Run test-report for full details.";
        }

        private void RunBeaconTest()
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: beacon test is disabled in screenshots.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunCli("test-beacon");
        }

        private void ExportTestReport()
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: report export is disabled in screenshots.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export AegisWall test report";
                dialog.Filter = "JSON files (*.json)|*.json";
                dialog.FileName = "aegiswall-test-report.json";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RunCli("test-report " + Quote(dialog.FileName));
                }
            }
        }
        private void BuildSettings()
        {
            Panel page = AddPage("settings");

            CardPanel network = AddCard(page, 0, 0, 400, 300);
            NewLabel(network, "Network policy", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(network, "Baseline allowances while the allowlist is enforced.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 340, 20, true);
            AddSettingToggle(network, 76, "Allow loopback", "Local IPC and localhost.", "allowLoopback", config.AllowLoopback, 330);
            AddSettingToggle(network, 126, "Allow DHCP", "Maintain the IP lease.", "allowDhcp", config.AllowDhcp, 330);
            AddSettingToggle(network, 176, "Allow system DNS", "svchost DNS resolution.", "allowSystemDns", config.AllowSystemDns, 330);
            AddSettingToggle(network, 226, "Require key at boot", "Fail closed until unlock.", "requireKeyAtBoot", config.RequireKeyAtBoot, 330);
            NewLabel(network, "Available during lockdown. All else is blocked.", 7.5f, FontStyle.Regular, Theme.Faint, 24, 270, 340, 20, true);

            CardPanel dns = AddCard(page, 0, 316, 400, 230);
            NewLabel(dns, "DNS and resolution", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(dns, "System DNS or a pinned resolver list.", 8.5f, FontStyle.Regular, Theme.Muted, 24, 44, 340, 20, true);
            NewLabel(dns, "Mode", 7.5f, FontStyle.Bold, Theme.Muted, 24, 66, 0, 0, true);
            SelectBox dnsMode = new SelectBox();
            dnsMode.Font = Theme.UiFont(9f);
            dnsMode.Items.Add("System DNS");
            dnsMode.Items.Add("Pinned servers");
            dnsMode.Location = new Point(24, 84);
            dnsMode.Size = new Size(150, 26);
            dnsMode.SelectedIndex = string.Equals(config.DnsMode, "Pinned", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            dnsMode.SelectedIndexChanged += delegate
            {
                if (loading) { return; }
                string mode = dnsMode.SelectedIndex == 1 ? "Pinned" : "System";
                RunCli("set " + Quote("{\"dnsMode\":\"" + mode + "\"}"));
            };
            dns.Controls.Add(dnsMode);

            NewLabel(dns, "Servers", 7.5f, FontStyle.Bold, Theme.Muted, 190, 66, 0, 0, true);
            FlatTextBox dnsBox = new FlatTextBox();
            dnsBox.Text = string.Join(", ", config.DnsServers.ToArray());
            dnsBox.Location = new Point(190, 84);
            dnsBox.Size = new Size(186, 34);
            dns.Controls.Add(dnsBox);
            AddButton(dns, "Apply DNS", "", 24, 126, 120, 32, Theme.Accent, Color.White, Theme.AccentHover, Color.Transparent,
                delegate { ApplyDns(dnsBox.Text); });
            NewLabel(dns, "Pinned mode only permits svchost DNS to the listed servers. Unlisted resolvers are blocked.", 7.5f, FontStyle.Regular, Theme.Faint, 24, 174, 340, 30, true);

            CardPanel automation = AddCard(page, 0, 562, 400, 130);
            NewLabel(automation, "Automation", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            AddSettingToggle(automation, 50, "Auto-start service", "Start AegisWall at boot.", "autoStart", config.AutoStart, 330);
            AddSettingToggle(automation, 90, "Watchdog task", "Re-apply filters if missing.", "watchdogEnabled", config.WatchdogEnabled, 330);

            CardPanel hardening = AddCard(page, 420, 0, 400, 250);
            NewLabel(hardening, "Hardening", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(hardening, "Kernel driver", 8.5f, FontStyle.Bold, Theme.Text, 24, 50, 0, 0, true);
            AddPill(hardening, status.KernelDriverEnabled ? "Installed" : "Phase 2", status.KernelDriverEnabled ? Theme.GreenSoft : Theme.OrangeSoft,
                status.KernelDriverEnabled ? Theme.Green : Theme.Orange, status.KernelDriverEnabled ? Theme.Green : Theme.Orange, 290, 46);
            NewLabel(hardening, "User-mode WFP is active. A signed WFP callout driver is the next phase.", 7.5f, FontStyle.Regular, Theme.Muted, 24, 70, 340, 20, true);

            NewLabel(hardening, "WDAC policy path", 8.5f, FontStyle.Bold, Theme.Text, 24, 100, 0, 0, true);
            FlatTextBox wdac = new FlatTextBox();
            wdac.Text = config.WdacPolicyPath;
            wdac.Location = new Point(24, 120);
            wdac.Size = new Size(250, 34);
            hardening.Controls.Add(wdac);
            AddButton(hardening, "Browse", "folder-open", 282, 118, 94, 32, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { BrowseWdac(wdac); });
            AddButton(hardening, "Apply path", "", 24, 160, 120, 32, Theme.Accent, Color.White, Theme.AccentHover, Color.Transparent,
                delegate { RunCli("set " + Quote("{\"wdacPolicyPath\":" + Quote(wdac.Text) + "}")); });
            NewLabel(hardening, "UEFI-locked WDAC is the strongest admin-resistance primitive on Windows. Installation is manual and documented.", 7.5f, FontStyle.Regular, Theme.Faint, 24, 206, 340, 30, true);

            CardPanel service = AddCard(page, 420, 266, 400, 220);
            NewLabel(service, "Service", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            NewLabel(service, "Recovery: restart on failure. Watchdog: SYSTEM task every minute.", 7.5f, FontStyle.Regular, Theme.Muted, 24, 44, 340, 20, true);
            NewLabel(service, "Persistent filters survive crashes. Stopping the service while armed does not open the network.", 7.5f, FontStyle.Regular, Theme.Muted, 24, 64, 340, 30, true);
            NewLabel(service, "Data: %ProgramData%\\AegisWall", 7.5f, FontStyle.Regular, Theme.Faint, 24, 98, 340, 18, true);
            AddButton(service, "Open data folder", "folder-open", 24, 128, 170, 36, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { OpenDataFolder(); });
            AddButton(service, "Uninstall", "trash-2", 206, 128, 130, 36, Color.White, Theme.Red, Theme.RedSoft, Theme.Border,
                delegate { ShowUninstallHelp(); });

            CardPanel diagnostics = AddCard(page, 420, 502, 400, 190);
            NewLabel(diagnostics, "Diagnostics", 11f, FontStyle.Bold, Theme.Strong, 24, 18, 0, 0, true);
            filterCountLabel = NewLabel(diagnostics, "Filters: " + status.FilterCount, 8.5f, FontStyle.Regular, Theme.Text, 24, 50, 0, 0, true);
            serviceStartedLabel = NewLabel(diagnostics, "Service started: " + (string.IsNullOrEmpty(status.ServiceStartedUtc) ? "unknown" : LocalTime(status.ServiceStartedUtc)),
                8.5f, FontStyle.Regular, Theme.Text, 24, 74, 340, 18, true);
            long chain = demo ? 0 : AuditLog.VerifyChain();
            NewLabel(diagnostics, "Audit chain: " + (chain == 0 ? "valid" : "broken at " + chain), 8.5f, FontStyle.Regular,
                chain == 0 ? Theme.Green : Theme.Red, 24, 98, 340, 18, true);
            NewLabel(diagnostics, "Key: " + status.KeyProvider + " | " + status.KeyRemovalAction + " | grace " + status.GraceSeconds + "s",
                7.5f, FontStyle.Regular, Theme.Muted, 24, 122, 340, 18, true);
            AddButton(diagnostics, "Run diagnostics", "activity", 24, 150, 170, 36, Color.White, Theme.Text, Theme.GraySoft, Theme.Border,
                delegate { RunCli("diagnostics"); });

            NewLabel(page, "Changes require administrator approval and a hardware-key unlock. The UI itself runs unprivileged.",
                8f, FontStyle.Regular, Theme.Muted, 0, 704, PageWidth, 30, false);
        }

        private void BrowseWdac(FlatTextBox target)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select a WDAC policy XML";
                dialog.Filter = "WDAC policy (*.xml)|*.xml|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = dialog.FileName;
                }
            }
        }

        private void AddSettingToggle(Control parent, int y, string title, string subtitle, string key, bool value, int toggleX)
        {
            NewLabel(parent, title, 9.5f, FontStyle.Bold, Theme.Text, 24, y, 0, 0, true);
            NewLabel(parent, subtitle, 8f, FontStyle.Regular, Theme.Muted, 24, y + 20, 270, 18, true);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = value;
            toggle.Location = new Point(toggleX, y + 2);
            toggle.Size = new Size(46, 26);
            toggle.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunCli("set " + Quote("{\"" + key + "\":" + (toggle.Checked ? "true" : "false") + "}"));
            };
            parent.Controls.Add(toggle);
        }

        private void ApplyDns(string text)
        {
            string[] parts = text.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> servers = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (value.Length > 0) { servers.Add(value); }
            }

            System.Text.StringBuilder json = new System.Text.StringBuilder();
            json.Append("{\"dnsServers\":[");
            for (int i = 0; i < servers.Count; i++)
            {
                if (i > 0) { json.Append(','); }
                json.Append('"').Append(servers[i].Replace("\"", "\\\"")).Append('"');
            }
            json.Append("]}");
            RunCli("set " + Quote(json.ToString()));
        }

        private void AddApp()
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: the app picker is disabled in screenshots.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Allow an application";
                dialog.Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RunCli("allow " + Quote(dialog.FileName));
                }
            }
        }

        private void ShowPage(string page)
        {
            if (string.IsNullOrEmpty(page) || !pages.ContainsKey(page))
            {
                page = "dashboard";
            }

            currentPage = page;
            foreach (KeyValuePair<string, Panel> item in pages)
            {
                item.Value.Visible = string.Equals(item.Key, page, StringComparison.OrdinalIgnoreCase);
            }
            foreach (KeyValuePair<string, NavButton> item in navButtons)
            {
                item.Value.Active = string.Equals(item.Key, page, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(page, "apps", StringComparison.OrdinalIgnoreCase))
            {
                titleLabel.Text = "Applications";
                subtitleLabel.Text = "Strict allowlist for outbound network access.";
            }
            else if (string.Equals(page, "lockdown", StringComparison.OrdinalIgnoreCase))
            {
                titleLabel.Text = "Lockdown";
                subtitleLabel.Text = "Trigger-based network punishment timer.";
            }
            else if (string.Equals(page, "key", StringComparison.OrdinalIgnoreCase))
            {
                titleLabel.Text = "Hardware key";
                subtitleLabel.Text = "YubiKey-anchored authorization and recovery.";
            }
            else if (string.Equals(page, "logs", StringComparison.OrdinalIgnoreCase))
            {
                titleLabel.Text = "Audit log";
                subtitleLabel.Text = "Tamper-evident event history.";
            }
            else if (string.Equals(page, "lab", StringComparison.OrdinalIgnoreCase))
            {
                titleLabel.Text = "Test lab";
                subtitleLabel.Text = "Safe containment tests. Filters stay enforced.";
            }
            else if (string.Equals(page, "settings", StringComparison.OrdinalIgnoreCase))
            {
                titleLabel.Text = "Settings";
                subtitleLabel.Text = "Network policy and service configuration.";
            }
            else
            {
                titleLabel.Text = "Dashboard";
                subtitleLabel.Text = "Protection at a glance.";
            }

            UpdateLive();
        }

        private void UpdateLive()
        {
            if (status == null) { return; }

            string state = status.State ?? "Unknown";
            string pillText = state;
            Color fill = Theme.GraySoft;
            Color fore = Theme.Muted;
            Color dot = Theme.Faint;

            if (string.Equals(state, "Protected", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Protected";
                fill = Theme.GreenSoft;
                fore = Theme.Green;
                dot = Theme.Green;
            }
            else if (string.Equals(state, "Lockdown", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Lockdown";
                fill = Theme.RedSoft;
                fore = Theme.Red;
                dot = Theme.Red;
            }
            else if (string.Equals(state, "KeyLocked", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Key locked";
                fill = Theme.OrangeSoft;
                fore = Theme.Orange;
                dot = Theme.Orange;
            }
            else if (string.Equals(state, "KeyGrace", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Key grace";
                fill = Theme.OrangeSoft;
                fore = Theme.Orange;
                dot = Theme.Orange;
            }
            else if (string.Equals(state, "Unverified", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Unverified";
                fill = Theme.OrangeSoft;
                fore = Theme.Orange;
                dot = Theme.Orange;
            }
            else if (string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Error";
                fill = Theme.RedSoft;
                fore = Theme.Red;
                dot = Theme.Red;
            }
            else if (string.Equals(state, "Disarmed", StringComparison.OrdinalIgnoreCase))
            {
                pillText = "Disarmed";
            }

            if (statusPill != null)
            {
                statusPill.Text = pillText;
                statusPill.FillColor = fill;
                statusPill.TextColor = fore;
                statusPill.DotColor = dot;
                statusPill.Size = statusPill.GetPreferredSize(Size.Empty);
                PositionHeader();
            }

            if (heroStateLabel != null)
            {
                heroStateLabel.Text = StateTitle(state);
                heroStateLabel.ForeColor = StateColor(state);
            }
            if (heroBodyLabel != null)
            {
                heroBodyLabel.Text = StateBody(state);
            }
            if (allowedValueLabel != null)
            {
                allowedValueLabel.Text = status.AllowedApps.ToString(CultureInfo.InvariantCulture);
            }
            if (blockedValueLabel != null)
            {
                blockedValueLabel.Text = status.BlockedAttempts.ToString(CultureInfo.InvariantCulture);
            }
            if (lockdownValueLabel != null)
            {
                lockdownValueLabel.Text = status.LockdownCount.ToString(CultureInfo.InvariantCulture);
            }
            if (timerValueLabel != null)
            {
                timerValueLabel.Text = status.LockdownMinutes.ToString(CultureInfo.InvariantCulture) + " min";
            }
            if (keyValueLabel != null)
            {
                keyValueLabel.Text = status.KeyEnrolled ? (status.KeyPresent ? "Connected" : "Not detected") : "Not enrolled";
            }
            if (keySubLabel != null)
            {
                keySubLabel.Text = string.IsNullOrEmpty(status.KeySerial) ? "No key enrolled." : "Serial " + status.KeySerial;
            }
            if (filterCountLabel != null)
            {
                filterCountLabel.Text = "Filters: " + status.FilterCount;
            }
            if (serviceStartedLabel != null)
            {
                serviceStartedLabel.Text = "Service started: " + (string.IsNullOrEmpty(status.ServiceStartedUtc) ? "unknown" : LocalTime(status.ServiceStartedUtc));
            }
            if (countdownLabel != null)
            {
                if (status.LockdownRemainingSeconds > 0)
                {
                    countdownLabel.Text = FormatDuration(status.LockdownRemainingSeconds);
                    countdownLabel.ForeColor = Theme.Red;
                    countdownReasonLabel.Text = status.LockdownReason;
                    countdownProgress.Value = Math.Min(100, Math.Max(0, status.LockdownRemainingSeconds * 100 / Math.Max(60, status.LockdownMinutes * 60)));
                }
                else
                {
                    countdownLabel.Text = "No active lockdown";
                    countdownLabel.ForeColor = Theme.Strong;
                    countdownReasonLabel.Text = "The network is open to allowlisted applications.";
                    countdownProgress.Value = 0;
                }
            }
            if (monitorLabel != null)
            {
                if (status.KeyGraceRemainingSeconds > 0)
                {
                    monitorLabel.Text = "Key removed: grace " + status.KeyGraceRemainingSeconds + "s";
                    monitorLabel.ForeColor = Theme.Orange;
                }
                else
                {
                    monitorLabel.Text = status.MonitorAvailable ? "Trigger monitor active" : "Trigger monitor unavailable";
                    monitorLabel.ForeColor = status.MonitorAvailable ? Theme.Green : Theme.Orange;
                }
            }
        }

        private static string StateTitle(string state)
        {
            if (string.Equals(state, "Protected", StringComparison.OrdinalIgnoreCase)) { return "Protected"; }
            if (string.Equals(state, "Lockdown", StringComparison.OrdinalIgnoreCase)) { return "Network locked"; }
            if (string.Equals(state, "KeyLocked", StringComparison.OrdinalIgnoreCase)) { return "Hardware key required"; }
            if (string.Equals(state, "KeyGrace", StringComparison.OrdinalIgnoreCase)) { return "Hardware key removed"; }
            if (string.Equals(state, "Disarmed", StringComparison.OrdinalIgnoreCase)) { return "Protection is off"; }
            if (string.Equals(state, "Unverified", StringComparison.OrdinalIgnoreCase)) { return "Configuration unverified"; }
            if (string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase)) { return "Protection error"; }
            if (string.Equals(state, "NotInstalled", StringComparison.OrdinalIgnoreCase)) { return "Not installed"; }
            return "AegisWall";
        }

        private static Color StateColor(string state)
        {
            if (string.Equals(state, "Protected", StringComparison.OrdinalIgnoreCase)) { return Theme.Green; }
            if (string.Equals(state, "Lockdown", StringComparison.OrdinalIgnoreCase)) { return Theme.Red; }
            if (string.Equals(state, "KeyLocked", StringComparison.OrdinalIgnoreCase)) { return Theme.Orange; }
            if (string.Equals(state, "KeyGrace", StringComparison.OrdinalIgnoreCase)) { return Theme.Orange; }
            if (string.Equals(state, "Unverified", StringComparison.OrdinalIgnoreCase)) { return Theme.Orange; }
            if (string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase)) { return Theme.Red; }
            return Theme.Strong;
        }

        private static string StateBody(string state)
        {
            if (string.Equals(state, "Protected", StringComparison.OrdinalIgnoreCase)) { return "Only allowlisted applications can reach the network."; }
            if (string.Equals(state, "Lockdown", StringComparison.OrdinalIgnoreCase)) { return "A blocked connection attempt triggered the punishment timer."; }
            if (string.Equals(state, "KeyLocked", StringComparison.OrdinalIgnoreCase)) { return "Insert the enrolled hardware key and unlock to restore allowlisted traffic."; }
            if (string.Equals(state, "KeyGrace", StringComparison.OrdinalIgnoreCase)) { return "The hardware key was removed. Reinsert it before the grace period ends."; }
            if (string.Equals(state, "Disarmed", StringComparison.OrdinalIgnoreCase)) { return "No WFP filters are active. Arm AegisWall to enforce the allowlist."; }
            if (string.Equals(state, "Unverified", StringComparison.OrdinalIgnoreCase)) { return "The signed policy could not be verified; external traffic is blocked."; }
            if (string.Equals(state, "Error", StringComparison.OrdinalIgnoreCase)) { return "The filtering engine could not apply the policy. Check the audit log."; }
            if (string.Equals(state, "NotInstalled", StringComparison.OrdinalIgnoreCase)) { return "Run install.ps1 as administrator to install the service."; }
            return "Hardware-anchored application firewall.";
        }

        private static string EventTitle(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) { return "Event"; }
            string value = eventName.Replace('.', ' ');
            if (value.Length == 0) { return eventName; }
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static string LocalTime(string utc)
        {
            DateTime value;
            if (DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
            {
                return value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
            return "--:--";
        }

        private static string FormatDuration(int seconds)
        {
            if (seconds < 0) { seconds = 0; }
            int minutes = seconds / 60;
            int rest = seconds % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + rest.ToString("00", CultureInfo.InvariantCulture);
        }

        private void DebounceRebuild()
        {
            if (loading) { return; }
            if (uiDebounce != null)
            {
                uiDebounce.Stop();
                uiDebounce.Dispose();
            }

            uiDebounce = new Timer();
            uiDebounce.Interval = 350;
            uiDebounce.Tick += delegate
            {
                uiDebounce.Stop();
                uiDebounce.Dispose();
                uiDebounce = null;
                BuildPages();
                ShowPage(currentPage);
            };
            uiDebounce.Start();
        }

        private void ReloadAll()
        {
            loading = true;
            try
            {
                LoadData();
                BuildPages();
                ShowPage(currentPage);
            }
            finally
            {
                loading = false;
            }
        }

        private void RunCli(string arguments)
        {
            if (demo)
            {
                MessageBox.Show(this, "Demo mode: " + arguments, "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string cli = Path.Combine(baseDir, "AegisWall.Cli.exe");
            string portableCli = Path.Combine(baseDir, "Run-AegisWall-Cli.ps1");
            bool portable = string.Equals(Environment.GetEnvironmentVariable("AEGISWALL_PORTABLE"), "1", StringComparison.Ordinal)
                || File.Exists(Path.Combine(baseDir, "portable.mode"));

            if (portable && !File.Exists(portableCli))
            {
                MessageBox.Show(this, "Portable CLI script was not found next to the UI.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!portable && !File.Exists(cli))
            {
                MessageBox.Show(this, "AegisWall.Cli.exe was not found next to the UI.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                if (portable)
                {
                    info.FileName = "powershell.exe";
                    info.Arguments = "-NoProfile -ExecutionPolicy Bypass -STA -File " + Quote(portableCli) + " " + arguments;
                }
                else
                {
                    info.FileName = cli;
                    info.Arguments = arguments;
                }
                info.WorkingDirectory = baseDir;
                info.Verb = "runas";
                info.UseShellExecute = true;
                Process.Start(info);
                Timer timer = new Timer();
                timer.Interval = 1800;
                timer.Tick += delegate
                {
                    timer.Stop();
                    timer.Dispose();
                    ReloadAll();
                };
                timer.Start();
            }
            catch
            {
                MessageBox.Show(this, "Administrator approval is required for this action.", "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private void OpenDataFolder()
        {
            try
            {
                Process.Start("explorer.exe", ConfigStore.DataDir);
            }
            catch
            {
            }
        }

        private void ShowUninstallHelp()
        {
            MessageBox.Show(this,
                "Uninstall\n\n" +
                "1. Open an elevated PowerShell.\n" +
                "2. Run .\\uninstall.ps1\n\n" +
                "The uninstaller stops the service, removes every AegisWall WFP filter, deletes the watchdog task, and removes the service.",
                "AegisWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(url);
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch
            {
            }
        }
    }
}















