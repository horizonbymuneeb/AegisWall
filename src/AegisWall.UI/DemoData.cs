using System;
using System.Collections.Generic;
using System.Globalization;
using AegisWall.Core;

namespace AegisWall.UI
{
    public static class DemoData
    {
        public static AegisConfig Config()
        {
            AegisConfig config = new AegisConfig();
            config.Armed = true;
            config.RequireKeyAtBoot = true;
            config.LockdownMinutes = 30;
            config.TriggerMode = "UserAppsOnly";
            config.GraceSeconds = 30;
            config.LockdownOnKeyRemoval = true;
            config.ExcludeSystemProcesses = true;
            config.AllowLoopback = true;
            config.AllowDhcp = true;
            config.AllowSystemDns = true;
            config.DnsMode = "System";
            config.KeyRemovalAction = "BlockAll";
            config.WatchdogEnabled = true;
            config.TestMode = true;
            config.TestLockdownSeconds = 60;
            config.AutoStart = true;
            config.KernelDriverEnabled = false;
            config.WdacPolicyPath = string.Empty;
            config.UsbVolumeLabel = "AEGISWALL";
            config.BlockedAttempts = 12;
            config.LockdownCount = 2;
            config.LastBlockedApp = @"C:\Users\Public\update.exe";
            config.LastBlockedUtc = DateTime.UtcNow.AddMinutes(-14).ToString("o", CultureInfo.InvariantCulture);
            config.KeyProvider = "yubikey";
            config.KeySerial = "12345678";
            config.KeyChallenge = Convert.ToBase64String(new byte[32]);
            config.VaultBlob = Convert.ToBase64String(new byte[48]);
            config.KeyEnrolledUtc = DateTime.UtcNow.AddDays(-12).ToString("o", CultureInfo.InvariantCulture);
            config.InstalledUtc = DateTime.UtcNow.AddDays(-12).ToString("o", CultureInfo.InvariantCulture);
            config.ServiceVersion = ProtectionEngine.Version;
            config.AllowedApps.Add(App("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", false));
            config.AllowedApps.Add(App("Mozilla Firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe", false));
            config.AllowedApps.Add(App("Visual Studio Code", @"C:\Users\Muneeb\AppData\Local\Programs\Microsoft VS Code\Code.exe", false));
            config.AllowedApps.Add(App("Slack", @"C:\Users\Muneeb\AppData\Local\slack\slack.exe", false));
            config.AllowedApps.Add(App("Zoom", @"C:\Users\Muneeb\AppData\Roaming\Zoom\bin\Zoom.exe", true));
            config.AllowedApps.Add(App("Steam", @"C:\Program Files (x86)\Steam\steam.exe", false));
            return config;
        }

        public static StatusSnapshot Status(string page)
        {
            StatusSnapshot status = new StatusSnapshot();
            status.State = "Protected";
            status.Armed = true;
            status.KeyPresent = true;
            status.KeyEnrolled = true;
            status.ServiceRunning = true;
            status.MonitorAvailable = true;
            status.FiltersApplied = true;
            status.ConfigVerified = true;
            status.KeySerial = "12345678";
            status.KeyProvider = "yubikey";
            status.AllowedApps = 6;
            status.LockdownMinutes = 30;
            status.BlockedAttempts = 12;
            status.LockdownCount = 2;
            status.FilterCount = 18;
            status.LastBlockedApp = @"C:\Users\Public\update.exe";
            status.LastBlockedUtc = DateTime.UtcNow.AddMinutes(-14).ToString("o", CultureInfo.InvariantCulture);
            status.DnsMode = "System";
            status.TriggerMode = "UserAppsOnly";
            status.KeyRemovalAction = "BlockAll";
            status.KernelDriverEnabled = false;
            status.WatchdogEnabled = true;
            status.AutoStart = true;
            status.GraceSeconds = 30;
            status.ServiceStartedUtc = DateTime.UtcNow.AddHours(-3).ToString("o", CultureInfo.InvariantCulture);
            status.ServiceVersion = ProtectionEngine.Version;
            status.LastEvent = "key.unlock";
            status.UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            if (string.Equals(page, "lab", StringComparison.OrdinalIgnoreCase))
            {
                status.TestMode = true;
                status.TestLockdownSeconds = 60;
                status.BlockedAttempts = 13;
                status.LockdownCount = 3;
            }

            if (string.Equals(page, "lockdown", StringComparison.OrdinalIgnoreCase))
            {
                status.State = "Lockdown";
                status.LockdownUntilUtc = DateTime.UtcNow.AddSeconds(765).ToString("o", CultureInfo.InvariantCulture);
                status.LockdownReason = @"Blocked connection attempt: C:\Users\Public\update.exe";
                status.LockdownRemainingSeconds = 765;
                status.LastEvent = "lockdown.trigger";
            }

            return status;
        }

