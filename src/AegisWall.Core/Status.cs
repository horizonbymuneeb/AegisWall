using System;
using System.Globalization;
using System.IO;

namespace AegisWall.Core
{
    public sealed class StatusSnapshot
    {
        public string State { get; set; }
        public bool Armed { get; set; }
        public bool KeyPresent { get; set; }
        public bool KeyEnrolled { get; set; }
        public bool ServiceRunning { get; set; }
        public bool MonitorAvailable { get; set; }
        public bool FiltersApplied { get; set; }
        public bool ConfigVerified { get; set; }
        public string LockdownUntilUtc { get; set; }
        public string LockdownReason { get; set; }
        public string LastEvent { get; set; }
        public string UpdatedUtc { get; set; }
        public string ServiceVersion { get; set; }
        public string KeySerial { get; set; }
        public string KeyProvider { get; set; }
        public string DataDir { get; set; }
        public int AllowedApps { get; set; }
        public int LockdownMinutes { get; set; }
        public int LockdownRemainingSeconds { get; set; }
        public int BlockedAttempts { get; set; }
        public int LockdownCount { get; set; }
        public int KeyGraceRemainingSeconds { get; set; }
        public int FilterCount { get; set; }
        public string LastBlockedApp { get; set; }
        public string LastBlockedUtc { get; set; }
        public string DnsMode { get; set; }
        public string TriggerMode { get; set; }
        public string KeyRemovalAction { get; set; }
        public bool KernelDriverEnabled { get; set; }
        public bool WatchdogEnabled { get; set; }
        public bool AutoStart { get; set; }
        public bool TestMode { get; set; }
        public int TestLockdownSeconds { get; set; }
        public string WdacPolicyPath { get; set; }
        public int GraceSeconds { get; set; }
        public string ServiceStartedUtc { get; set; }

        public StatusSnapshot()
        {
            State = "Unknown";
            LockdownUntilUtc = string.Empty;
            LockdownReason = string.Empty;
            LastEvent = string.Empty;
            UpdatedUtc = string.Empty;
            ServiceVersion = "0.1.0";
            KeySerial = string.Empty;
            KeyProvider = string.Empty;
            DataDir = string.Empty;
            LockdownMinutes = 30;
            LastBlockedApp = string.Empty;
            LastBlockedUtc = string.Empty;
            DnsMode = "System";
            TriggerMode = "UserAppsOnly";
            KeyRemovalAction = "BlockAll";
            WdacPolicyPath = string.Empty;
            GraceSeconds = 30;
            TestMode = false;
            TestLockdownSeconds = 60;
            ServiceStartedUtc = string.Empty;
        }

        public static StatusSnapshot NotInstalled()
        {
            StatusSnapshot status = new StatusSnapshot();
            status.State = "NotInstalled";
            status.ServiceRunning = false;
            status.UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return status;
        }
    }

    public sealed class LastCommandResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
        public string Utc { get; set; }

        public LastCommandResult()
        {
            Message = string.Empty;
            Data = string.Empty;
            Utc = string.Empty;
        }
    }

    public static class StatusStore
    {
        public static void Write(StatusSnapshot status)
        {
            status.UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            status.DataDir = ConfigStore.DataDir;
            ConfigStore.WriteAtomic(ConfigStore.StatusPath, Json.Serialize(status), true);
        }

        public static StatusSnapshot Read()
        {
            try
            {
                if (!File.Exists(ConfigStore.StatusPath))
                {
                    return StatusSnapshot.NotInstalled();
                }

                StatusSnapshot status = Json.Deserialize<StatusSnapshot>(File.ReadAllText(ConfigStore.StatusPath));
                if (status == null)
                {
                    return StatusSnapshot.NotInstalled();
                }

                DateTime until;
                if (DateTime.TryParse(status.LockdownUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until) && until > DateTime.UtcNow)
                {
                    status.LockdownRemainingSeconds = (int)Math.Max(0, (until - DateTime.UtcNow).TotalSeconds);
                }
                else
                {
                    status.LockdownRemainingSeconds = 0;
                }
                return status;
            }
            catch
            {
                return StatusSnapshot.NotInstalled();
            }
        }
    }
}

