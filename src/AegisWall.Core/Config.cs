using System;
using System.Collections.Generic;
using System.Globalization;

namespace AegisWall.Core
{
    public sealed class AegisConfig
    {
        public int Version { get; set; }
        public bool Armed { get; set; }
        public bool RequireKeyAtBoot { get; set; }
        public int LockdownMinutes { get; set; }
        public string TriggerMode { get; set; }
        public int GraceSeconds { get; set; }
        public bool LockdownOnKeyRemoval { get; set; }
        public bool ExcludeSystemProcesses { get; set; }
        public bool AllowLoopback { get; set; }
        public bool AllowDhcp { get; set; }
        public bool AllowSystemDns { get; set; }
        public string DnsMode { get; set; }
        public List<string> DnsServers { get; set; }
        public List<AllowedApp> AllowedApps { get; set; }
        public string KeyRemovalAction { get; set; }
        public bool WatchdogEnabled { get; set; }
        public bool TestMode { get; set; }
        public int TestLockdownSeconds { get; set; }
        public bool AutoStart { get; set; }
        public bool KernelDriverEnabled { get; set; }
        public string WdacPolicyPath { get; set; }
        public int BlockedAttempts { get; set; }
        public int LockdownCount { get; set; }
        public string LastBlockedApp { get; set; }
        public string LastBlockedUtc { get; set; }

        public string KeyProvider { get; set; }
        public string KeySerial { get; set; }
        public string KeyChallenge { get; set; }
        public string KeyEnrolledUtc { get; set; }
        public string VaultBlob { get; set; }
        public string VaultSalt { get; set; }
        public string RecoveryBlob { get; set; }
        public string RecoverySalt { get; set; }
        public string YkmanPath { get; set; }
        public string UsbVolumeLabel { get; set; }

        public string LockdownUntilUtc { get; set; }
        public string LockdownReason { get; set; }
        public string InstalledUtc { get; set; }
        public string ServiceVersion { get; set; }
        public string Signature { get; set; }
        public string LastGoodHash { get; set; }

        public AegisConfig()
        {
            Version = 1;
            Armed = false;
            RequireKeyAtBoot = true;
            LockdownMinutes = 30;
            TriggerMode = "UserAppsOnly";
            GraceSeconds = 30;
            LockdownOnKeyRemoval = true;
            ExcludeSystemProcesses = true;
            AllowLoopback = true;
            AllowDhcp = true;
            AllowSystemDns = true;
            DnsMode = "System";
            DnsServers = new List<string>();
            AllowedApps = new List<AllowedApp>();
            KeyRemovalAction = "BlockAll";
            WatchdogEnabled = true;
            TestMode = false;
            TestLockdownSeconds = 60;
            AutoStart = true;
            KernelDriverEnabled = false;
            WdacPolicyPath = string.Empty;
            BlockedAttempts = 0;
            LockdownCount = 0;
            LastBlockedApp = string.Empty;
            LastBlockedUtc = string.Empty;
            KeyProvider = "yubikey";
            KeySerial = string.Empty;
            KeyChallenge = string.Empty;
            KeyEnrolledUtc = string.Empty;
            VaultBlob = string.Empty;
            VaultSalt = string.Empty;
            RecoveryBlob = string.Empty;
            RecoverySalt = string.Empty;
            YkmanPath = string.Empty;
            UsbVolumeLabel = "AEGISWALL";
            LockdownUntilUtc = string.Empty;
            LockdownReason = string.Empty;
            InstalledUtc = string.Empty;
            ServiceVersion = "0.1.1";
            Signature = string.Empty;
            LastGoodHash = string.Empty;
        }

        public static AegisConfig Default()
        {
            AegisConfig config = new AegisConfig();
            config.InstalledUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return config;
        }

        public bool IsKeyEnrolled
        {
            get { return !string.IsNullOrEmpty(VaultBlob) && !string.IsNullOrEmpty(KeyChallenge); }
        }

        public bool IsLockdownActive
        {
            get
            {
                DateTime until;
                return DateTime.TryParse(LockdownUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until)
                    && until > DateTime.UtcNow;
            }
        }

        public DateTime LockdownUntil
        {
            get
            {
                DateTime until;
                if (DateTime.TryParse(LockdownUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until))
                {
                    return until;
                }
                return DateTime.MinValue;
            }
        }
    }

    public sealed class AllowedApp
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool Outbound { get; set; }
        public bool Inbound { get; set; }
        public bool Enabled { get; set; }
        public string Notes { get; set; }
        public string Sha256 { get; set; }
        public string AddedUtc { get; set; }
        public string LastSeenUtc { get; set; }
        public int BlockedCount { get; set; }