        public static List<AuditEntry> Audit()
        {
            List<AuditEntry> entries = new List<AuditEntry>();
            entries.Add(Entry(8, DateTime.UtcNow.AddMinutes(-1), "blocked", @"C:\Users\Public\update.exe", "monitor"));
            entries.Add(Entry(7, DateTime.UtcNow.AddMinutes(-3), "key.removed.grace", "Key removed; grace 30s", "service"));
            entries.Add(Entry(6, DateTime.UtcNow.AddMinutes(-2), "key.unlock", "Hardware key unlocked", "admin"));
            entries.Add(Entry(5, DateTime.UtcNow.AddMinutes(-8), "allow", @"C:\Program Files\Google\Chrome\Application\chrome.exe", "admin"));
            entries.Add(Entry(4, DateTime.UtcNow.AddMinutes(-14), "lockdown.trigger", @"Blocked connection attempt: C:\Users\Public\update.exe", "monitor"));
            entries.Add(Entry(3, DateTime.UtcNow.AddMinutes(-15), "lockdown.expired", "Punishment timer elapsed", "service"));
            entries.Add(Entry(2, DateTime.UtcNow.AddMinutes(-61), "arm", "Protection armed", "admin"));
            entries.Add(Entry(1, DateTime.UtcNow.AddMinutes(-62), "service.start", "AegisWall " + ProtectionEngine.Version, "service"));
            return entries;
        }

        public static LastCommandResult LastCommand()
        {
            LastCommandResult result = new LastCommandResult();
            result.Ok = true;
            result.Message = "Beacon blocked by AegisWall";
            result.Utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            result.Data = Json.Serialize(new
            {
                beacon = @"C:\Temp\AegisWallTest\beacon.exe",
                exitCode = 7,
                blocked = true,
                blockedAttemptsBefore = 12,
                blockedAttemptsAfter = 13,
                lockdown = true
            });
            return result;
        }

        private static AllowedApp App(string name, string path, bool inbound)
        {
            AllowedApp app = new AllowedApp();
            app.Name = name;
            app.Path = path;
            app.Outbound = true;
            app.Inbound = inbound;
            app.Enabled = true;
            app.Notes = inbound ? "Inbound allowed for calls" : string.Empty;
            app.BlockedCount = name == "Steam" ? 3 : 0;
            app.AddedUtc = DateTime.UtcNow.AddDays(-7).ToString("o", CultureInfo.InvariantCulture);
            app.LastSeenUtc = DateTime.UtcNow.AddMinutes(-4).ToString("o", CultureInfo.InvariantCulture);
            return app;
        }

        private static AuditEntry Entry(long seq, DateTime utc, string eventName, string detail, string actor)
        {
            AuditEntry entry = new AuditEntry();
            entry.Seq = seq;
            entry.Utc = utc.ToString("o", CultureInfo.InvariantCulture);
            entry.Event = eventName;
            entry.Detail = detail;
            entry.Actor = actor;
            entry.Prev = new string('0', 64);
            entry.Hash = new string('0', 64);
            return entry;
        }
    }
}
