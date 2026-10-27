using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace AegisWall.Core
{
    public static class ConfigStore
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static string DataDir
        {
            get
            {
                string overrideDir = Environment.GetEnvironmentVariable("AEGISWALL_DATA_DIR");
                if (!string.IsNullOrWhiteSpace(overrideDir))
                {
                    return Path.GetFullPath(overrideDir);
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "AegisWall");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(DataDir, "config.json"); }
        }

        public static string StatusPath
        {
            get { return Path.Combine(DataDir, "status.json"); }
        }

        public static string AuditPath
        {
            get { return Path.Combine(DataDir, "audit.jsonl"); }
        }

        public static string LastCommandPath
        {
            get { return Path.Combine(DataDir, "last-command.json"); }
        }

        public static void EnsureDataDir()
        {
            Directory.CreateDirectory(DataDir);
            if (string.Equals(Environment.GetEnvironmentVariable("AEGISWALL_SKIP_ACL"), "1", StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                SecurityIdentifier system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

                DirectorySecurity security = Directory.GetAccessControl(DataDir);
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                Directory.SetAccessControl(DataDir, security);
            }
            catch
            {
                // ACL hardening is best-effort. The service still runs as SYSTEM.
            }
        }

        public static AegisConfig Load()
        {
            EnsureDataDir();
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return AegisConfig.Default();
                }

                string json = File.ReadAllText(ConfigPath, Utf8);
                AegisConfig config = Json.Deserialize<AegisConfig>(json);
                if (config == null)
                {
                    return AegisConfig.Default();
                }

                if (config.DnsServers == null) { config.DnsServers = new System.Collections.Generic.List<string>(); }
                if (config.AllowedApps == null) { config.AllowedApps = new System.Collections.Generic.List<AllowedApp>(); }
                if (string.IsNullOrEmpty(config.TriggerMode)) { config.TriggerMode = "UserAppsOnly"; }
                if (config.LockdownMinutes <= 0) { config.LockdownMinutes = 30; }
                if (config.LockdownMinutes > 1440) { config.LockdownMinutes = 1440; }
                if (config.GraceSeconds < 0 || config.GraceSeconds > 3600) { config.GraceSeconds = 30; }
                if (string.IsNullOrEmpty(config.DnsMode)) { config.DnsMode = "System"; }
                if (string.IsNullOrEmpty(config.KeyRemovalAction)) { config.KeyRemovalAction = "BlockAll"; }
                if (config.WdacPolicyPath == null) { config.WdacPolicyPath = string.Empty; }
                if (string.IsNullOrEmpty(config.UsbVolumeLabel)) { config.UsbVolumeLabel = "AEGISWALL"; }
                if (config.LastBlockedApp == null) { config.LastBlockedApp = string.Empty; }
                if (config.LastBlockedUtc == null) { config.LastBlockedUtc = string.Empty; }

                foreach (AllowedApp app in config.AllowedApps)
                {
                    if (app.Notes == null) { app.Notes = string.Empty; }
                    if (app.LastSeenUtc == null) { app.LastSeenUtc = string.Empty; }
                    if (app.Sha256 == null) { app.Sha256 = string.Empty; }
                    if (!app.Outbound && !app.Inbound && app.Enabled) { app.Outbound = true; }
                }
                return config;
            }
            catch
            {
                return AegisConfig.Default();
            }
        }

        public static void Save(AegisConfig config, byte[] vaultKey)
        {
            EnsureDataDir();
            ConfigSigner.Sign(config, vaultKey);
            string json = Json.Serialize(config);
            WriteAtomic(ConfigPath, json, true);
        }

        public static void WriteAtomic(string path, string content, bool usersRead)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, content, Utf8);
            HardenFile(temp, usersRead);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null);
                }
                catch
                {
                    File.Delete(path);
                    File.Move(temp, path);
                }
            }
            else
            {
                File.Move(temp, path);
            }

            HardenFile(path, usersRead);
        }

        public static void HardenFile(string path, bool usersRead)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("AEGISWALL_SKIP_ACL"), "1", StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                SecurityIdentifier system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

                FileSecurity security = File.GetAccessControl(path);
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
                if (usersRead)
                {
                    security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Read, AccessControlType.Allow));
                }
                File.SetAccessControl(path, security);
            }
            catch
            {
                // Best-effort.
            }
        }

        public static bool Validate(AegisConfig config, byte[] vaultKey)
        {
            return ConfigSigner.Verify(config, vaultKey);
        }
    }
}
