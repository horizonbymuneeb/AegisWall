using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace AegisWall.Core
{
    public sealed class ProtectionEngine : IDisposable
    {
        public const string Version = "0.1.1";

        private readonly object gate = new object();
        private readonly WfpEngine wfp = new WfpEngine();
        private readonly Dictionary<string, DateTime> recentBlocks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private AegisConfig config;
        private byte[] vaultKey;
        private IHardwareKey hardwareKey;
        private bool keyPresent;
        private bool configVerified;
        private bool filtersApplied;
        private bool lastKeyMissing;
        private DateTime keyMissingSinceUtc = DateTime.MinValue;
        private DateTime startedUtc = DateTime.MinValue;
        private string lastError = string.Empty;
        private string lastEvent = string.Empty;
        private IpcServer ipc;
        private BlockEventMonitor monitor;
        private Timer tickTimer;
        private Timer keyTimer;
        private Timer statusTimer;
        private bool started;

        public AegisConfig Config
        {
            get { return config; }
        }

        public bool MonitorAvailable
        {
            get { return monitor != null && monitor.Available; }
        }

        public void Start()
        {
            lock (gate)
            {
                if (started)
                {
                    return;
                }

                ConfigStore.EnsureDataDir();
                config = ConfigStore.Load();
                hardwareKey = null;
                vaultKey = null;
                configVerified = false;
                filtersApplied = false;
                keyPresent = false;
                keyMissingSinceUtc = DateTime.MinValue;
                startedUtc = DateTime.UtcNow;
                lastError = string.Empty;
                lastEvent = "service.start";

                if (config.IsKeyEnrolled)
                {
                    try
                    {
                        hardwareKey = HardwareKeyFactory.Create(config);
                        keyPresent = hardwareKey.IsPresent();
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }
                }

                try
                {
                    wfp.Open();
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }

                ApplyLocked();
                ipc = new IpcServer(Handle);
                ipc.Start();
                monitor = new BlockEventMonitor(this);
                monitor.Start();
                tickTimer = new Timer(Tick, null, 1000, 1000);
                keyTimer = new Timer(PollKey, null, 10000, 10000);
                statusTimer = new Timer(WriteStatusTimer, null, 500, 2000);
                started = true;
                AuditLog.Append("service.start", "AegisWall " + Version, "service");
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                if (!started)
                {
                    return;
                }

                started = false;
                if (tickTimer != null) { tickTimer.Dispose(); tickTimer = null; }
                if (keyTimer != null) { keyTimer.Dispose(); keyTimer = null; }
                if (statusTimer != null) { statusTimer.Dispose(); statusTimer = null; }
                if (monitor != null) { monitor.Dispose(); monitor = null; }
                if (ipc != null) { ipc.Dispose(); ipc = null; }
                wfp.Close();
                AuditLog.Append("service.stop", "Persistent filters remain in place while armed.", "service");
            }
        }

        public void Dispose()
        {
            Stop();
            wfp.Dispose();
        }

        public void OnBlockedConnection(string application, int processId, string filterName)
        {
            lock (gate)
            {
                if (!config.Armed || vaultKey == null)
                {
                    return;
                }

                if (string.Equals(config.TriggerMode, "Manual", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string path = string.Empty;
                if (processId > 0)
                {
                    path = PathUtil.ProcessPath(processId);
                }
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = application;
                }
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = "unknown process";
                }
                if (path.IndexOf("AegisWall", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                if (config.ExcludeSystemProcesses
                    && !string.Equals(config.TriggerMode, "All", StringComparison.OrdinalIgnoreCase)
                    && PathUtil.IsWindowsComponent(path))
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                config.BlockedAttempts++;
                config.LastBlockedApp = path;
                config.LastBlockedUtc = now.ToString("o", CultureInfo.InvariantCulture);
                foreach (AllowedApp app in config.AllowedApps)
                {
                    if (PathUtil.Key(app.Path) == PathUtil.Key(path))
                    {
                        app.BlockedCount++;
                        app.LastSeenUtc = config.LastBlockedUtc;
                        break;
                    }
                }

                DateTime last;
                if (recentBlocks.TryGetValue(path, out last) && (now - last).TotalSeconds < 60)
                {
                    return;
                }
                recentBlocks[path] = now;

                TriggerLockdown("Blocked connection attempt: " + path + " (" + filterName + ")", "monitor");
            }
        }

        public void TriggerLockdown(string reason, string actor)
        {
            lock (gate)
            {
                if (!config.Armed || vaultKey == null)
                {
                    return;
                }

                DateTime until = config.TestMode ? DateTime.UtcNow.AddSeconds(Math.Max(15, config.TestLockdownSeconds)) : DateTime.UtcNow.AddMinutes(config.LockdownMinutes);
                if (config.LockdownUntil > until)
                {
                    until = config.LockdownUntil;
                }

                config.LockdownUntilUtc = until.ToString("o", CultureInfo.InvariantCulture);
                config.LockdownReason = (config.TestMode ? "[TEST] " : string.Empty) + reason;
                config.LockdownCount++;
                lastEvent = "lockdown.trigger";
                SaveAndApply(vaultKey);
                AuditLog.Append("lockdown.trigger", reason, actor ?? "monitor");
            }
        }

        private IpcResponse Handle(IpcRequest request)
        {
            lock (gate)
            {
                try
                {
                    string cmd = (request.Cmd ?? string.Empty).Trim().ToLowerInvariant();
                    switch (cmd)
                    {
                        case "ping":
                            return IpcResponse.Success("pong");
                        case "status":
                            return StatusResponse();
                        case "arm":
                            return Arm();
                        case "disarm":
                            return Disarm(request.RecoveryCode);
                        case "unlock":
                            return Unlock();
                        case "lockdown":
                            return ManualLockdown(request.Minutes);
                        case "allow":
                            return Allow(request);
                        case "update-app":
                            return UpdateApp(request);
                        case "remove":
                            return Remove(request.Path);
                        case "list":
                            return ListApps();
                        case "export":
                            return ListApps();
                        case "import":
                            return Import(request);
                        case "set":
                            return Set(request.Payload);
                        case "reload":
                            return Reload();
                        case "verify":
                            return Verify();
                        case "key-status":
                            return KeyStatus();
                        case "rotate-recovery":
                            return RotateRecovery();
                        case "audit":
                            return Audit(request.Limit);
                        case "diagnostics":
                            return Diagnostics();
                        case "preview":
                            return Preview();
                        default:
                            return IpcResponse.Error("Unknown command: " + cmd);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    AuditLog.Append("command.error", request.Cmd + ": " + ex.Message, "service");
                    return IpcResponse.Error(ex.Message);
                }
            }
        }

        private IpcResponse StatusResponse()
        {
            IpcResponse response = IpcResponse.Success("ok");
            response.Status = BuildStatus();
            return response;
        }

        private IpcResponse Arm()
        {
            if (!config.IsKeyEnrolled)
            {
                return IpcResponse.Error("Enroll a YubiKey before arming AegisWall.");
            }
            if (vaultKey == null)
            {
                return IpcResponse.Error("Hardware key is locked. Insert the key and run unlock.");
            }
            if (!ConfigSigner.Verify(config, vaultKey))
            {
                return IpcResponse.Error("Config signature is invalid. Use recovery or re-enroll.");
            }

            config.Armed = true;
            lastEvent = "arm";
            SaveAndApply(vaultKey);
            AuditLog.Append("arm", "Protection armed", "admin");
            return StatusResponse();
        }

        private IpcResponse Disarm(string recoveryCode)
        {
            byte[] signingKey = vaultKey;
            if (signingKey == null && !string.IsNullOrWhiteSpace(recoveryCode))
            {
                signingKey = KeyVault.Recover(config, recoveryCode);
                if (signingKey == null)
                {
                    return IpcResponse.Error("Recovery code is invalid.");
                }
            }
            if (signingKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key or provide the recovery code.");
            }

            config.Armed = false;
            config.LockdownUntilUtc = string.Empty;
            config.LockdownReason = string.Empty;
            lastEvent = "disarm";
            SaveAndApply(signingKey);
            AuditLog.Append("disarm", "Protection disarmed", "admin");
            return StatusResponse();
        }

        private IpcResponse Unlock()
        {
            if (!config.IsKeyEnrolled)
            {
                return IpcResponse.Error("No hardware key is enrolled.");
            }

            if (vaultKey == null)
            {
                IHardwareKey key;
                try
                {
                    key = hardwareKey ?? HardwareKeyFactory.Create(config);
                    byte[] candidate = KeyVault.Unlock(config, key);
                    if (candidate == null)
                    {
                        return IpcResponse.Error("Key not detected, wrong key, or touch was not confirmed.");
                    }
                    if (!ConfigSigner.Verify(config, candidate))
                    {
                        return IpcResponse.Error("Config signature is invalid. Use recovery.");
                    }
                    vaultKey = candidate;
                    configVerified = true;
                    keyPresent = true;
                    keyMissingSinceUtc = DateTime.MinValue;
                    AuditLog.Append("key.unlock", "Hardware key unlocked", "admin");
                }
                catch (Exception ex)
                {
                    return IpcResponse.Error(ex.Message);
                }
            }

            config.LockdownUntilUtc = string.Empty;
            config.LockdownReason = string.Empty;
            lastEvent = "unlock";
            SaveAndApply(vaultKey);
            AuditLog.Append("unlock", "Lockdown cleared", "admin");
            return StatusResponse();
        }

        private IpcResponse ManualLockdown(int minutes)
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            int duration = minutes > 0 ? minutes : config.LockdownMinutes;
            if (duration < 1) { duration = 1; }
            if (duration > 1440) { duration = 1440; }

            config.LockdownUntilUtc = (config.TestMode ? DateTime.UtcNow.AddSeconds(Math.Max(15, config.TestLockdownSeconds)) : DateTime.UtcNow.AddMinutes(duration)).ToString("o", CultureInfo.InvariantCulture);
            config.LockdownReason = (config.TestMode ? "[TEST] " : string.Empty) + "Manual lockdown";
            config.LockdownCount++;
            lastEvent = "lockdown.manual";
            SaveAndApply(vaultKey);
            AuditLog.Append("lockdown.manual", duration + " minutes", "admin");
            return StatusResponse();
        }

        private IpcResponse Allow(IpcRequest request)
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            string full = PathUtil.Normalize(request.Path);
            if (string.IsNullOrWhiteSpace(full) || !File.Exists(full))
            {
                return IpcResponse.Error("File not found: " + request.Path);
            }

            string key = PathUtil.Key(full);
            for (int i = config.AllowedApps.Count - 1; i >= 0; i--)
            {
                if (PathUtil.Key(config.AllowedApps[i].Path) == key)
                {
                    config.AllowedApps.RemoveAt(i);
                }
            }

            AllowedApp app = new AllowedApp();
            app.Name = Path.GetFileNameWithoutExtension(full);
            app.Path = full;
            app.Outbound = request.Outbound;
            app.Inbound = request.Inbound;
            app.Enabled = request.Enabled;
            app.Notes = request.Notes ?? string.Empty;
            app.Sha256 = PathUtil.Sha256File(full);
            app.AddedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            config.AllowedApps.Add(app);
            SortApps();

            lastEvent = "allow";
            SaveAndApply(vaultKey);
            AuditLog.Append("allow", full + " out=" + app.Outbound + " in=" + app.Inbound, "admin");
            return StatusResponse();
        }

        private IpcResponse UpdateApp(IpcRequest request)
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            string key = PathUtil.Key(request.Path);
            AllowedApp found = null;
            foreach (AllowedApp app in config.AllowedApps)
            {
                if (PathUtil.Key(app.Path) == key)
                {
                    found = app;
                    break;
                }
            }
            if (found == null)
            {
                return IpcResponse.Error("App is not in the allowlist.");
            }

            found.Outbound = request.Outbound;
            found.Inbound = request.Inbound;
            found.Enabled = request.Enabled;
            found.Notes = request.Notes ?? found.Notes ?? string.Empty;
            if (File.Exists(found.Path))
            {
                found.Sha256 = PathUtil.Sha256File(found.Path);
            }

            lastEvent = "app.update";
            SaveAndApply(vaultKey);
            AuditLog.Append("app.update", found.Path + " out=" + found.Outbound + " in=" + found.Inbound + " enabled=" + found.Enabled, "admin");
            return StatusResponse();
        }

        private IpcResponse Import(IpcRequest request)
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            List<AllowedApp> imported = Json.Deserialize<List<AllowedApp>>(request.Payload ?? "[]");
            if (imported == null || imported.Count == 0)
            {
                return IpcResponse.Error("Import payload is empty.");
            }

            if (request.Replace)
            {
                config.AllowedApps.Clear();
            }

            int added = 0;
            foreach (AllowedApp app in imported)
            {
                string full = PathUtil.Normalize(app.Path);
                if (string.IsNullOrWhiteSpace(full))
                {
                    continue;
                }

                int existing = -1;
                for (int i = 0; i < config.AllowedApps.Count; i++)
                {
                    if (PathUtil.Key(config.AllowedApps[i].Path) == PathUtil.Key(full))
                    {
                        existing = i;
                        break;
                    }
                }

                if (existing >= 0)
                {
                    config.AllowedApps.RemoveAt(existing);
                }

                app.Path = full;
                if (string.IsNullOrEmpty(app.Name)) { app.Name = Path.GetFileNameWithoutExtension(full); }
                if (app.Notes == null) { app.Notes = string.Empty; }
                if (!app.Outbound && !app.Inbound && app.Enabled) { app.Outbound = true; }
                if (string.IsNullOrEmpty(app.AddedUtc)) { app.AddedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); }
                if (File.Exists(full)) { app.Sha256 = PathUtil.Sha256File(full); }
                config.AllowedApps.Add(app);
                added++;
            }

            SortApps();
            lastEvent = "import";
            SaveAndApply(vaultKey);
            AuditLog.Append("import", added + " apps imported", "admin");
            return StatusResponse();
        }

        private IpcResponse RotateRecovery()
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            string code = KeyVault.RotateRecovery(config, vaultKey);
            if (string.IsNullOrEmpty(code))
            {
                return IpcResponse.Error("Could not rotate the recovery code.");
            }

            SaveAndApply(vaultKey);
            AuditLog.Append("recovery.rotate", "Recovery code rotated", "admin");
            IpcResponse response = StatusResponse();
            response.Data = code;
            return response;
        }

        private IpcResponse Audit(int limit)
        {
            if (limit <= 0) { limit = 100; }
            if (limit > 1000) { limit = 1000; }
            IpcResponse response = IpcResponse.Success("ok");
            response.Data = Json.Serialize(AuditLog.Read(limit));
            response.Status = BuildStatus();
            return response;
        }

        private IpcResponse Diagnostics()
        {
            IpcResponse response = IpcResponse.Success("ok");
            response.Data = Json.Serialize(new
            {
                version = Version,
                serviceStartedUtc = startedUtc.ToString("o", CultureInfo.InvariantCulture),
                dataDir = ConfigStore.DataDir,
                configPath = ConfigStore.ConfigPath,
                wfpOpen = wfp.IsOpen,
                filterCount = wfp.LastFilterCount,
                monitorAvailable = MonitorAvailable,
                auditChain = AuditLog.VerifyChain(),
                keyEnrolled = config.IsKeyEnrolled,
                keyPresent = keyPresent,
                keyUnlocked = vaultKey != null,
                configVerified = configVerified,
                armed = config.Armed,
                lockdown = config.IsLockdownActive,
                blockedAttempts = config.BlockedAttempts,
                lockdownCount = config.LockdownCount,
                lastBlockedApp = config.LastBlockedApp,
                dnsMode = config.DnsMode,
                kernelDriverEnabled = config.KernelDriverEnabled,
                wdacPolicyPath = config.WdacPolicyPath,
                testMode = config.TestMode,
                testLockdownSeconds = config.TestLockdownSeconds
            });
            response.Status = BuildStatus();
            return response;
        }

        private IpcResponse Preview()
        {
            int outbound = 0;
            int inbound = 0;
            int disabled = 0;
            foreach (AllowedApp app in config.AllowedApps)
            {
                if (!app.Enabled) { disabled++; continue; }
                if (app.Outbound) { outbound++; }
                if (app.Inbound) { inbound++; }
            }

            int filters = 4;
            if (config.AllowLoopback) { filters += 4; }
            if (config.AllowDhcp) { filters += 4; }
            if (config.AllowSystemDns) { filters += (string.Equals(config.DnsMode, "Pinned", StringComparison.OrdinalIgnoreCase) ? 4 * Math.Max(1, config.DnsServers.Count) : 4); }
            filters += outbound * 2 + inbound * 2;
            if (config.IsLockdownActive) { filters += 4; }

            IpcResponse response = IpcResponse.Success("ok");
            response.Data = Json.Serialize(new
            {
                outboundApps = outbound,
                inboundApps = inbound,
                disabledApps = disabled,
                estimatedFilters = filters,
                dnsMode = config.DnsMode,
                lockdown = config.IsLockdownActive,
                keyMissing = config.Armed && config.IsKeyEnrolled && vaultKey == null,
                testMode = config.TestMode,
                testLockdownSeconds = config.TestLockdownSeconds
            });
            response.Status = BuildStatus();
            return response;
        }

        private void SortApps()
        {
            config.AllowedApps.Sort(delegate(AllowedApp a, AllowedApp b)
            {
                return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
        }

        private IpcResponse Remove(string path)
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            string key = PathUtil.Key(path);
            int removed = 0;
            for (int i = config.AllowedApps.Count - 1; i >= 0; i--)
            {
                if (PathUtil.Key(config.AllowedApps[i].Path) == key)
                {
                    config.AllowedApps.RemoveAt(i);
                    removed++;
                }
            }

            if (removed == 0)
            {
                return IpcResponse.Error("App is not in the allowlist.");
            }

            lastEvent = "remove";
            SaveAndApply(vaultKey);
            AuditLog.Append("remove", PathUtil.Normalize(path), "admin");
            return StatusResponse();
        }

        private IpcResponse ListApps()
        {
            IpcResponse response = IpcResponse.Success("ok");
            response.Data = Json.Serialize(config.AllowedApps);
            response.Status = BuildStatus();
            return response;
        }

        private IpcResponse Set(string payload)
        {
            if (vaultKey == null)
            {
                return IpcResponse.Error("Unlock with the hardware key first.");
            }

            Dictionary<string, object> patch = Json.DeserializeObject(payload ?? "{}");
            if (patch == null)
            {
                return IpcResponse.Error("Invalid settings payload.");
            }

            if (patch.ContainsKey("lockdownMinutes"))
            {
                int minutes = Convert.ToInt32(patch["lockdownMinutes"], CultureInfo.InvariantCulture);
                if (minutes < 1) { minutes = 1; }
                if (minutes > 1440) { minutes = 1440; }
                config.LockdownMinutes = minutes;
            }

            if (patch.ContainsKey("triggerMode"))
            {
                string mode = Convert.ToString(patch["triggerMode"], CultureInfo.InvariantCulture);
                if (mode == "All" || mode == "UserAppsOnly" || mode == "Manual")
                {
                    config.TriggerMode = mode;
                }
            }

            if (patch.ContainsKey("graceSeconds"))
            {
                int grace = Convert.ToInt32(patch["graceSeconds"], CultureInfo.InvariantCulture);
                if (grace < 0) { grace = 0; }
                if (grace > 3600) { grace = 3600; }
                config.GraceSeconds = grace;
            }

            if (patch.ContainsKey("lockdownOnKeyRemoval")) { config.LockdownOnKeyRemoval = Convert.ToBoolean(patch["lockdownOnKeyRemoval"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("excludeSystemProcesses")) { config.ExcludeSystemProcesses = Convert.ToBoolean(patch["excludeSystemProcesses"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("allowLoopback")) { config.AllowLoopback = Convert.ToBoolean(patch["allowLoopback"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("allowDhcp")) { config.AllowDhcp = Convert.ToBoolean(patch["allowDhcp"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("allowSystemDns")) { config.AllowSystemDns = Convert.ToBoolean(patch["allowSystemDns"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("requireKeyAtBoot")) { config.RequireKeyAtBoot = Convert.ToBoolean(patch["requireKeyAtBoot"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("watchdogEnabled")) { config.WatchdogEnabled = Convert.ToBoolean(patch["watchdogEnabled"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("testMode"))
            {
                config.TestMode = Convert.ToBoolean(patch["testMode"], CultureInfo.InvariantCulture);
                if (config.TestMode && config.IsLockdownActive)
                {
                    config.LockdownUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(15, config.TestLockdownSeconds)).ToString("o", CultureInfo.InvariantCulture);
                    if (!config.LockdownReason.StartsWith("[TEST]", StringComparison.Ordinal)) { config.LockdownReason = "[TEST] " + config.LockdownReason; }
                }
            }
            if (patch.ContainsKey("testLockdownSeconds"))
            {
                int seconds = Convert.ToInt32(patch["testLockdownSeconds"], CultureInfo.InvariantCulture);
                if (seconds < 15) { seconds = 15; }
                if (seconds > 3600) { seconds = 3600; }
                config.TestLockdownSeconds = seconds;
            }
            if (patch.ContainsKey("autoStart")) { config.AutoStart = Convert.ToBoolean(patch["autoStart"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("kernelDriverEnabled")) { config.KernelDriverEnabled = Convert.ToBoolean(patch["kernelDriverEnabled"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("ykmanPath")) { config.YkmanPath = Convert.ToString(patch["ykmanPath"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("usbVolumeLabel")) { config.UsbVolumeLabel = Convert.ToString(patch["usbVolumeLabel"], CultureInfo.InvariantCulture); }
            if (patch.ContainsKey("wdacPolicyPath")) { config.WdacPolicyPath = Convert.ToString(patch["wdacPolicyPath"], CultureInfo.InvariantCulture); }

            if (patch.ContainsKey("dnsMode"))
            {
                string dnsMode = Convert.ToString(patch["dnsMode"], CultureInfo.InvariantCulture);
                if (dnsMode == "System" || dnsMode == "Pinned")
                {
                    config.DnsMode = dnsMode;
                }
            }

            if (patch.ContainsKey("keyRemovalAction"))
            {
                string action = Convert.ToString(patch["keyRemovalAction"], CultureInfo.InvariantCulture);
                if (action == "BlockAll" || action == "Lockdown")
                {
                    config.KeyRemovalAction = action;
                }
            }

            if (patch.ContainsKey("dnsServers"))
            {
                List<string> servers = new List<string>();
                object raw = patch["dnsServers"];
                IEnumerable items = raw as IEnumerable;
                if (items != null && !(raw is string))
                {
                    foreach (object item in items)
                    {
                        string server = Convert.ToString(item, CultureInfo.InvariantCulture);
                        if (!string.IsNullOrWhiteSpace(server))
                        {
                            servers.Add(server.Trim());
                        }
                    }
                }
                config.DnsServers = servers;
            }

            lastEvent = "settings";
            SaveAndApply(vaultKey);
            AuditLog.Append("settings", "Settings updated", "admin");
            return StatusResponse();
        }

        private IpcResponse Reload()
        {
            config = ConfigStore.Load();
            hardwareKey = null;
            if (vaultKey != null)
            {
                configVerified = ConfigSigner.Verify(config, vaultKey);
            }
            else
            {
                configVerified = false;
            }

            ApplyLocked();
            WriteStatus();
            lastEvent = "reload";
            AuditLog.Append("reload", "Configuration reloaded", "service");
            return StatusResponse();
        }

        private IpcResponse Verify()
        {
            IpcResponse response = IpcResponse.Success(configVerified ? "verified" : "unverified");
            response.Data = config.LastGoodHash;
            response.Status = BuildStatus();
            return response;
        }

        private IpcResponse KeyStatus()
        {
            IpcResponse response = IpcResponse.Success("ok");
            response.Data = Json.Serialize(new
            {
                enrolled = config.IsKeyEnrolled,
                present = keyPresent,
                serial = config.KeySerial,
                provider = config.KeyProvider,
                unlocked = vaultKey != null
            });
            response.Status = BuildStatus();
            return response;
        }

        private void SaveAndApply(byte[] signingKey)
        {
            if (signingKey == null)
            {
                return;
            }

            ConfigStore.Save(config, signingKey);
            configVerified = ConfigSigner.Verify(config, signingKey);
            ApplyLocked();
            WriteStatus();
        }

        private void ApplyLocked()
        {
            if (!wfp.IsOpen)
            {
                filtersApplied = false;
                return;
            }

            bool keyMissing = config.Armed && config.IsKeyEnrolled && vaultKey == null;
            bool verified = vaultKey != null && ConfigSigner.Verify(config, vaultKey);
            try
            {
                wfp.Apply(config, config.Armed, config.IsLockdownActive, keyMissing, verified);
                filtersApplied = config.Armed;
                lastKeyMissing = keyMissing;
                lastError = string.Empty;
            }
            catch (Exception ex)
            {
                filtersApplied = false;
                lastError = ex.Message;
                AuditLog.Append("wfp.error", ex.Message, "service");
            }
        }

        private void Tick(object state)
        {
            lock (gate)
            {
                if (config.Armed && !string.IsNullOrEmpty(config.LockdownUntilUtc) && !config.IsLockdownActive && vaultKey != null)
                {
                    config.LockdownUntilUtc = string.Empty;
                    config.LockdownReason = string.Empty;
                    lastEvent = "lockdown.expired";
                    SaveAndApply(vaultKey);
                    AuditLog.Append("lockdown.expired", "Punishment timer elapsed", "service");
                }
            }
        }

        private void PollKey(object state)
        {
            lock (gate)
            {
                if (!config.IsKeyEnrolled)
                {
                    keyPresent = false;
                    return;
                }

                bool present;
                try
                {
                    if (hardwareKey == null)
                    {
                        hardwareKey = HardwareKeyFactory.Create(config);
                    }
                    present = hardwareKey.IsPresent();
                }
                catch
                {
                    present = false;
                }

                if (!present && vaultKey != null)
                {
                    if (keyMissingSinceUtc == DateTime.MinValue)
                    {
                        keyMissingSinceUtc = DateTime.UtcNow;
                        AuditLog.Append("key.removed.grace", "Grace period " + config.GraceSeconds + "s", "service");
                    }
                    else if ((DateTime.UtcNow - keyMissingSinceUtc).TotalSeconds >= config.GraceSeconds)
                    {
                        byte[] oldKey = vaultKey;
                        if (config.LockdownOnKeyRemoval
                            || string.Equals(config.KeyRemovalAction, "Lockdown", StringComparison.OrdinalIgnoreCase))
                        {
                            config.LockdownUntilUtc = DateTime.UtcNow.AddMinutes(config.LockdownMinutes).ToString("o", CultureInfo.InvariantCulture);
                            config.LockdownReason = "Hardware key removed";
                            config.LockdownCount++;
                            ConfigStore.Save(config, oldKey);
                        }

                        vaultKey = null;
                        configVerified = false;
                        lastEvent = "key.removed";
                        AuditLog.Append("key.removed", "Hardware key removed; fail-closed", "service");
                        ApplyLocked();
                    }
                }
                else if (present && keyMissingSinceUtc != DateTime.MinValue)
                {
                    keyMissingSinceUtc = DateTime.MinValue;
                    AuditLog.Append("key.returned", "Hardware key returned during grace", "service");
                }

                keyPresent = present;
                bool keyMissing = config.Armed && config.IsKeyEnrolled && vaultKey == null;
                if (keyMissing != lastKeyMissing)
                {
                    ApplyLocked();
                }
            }
        }

        private void WriteStatusTimer(object state)
        {
            lock (gate)
            {
                WriteStatus();
            }
        }

        private void WriteStatus()
        {
            try
            {
                StatusStore.Write(BuildStatus());
            }
            catch
            {
            }
        }

        private StatusSnapshot BuildStatus()
        {
            StatusSnapshot status = new StatusSnapshot();
            status.Armed = config.Armed;
            status.KeyEnrolled = config.IsKeyEnrolled;
            status.KeyPresent = keyPresent;
            status.KeySerial = config.KeySerial;
            status.KeyProvider = config.KeyProvider;
            status.ServiceRunning = started;
            status.MonitorAvailable = MonitorAvailable;
            status.FiltersApplied = filtersApplied;
            status.ConfigVerified = vaultKey != null && ConfigSigner.Verify(config, vaultKey);
            status.LockdownUntilUtc = config.LockdownUntilUtc;
            status.LockdownReason = config.LockdownReason;
            status.LockdownMinutes = config.LockdownMinutes;
            status.AllowedApps = config.AllowedApps == null ? 0 : config.AllowedApps.Count;
            status.LastEvent = lastEvent;
            status.ServiceVersion = Version;
            status.BlockedAttempts = config.BlockedAttempts;
            status.LockdownCount = config.LockdownCount;
            status.LastBlockedApp = config.LastBlockedApp;
            status.LastBlockedUtc = config.LastBlockedUtc;
            status.DnsMode = config.DnsMode;
            status.TriggerMode = config.TriggerMode;
            status.KeyRemovalAction = config.KeyRemovalAction;
            status.KernelDriverEnabled = config.KernelDriverEnabled;
            status.WatchdogEnabled = config.WatchdogEnabled;
            status.TestMode = config.TestMode;
            status.TestLockdownSeconds = config.TestLockdownSeconds;
            status.AutoStart = config.AutoStart;
            status.WdacPolicyPath = config.WdacPolicyPath;
            status.GraceSeconds = config.GraceSeconds;
            status.FilterCount = wfp.LastFilterCount;
            status.ServiceStartedUtc = startedUtc == DateTime.MinValue ? string.Empty : startedUtc.ToString("o", CultureInfo.InvariantCulture);
            if (keyMissingSinceUtc != DateTime.MinValue && vaultKey != null)
            {
                double graceLeft = config.GraceSeconds - (DateTime.UtcNow - keyMissingSinceUtc).TotalSeconds;
                status.KeyGraceRemainingSeconds = (int)Math.Max(0, graceLeft);
            }

            if (!string.IsNullOrEmpty(lastError) && !filtersApplied && config.Armed)
            {
                status.State = "Error";
            }
            else if (!config.Armed)
            {
                status.State = "Disarmed";
            }
            else if (config.IsKeyEnrolled && vaultKey != null && keyMissingSinceUtc != DateTime.MinValue)
            {
                status.State = "KeyGrace";
            }
            else if (config.IsKeyEnrolled && vaultKey == null)
            {
                status.State = "KeyLocked";
            }
            else if (!status.ConfigVerified)
            {
                status.State = "Unverified";
            }
            else if (config.IsLockdownActive)
            {
                status.State = "Lockdown";
            }
            else
            {
                status.State = "Protected";
            }

            DateTime until;
            if (DateTime.TryParse(config.LockdownUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out until) && until > DateTime.UtcNow)
            {
                status.LockdownRemainingSeconds = (int)Math.Max(0, (until - DateTime.UtcNow).TotalSeconds);
            }

            return status;
        }
    }
}