        public AllowedApp()
        {
            Name = string.Empty;
            Path = string.Empty;
            Outbound = true;
            Inbound = false;
            Enabled = true;
            Notes = string.Empty;
            Sha256 = string.Empty;
            AddedUtc = string.Empty;
            LastSeenUtc = string.Empty;
            BlockedCount = 0;
        }
    }

    public static class ConfigSigner
    {
        public static string Canonical(AegisConfig config)
        {
            SortedDictionary<string, object> payload = new SortedDictionary<string, object>(StringComparer.Ordinal);
            payload["version"] = config.Version;
            payload["armed"] = config.Armed;
            payload["requireKeyAtBoot"] = config.RequireKeyAtBoot;
            payload["lockdownMinutes"] = config.LockdownMinutes;
            payload["triggerMode"] = config.TriggerMode ?? string.Empty;
            payload["graceSeconds"] = config.GraceSeconds;
            payload["lockdownOnKeyRemoval"] = config.LockdownOnKeyRemoval;
            payload["excludeSystemProcesses"] = config.ExcludeSystemProcesses;
            payload["allowLoopback"] = config.AllowLoopback;
            payload["allowDhcp"] = config.AllowDhcp;
            payload["allowSystemDns"] = config.AllowSystemDns;
            payload["dnsMode"] = config.DnsMode ?? string.Empty;
            payload["keyRemovalAction"] = config.KeyRemovalAction ?? string.Empty;
            payload["watchdogEnabled"] = config.WatchdogEnabled;
            payload["testMode"] = config.TestMode;
            payload["testLockdownSeconds"] = config.TestLockdownSeconds;
            payload["autoStart"] = config.AutoStart;
            payload["kernelDriverEnabled"] = config.KernelDriverEnabled;
            payload["wdacPolicyPath"] = config.WdacPolicyPath ?? string.Empty;

            List<string> dns = new List<string>(config.DnsServers ?? new List<string>());
            dns.Sort(StringComparer.OrdinalIgnoreCase);
            payload["dnsServers"] = dns;

            List<AllowedApp> apps = new List<AllowedApp>(config.AllowedApps ?? new List<AllowedApp>());
            apps.Sort(delegate(AllowedApp a, AllowedApp b)
            {
                return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });

            List<Dictionary<string, object>> appPayload = new List<Dictionary<string, object>>();
            foreach (AllowedApp app in apps)
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["name"] = app.Name ?? string.Empty;
                item["path"] = PathUtil.Normalize(app.Path);
                item["outbound"] = app.Outbound;
                item["inbound"] = app.Inbound;
                item["enabled"] = app.Enabled;
                item["notes"] = app.Notes ?? string.Empty;
                item["sha256"] = (app.Sha256 ?? string.Empty).ToLowerInvariant();
                appPayload.Add(item);
            }
            payload["allowedApps"] = appPayload;

            payload["keyProvider"] = config.KeyProvider ?? string.Empty;
            payload["usbVolumeLabel"] = config.UsbVolumeLabel ?? string.Empty;
            payload["keySerial"] = config.KeySerial ?? string.Empty;
            payload["keyChallenge"] = config.KeyChallenge ?? string.Empty;
            payload["vaultBlob"] = config.VaultBlob ?? string.Empty;
            payload["vaultSalt"] = config.VaultSalt ?? string.Empty;
            payload["recoveryBlob"] = config.RecoveryBlob ?? string.Empty;
            payload["recoverySalt"] = config.RecoverySalt ?? string.Empty;
            payload["lockdownUntilUtc"] = config.LockdownUntilUtc ?? string.Empty;
            payload["lockdownReason"] = config.LockdownReason ?? string.Empty;
            payload["installedUtc"] = config.InstalledUtc ?? string.Empty;

            return Json.Serialize(payload);
        }

        public static void Sign(AegisConfig config, byte[] vaultKey)
        {
            if (vaultKey == null)
            {
                config.Signature = string.Empty;
                config.LastGoodHash = string.Empty;
                return;
            }

            string canonical = Canonical(config);
            config.LastGoodHash = Crypto.Sha256Hex(canonical);
            config.Signature = Crypto.HmacSha256Base64(vaultKey, canonical);
        }

        public static bool Verify(AegisConfig config, byte[] vaultKey)
        {
            if (vaultKey == null || string.IsNullOrEmpty(config.Signature))
            {
                return false;
            }

            string expected = Crypto.HmacSha256Base64(vaultKey, Canonical(config));
            return Crypto.FixedTimeEquals(expected, config.Signature);
        }
    }
}

