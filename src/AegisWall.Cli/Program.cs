using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using AegisWall.Core;

namespace AegisWall.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Usage();
                return 2;
            }

            string command = args[0].Trim().ToLowerInvariant();
            bool requiresAdmin = command != "status" && command != "help" && command != "--help" && command != "-h"
                && command != "version" && command != "--version" && command != "watchdog" && command != "installed";

            if (requiresAdmin && !PathUtil.IsAdmin())
            {
                return RelaunchElevated(args);
            }

            try
            {
                switch (command)
                {
                    case "help":
                    case "--help":
                    case "-h":
                        Usage();
                        return 0;
                    case "version":
                    case "--version":
                        Console.WriteLine("AegisWall " + ProtectionEngine.Version);
                        return 0;
                    case "status":
                        return Status(args);
                    case "enroll":
                        return Enroll(args);
                    case "unlock":
                        return SendCommand("unlock", null);
                    case "arm":
                        return SendCommand("arm", null);
                    case "disarm":
                        return Disarm(args);
                    case "lockdown":
                        return Lockdown(args);
                    case "allow":
                        return Allow(args);
                    case "remove":
                        return Remove(args);
                    case "list":
                        return SendCommand("list", null);
                    case "export":
                        return Export(args);
                    case "import":
                        return Import(args);
                    case "update-app":
                        return UpdateApp(args);
                    case "audit":
                        return AuditCommand(args);
                    case "diagnostics":
                        return SendCommand("diagnostics", null);
                    case "rotate-recovery":
                        return SendCommand("rotate-recovery", null);
                    case "preview":
                        return SendCommand("preview", null);
                    case "set":
                        return Set(args);
                    case "reload":
                        return SendCommand("reload", null);
                    case "verify":
                        return SendCommand("verify", null);
                    case "key-status":
                        return SendCommand("key-status", null);
                    case "reset":
                        return Reset(args);
                    case "installed":
                        return Installed(args);
                    case "test-mode":
                        return TestModeCommand(args);
                    case "test-beacon":
                        return TestBeacon();
                    case "test-report":
                        return TestReport(args);
                    case "watchdog":
                        return Watchdog();
                    default:
                        Console.Error.WriteLine("Unknown command: " + command);
                        Usage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                WriteLastCommand(false, ex.Message, string.Empty);
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        private static int Status(string[] args)
        {
            StatusSnapshot status = StatusStore.Read();
            if (IpcClient.IsServiceReachable())
            {
                IpcResponse live = IpcClient.Send(new IpcRequest { Cmd = "status" }, 5000);
                if (live != null && live.Ok && live.Status != null)
                {
                    status = live.Status;
                }
            }

            if (args.Length > 1 && string.Equals(args[1], "--json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(Json.Serialize(status));
                return 0;
            }

            Console.WriteLine("AegisWall " + status.ServiceVersion);
            Console.WriteLine("State           : " + status.State);
            Console.WriteLine("Armed           : " + (status.Armed ? "yes" : "no"));
            Console.WriteLine("Service         : " + (status.ServiceRunning ? "running" : "not reachable"));
            Console.WriteLine("WFP filters     : " + (status.FiltersApplied ? "applied" : "not applied"));
            Console.WriteLine("Config verified : " + (status.ConfigVerified ? "yes" : "no"));
            Console.WriteLine("Hardware key    : " + (status.KeyEnrolled ? (status.KeyPresent ? "present" : "not present") : "not enrolled")
                + (string.IsNullOrEmpty(status.KeySerial) ? string.Empty : " (serial " + status.KeySerial + ")")
                + " [" + status.KeyProvider + "]");
            Console.WriteLine("Allowed apps    : " + status.AllowedApps.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("Blocked attempts: " + status.BlockedAttempts.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("Lockdowns       : " + status.LockdownCount.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("WFP filters     : " + status.FilterCount.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("Trigger monitor : " + (status.MonitorAvailable ? "available" : "unavailable") + " / " + status.TriggerMode);
            Console.WriteLine("DNS mode        : " + status.DnsMode + " | key removal: " + status.KeyRemovalAction + " | grace: " + status.GraceSeconds + "s");
            if (!string.IsNullOrEmpty(status.LockdownUntilUtc))
            {
                Console.WriteLine("Lockdown        : " + status.LockdownReason);
                Console.WriteLine("Remaining       : " + FormatDuration(status.LockdownRemainingSeconds));
            }
            if (status.KeyGraceRemainingSeconds > 0)
            {
                Console.WriteLine("Key grace       : " + status.KeyGraceRemainingSeconds + "s remaining");
            }
            if (!string.IsNullOrEmpty(status.LastEvent))
            {
                Console.WriteLine("Last event      : " + status.LastEvent);
            }
            return 0;
        }

        private static int Enroll(string[] args)
        {
            bool programSlot = !HasFlag(args, "--no-program");
            bool requireTouch = !HasFlag(args, "--no-touch");
            bool force = HasFlag(args, "--force");
            bool usb = HasFlag(args, "--usb");
            string volume = FlagValue(args, "--volume");

            AegisConfig config = ConfigStore.Load();
            if (usb)
            {
                config.KeyProvider = "usb";
                if (!string.IsNullOrWhiteSpace(volume)) { config.UsbVolumeLabel = volume.Trim(); }
                if (string.IsNullOrEmpty(config.UsbVolumeLabel)) { config.UsbVolumeLabel = "AEGISWALL"; }
            }
            else if (HasFlag(args, "--yubikey"))
            {
                config.KeyProvider = "yubikey";
            }

            if (config.IsKeyEnrolled && !force)
            {
                Console.WriteLine("A key is already enrolled. Re-enrolling replaces the vault and recovery code.");
                Console.Write("Continue? [y/N] ");
                string answer = Console.ReadLine();
                if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Cancelled.");
                    return 1;
                }
            }

            IHardwareKey key = HardwareKeyFactory.Create(config);
            bool isUsb = string.Equals(config.KeyProvider, "usb", StringComparison.OrdinalIgnoreCase);
            if (programSlot)
            {
                if (isUsb)
                {
                    Console.WriteLine("This will write a DPAPI machine-bound key file to the USB drive labeled '" + config.UsbVolumeLabel + "'.");
                    Console.WriteLine("The key file will not work on another PC. Keep the recovery code offline.");
                }
                else
                {
                    Console.WriteLine("This will program YubiKey slot 2 with a new HMAC-SHA1 secret.");
                    Console.WriteLine("If slot 2 is used for something else, choose --no-program.");
                }
                Console.Write("Continue? [y/N] ");
                string answer = Console.ReadLine();
                if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Cancelled.");
                    return 1;
                }
            }

            if (isUsb)
            {
                Console.WriteLine("Reading USB key from drive '" + config.UsbVolumeLabel + "'...");
            }
            else
            {
                Console.WriteLine("Touch the YubiKey when it blinks.");
            }
            EnrollResult result = KeyVault.Enroll(key, config, programSlot, requireTouch);
            config.Armed = false;
            config.LockdownUntilUtc = string.Empty;
            config.LockdownReason = string.Empty;
            ConfigStore.Save(config, result.VaultKey);
            AuditLog.Append("key.enroll", "Serial " + config.KeySerial, "admin");

            Console.WriteLine();
            Console.WriteLine("Key enrolled successfully.");
            Console.WriteLine("Provider: " + config.KeyProvider + " | Serial: " + config.KeySerial);
            Console.WriteLine("RECOVERY CODE (save this offline, it will not be shown again):");
            Console.WriteLine("  " + result.RecoveryCode);
            Console.WriteLine();
            Console.WriteLine("Next: aegiswall allow \"C:\\Path\\To\\App.exe\"");
            Console.WriteLine("Then: aegiswall arm");
            WriteLastCommand(true, "Key enrolled", result.RecoveryCode);
            TryReloadService();
            return 0;
        }

        private static int Disarm(string[] args)
        {
            string recovery = FlagValue(args, "--recovery");
            IpcRequest request = new IpcRequest();
            request.Cmd = "disarm";
            request.RecoveryCode = recovery ?? string.Empty;
            return Send(request);
        }

        private static int Lockdown(string[] args)
        {
            int minutes = 0;
            if (args.Length > 1)
            {
                int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
            }
            IpcRequest request = new IpcRequest();
            request.Cmd = "lockdown";
            request.Minutes = minutes;
            return Send(request);
        }

        private static int Allow(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: aegiswall allow <path> [--inbound] [--inbound-only] [--both] [--outbound-only]");
                return 2;
            }

            bool outbound = true;
            bool inbound = false;
            if (HasFlag(args, "--inbound-only"))
            {
                outbound = false;
                inbound = true;
            }
            else if (HasFlag(args, "--both"))
            {
                inbound = true;
            }
            else if (HasFlag(args, "--no-outbound"))
            {
                outbound = false;
            }
            else if (HasFlag(args, "--inbound"))
            {
                inbound = true;
            }

            IpcRequest request = new IpcRequest();
            request.Cmd = "allow";
            request.Path = args[1];
            request.Outbound = outbound;
            request.Inbound = inbound;
            request.Enabled = true;
            return Send(request);
        }

        private static int Remove(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: aegiswall remove <path>");
                return 2;
            }
            IpcRequest request = new IpcRequest();
            request.Cmd = "remove";
            request.Path = args[1];
            return Send(request);
        }

        private static int Set(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: aegiswall set \"{\\\"lockdownMinutes\\\":15}\"");
                return 2;
            }
            IpcRequest request = new IpcRequest();
            request.Cmd = "set";
            request.Payload = args[1];
            return Send(request);
        }

        private static int Export(string[] args)
        {
            if (!EnsureServiceRunning()) { return 1; }
            IpcResponse response = IpcClient.Send(new IpcRequest { Cmd = "export" }, 30000);
            if (response == null || !response.Ok)
            {
                Console.Error.WriteLine("Export failed: " + (response == null ? "no response" : response.Message));
                return 1;
            }

            string path = args.Length > 1 ? args[1] : null;
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, response.Data, new UTF8Encoding(false));
                Console.WriteLine("Exported allowlist to " + Path.GetFullPath(path));
            }
            else
            {
                Console.WriteLine(response.Data);
            }
            WriteLastCommand(true, "Allowlist exported", response.Data);
            return 0;
        }

        private static int Import(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: aegiswall import <file.json> [--replace]");
                return 2;
            }
            if (!File.Exists(args[1]))
            {
                Console.Error.WriteLine("File not found: " + args[1]);
                return 2;
            }

            IpcRequest request = new IpcRequest();
            request.Cmd = "import";
            request.Payload = File.ReadAllText(args[1]);
            request.Replace = HasFlag(args, "--replace");
            return Send(request);
        }

        private static int UpdateApp(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: aegiswall update-app <path> [--outbound true|false] [--inbound true|false] [--enabled true|false] [--notes text]");
                return 2;
            }
            if (!EnsureServiceRunning()) { return 1; }

            IpcResponse list = IpcClient.Send(new IpcRequest { Cmd = "list" }, 30000);
            if (list == null || !list.Ok)
            {
                Console.Error.WriteLine("Could not read allowlist: " + (list == null ? "no response" : list.Message));
                return 1;
            }

            List<AllowedApp> apps = Json.Deserialize<List<AllowedApp>>(list.Data);
            AllowedApp found = null;
            foreach (AllowedApp app in apps)
            {
                if (PathUtil.Key(app.Path) == PathUtil.Key(args[1]))
                {
                    found = app;
                    break;
                }
            }
            if (found == null)
            {
                Console.Error.WriteLine("App is not in the allowlist.");
                return 1;
            }

            IpcRequest request = new IpcRequest();
            request.Cmd = "update-app";
            request.Path = found.Path;
            request.Outbound = FlagBool(args, "--outbound", found.Outbound);
            request.Inbound = FlagBool(args, "--inbound", found.Inbound);
            request.Enabled = FlagBool(args, "--enabled", found.Enabled);
            request.Notes = FlagValue(args, "--notes") ?? found.Notes;
            return Send(request);
        }

        private static int AuditCommand(string[] args)
        {
            int limit = 100;
            if (args.Length > 1)
            {
                int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out limit);
            }
            IpcRequest request = new IpcRequest();
            request.Cmd = "audit";
            request.Limit = limit;
            return Send(request);
        }

        private static bool FlagBool(string[] args, string flag, bool fallback)
        {
            string value = FlagValue(args, flag);
            if (string.IsNullOrEmpty(value)) { return fallback; }
            bool parsed;
            if (bool.TryParse(value, out parsed)) { return parsed; }
            if (value == "1") { return true; }
            if (value == "0") { return false; }
            return fallback;
        }

        private static int Installed(string[] args)
        {
            List<InstalledApp> apps = InstalledAppScanner.Scan();
            if (args.Length > 1 && string.Equals(args[1], "--json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(Json.Serialize(apps));
                return 0;
            }
            foreach (InstalledApp app in apps)
            {
                Console.WriteLine(app.Name + " | " + app.Path + " | " + app.Source);
            }
            Console.WriteLine(apps.Count + " apps");
            return 0;
        }
        private static int TestModeCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: aegiswall test-mode on|off");
                return 2;
            }
            string value = args[1].Trim().ToLowerInvariant();
            if (value != "on" && value != "off")
            {
                Console.Error.WriteLine("Usage: aegiswall test-mode on|off");
                return 2;
            }
            IpcRequest request = new IpcRequest();
            request.Cmd = "set";
            request.Payload = "{\"testMode\":" + (value == "on" ? "true" : "false") + "}";
            return Send(request);
        }

        private static int TestBeacon()
        {
            if (!EnsureServiceRunning()) { return 1; }
            IpcResponse before = IpcClient.Send(new IpcRequest { Cmd = "status" }, 10000);
            if (before == null || !before.Ok || before.Status == null)
            {
                Console.Error.WriteLine("Could not read service status.");
                return 1;
            }
            if (!before.Status.Armed)
            {
                Console.Error.WriteLine("Arm AegisWall before running the beacon test.");
                return 1;
            }

            string source = Path.Combine(Environment.SystemDirectory, "curl.exe");
            if (!File.Exists(source))
            {
                Console.Error.WriteLine("curl.exe was not found in System32.");
                return 1;
            }
            string directory = Path.Combine(Path.GetTempPath(), "AegisWallTest");
            Directory.CreateDirectory(directory);
            string beacon = Path.Combine(directory, "beacon.exe");
            File.Copy(source, beacon, true);

            int beforeCount = before.Status.BlockedAttempts;
            ProcessResult result = ProcessRunner.Run(beacon, "https://example.com --max-time 6 -s -o NUL -w \"%{http_code}\"", 15000);
            Thread.Sleep(1500);
            IpcResponse after = IpcClient.Send(new IpcRequest { Cmd = "status" }, 10000);
            int afterCount = after != null && after.Status != null ? after.Status.BlockedAttempts : beforeCount;
            bool blocked = afterCount > beforeCount || result.ExitCode != 0;
            bool lockdown = after != null && after.Status != null && string.Equals(after.Status.State, "Lockdown", StringComparison.OrdinalIgnoreCase);
            string data = Json.Serialize(new
            {
                beacon = beacon,
                exitCode = result.ExitCode,
                blocked = blocked,
                blockedAttemptsBefore = beforeCount,
                blockedAttemptsAfter = afterCount,
                lockdown = lockdown,
                stdout = result.StdOut,
                stderr = result.StdErr
            });
            string message = blocked ? "Beacon blocked by AegisWall" : "Beacon connected; check the allowlist and test setup";
            WriteLastCommand(blocked, message, data);
            AuditLog.Append("test.beacon", message + " | " + beacon, "admin");
            Console.WriteLine(message);
            Console.WriteLine(data);
            return blocked ? 0 : 1;
        }

        private static int TestReport(string[] args)
        {
            if (!EnsureServiceRunning()) { return 1; }
            IpcResponse status = IpcClient.Send(new IpcRequest { Cmd = "status" }, 10000);
            IpcResponse diagnostics = IpcClient.Send(new IpcRequest { Cmd = "diagnostics" }, 15000);
            List<AuditEntry> audit = AuditLog.Read(200);
            object diagnosticData = null;
            if (diagnostics != null && diagnostics.Ok && !string.IsNullOrEmpty(diagnostics.Data))
            {
                diagnosticData = Json.DeserializeObject(diagnostics.Data);
            }

            string report = Json.Serialize(new
            {
                generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                status = status == null ? null : status.Status,
                diagnostics = diagnosticData,
                audit = audit
            });
            string path = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "AegisWall-test-report.json");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
            File.WriteAllText(path, report, new UTF8Encoding(false));
            WriteLastCommand(true, "Test report written", Path.GetFullPath(path));
            Console.WriteLine("Test report: " + Path.GetFullPath(path));
            return 0;
        }
        private static int SendCommand(string command, string payload)
        {
            IpcRequest request = new IpcRequest();
            request.Cmd = command;
            request.Payload = payload ?? string.Empty;
            return Send(request);
        }

        private static int Send(IpcRequest request)
        {
            if (!EnsureServiceRunning())
            {
                return 1;
            }

            IpcResponse response = IpcClient.Send(request, 60000);
            if (response == null)
            {
                WriteLastCommand(false, "No response from service.", string.Empty);
                Console.Error.WriteLine("No response from service.");
                return 1;
            }

            WriteLastCommand(response.Ok, response.Message, response.Data);
            if (response.Status != null)
            {
                PrintStatus(response.Status);
            }
            else if (!response.Ok)
            {
                Console.Error.WriteLine("Error: " + response.Message);
            }
            else
            {
                if (!string.IsNullOrEmpty(response.Data))
                {
                    Console.WriteLine(response.Data);
                }
                if (!string.IsNullOrEmpty(response.Message) && response.Message != "ok")
                {
                    Console.WriteLine(response.Message);
                }
            }
            return response.Ok ? 0 : 1;
        }

        private static void PrintStatus(StatusSnapshot status)
        {
            Console.WriteLine("State: " + status.State + " | armed: " + (status.Armed ? "yes" : "no")
                + " | key: " + (status.KeyPresent ? "present" : "not present")
                + " | apps: " + status.AllowedApps);
            if (status.LockdownRemainingSeconds > 0)
            {
                Console.WriteLine("Lockdown: " + status.LockdownReason + " (" + FormatDuration(status.LockdownRemainingSeconds) + ")");
            }
        }

        private static int Reset(string[] args)
        {
            if (!HasFlag(args, "--force"))
            {
                Console.Error.WriteLine("Reset removes every AegisWall filter. Re-run with --force.");
                return 2;
            }

            StopService();
            try
            {
                using (WfpEngine wfp = new WfpEngine())
                {
                    wfp.Open();
                    wfp.RemoveAll();
                }
                Console.WriteLine("WFP filters removed.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Could not remove WFP filters: " + ex.Message);
            }

            AegisConfig config = ConfigStore.Load();
            config.Armed = false;
            config.LockdownUntilUtc = string.Empty;
            config.LockdownReason = string.Empty;
            ConfigStore.Save(config, null);
            AuditLog.Append("reset", "Filters removed and protection disarmed", "admin");
            WriteLastCommand(true, "Reset complete", string.Empty);
            Console.WriteLine("Protection disarmed. Service left installed; use uninstall.ps1 to remove it.");
            return 0;
        }

        private static int Watchdog()
        {
            if (!EnsureServiceRunning())
            {
                return 1;
            }

            IpcResponse status = IpcClient.Send(new IpcRequest { Cmd = "status" }, 10000);
            if (status != null && status.Ok && status.Status != null && status.Status.Armed && !status.Status.FiltersApplied)
            {
                IpcClient.Send(new IpcRequest { Cmd = "reload" }, 30000);
                AuditLog.Append("watchdog.reapply", "Filters were missing; reload requested", "watchdog");
            }
            WriteLastCommand(true, "Watchdog check complete", string.Empty);
            return 0;
        }

        private static bool EnsureServiceRunning()
        {
            if (IpcClient.IsServiceReachable())
            {
                return true;
            }

            try
            {
                using (ServiceController service = new ServiceController("AegisWall"))
                {
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
                }
                return IpcClient.IsServiceReachable();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("AegisWall service is not installed or could not start: " + ex.Message);
                Console.Error.WriteLine("Run install.ps1 as administrator first.");
                return false;
            }
        }

        private static void StopService()
        {
            try
            {
                using (ServiceController service = new ServiceController("AegisWall"))
                {
                    if (service.Status != ServiceControllerStatus.Stopped)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch
            {
            }
        }

        private static void TryReloadService()
        {
            if (!IpcClient.IsServiceReachable())
            {
                return;
            }

            IpcResponse response = IpcClient.Send(new IpcRequest { Cmd = "reload" }, 30000);
            if (response != null && response.Ok && response.Status != null)
            {
                PrintStatus(response.Status);
            }
        }

        private static void WriteLastCommand(bool ok, string message, string data)
        {
            try
            {
                LastCommandResult result = new LastCommandResult();
                result.Ok = ok;
                result.Message = message ?? string.Empty;
                result.Data = data ?? string.Empty;
                result.Utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                ConfigStore.WriteAtomic(ConfigStore.LastCommandPath, Json.Serialize(result), true);
            }
            catch
            {
            }
        }

        private static int RelaunchElevated(string[] args)
        {
            try
            {
                StringBuilder arguments = new StringBuilder();
                foreach (string arg in args)
                {
                    if (arguments.Length > 0) { arguments.Append(' '); }
                    if (arg.IndexOf(' ') >= 0 || arg.IndexOf('"') >= 0)
                    {
                        arguments.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
                    }
                    else
                    {
                        arguments.Append(arg);
                    }
                }

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Assembly.GetExecutingAssembly().Location;
                info.Arguments = arguments.ToString();
                info.Verb = "runas";
                info.UseShellExecute = true;
                Process.Start(info);
                return 0;
            }
            catch
            {
                Console.Error.WriteLine("Administrator rights are required. Run this command from an elevated prompt.");
                return 1;
            }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string FlagValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static string FormatDuration(int seconds)
        {
            if (seconds < 0) { seconds = 0; }
            int minutes = seconds / 60;
            int rest = seconds % 60;
            return minutes.ToString(CultureInfo.InvariantCulture) + "m " + rest.ToString("00", CultureInfo.InvariantCulture) + "s";
        }

        private static void Usage()
        {
            Console.WriteLine("AegisWall " + ProtectionEngine.Version);
            Console.WriteLine("Usage:");
            Console.WriteLine("  aegiswall status [--json]");
            Console.WriteLine("  aegiswall enroll [--usb [--volume LABEL] | --yubikey] [--no-program] [--no-touch] [--force]");
            Console.WriteLine("  aegiswall unlock");
            Console.WriteLine("  aegiswall arm");
            Console.WriteLine("  aegiswall disarm [--recovery CODE]");
            Console.WriteLine("  aegiswall lockdown [minutes]");
            Console.WriteLine("  aegiswall allow <path> [--inbound]");
            Console.WriteLine("  aegiswall remove <path>");
            Console.WriteLine("  aegiswall list");
            Console.WriteLine("  aegiswall set <json>");
            Console.WriteLine("  aegiswall verify");
            Console.WriteLine("  aegiswall key-status");
            Console.WriteLine("  aegiswall reload");
            Console.WriteLine("  aegiswall installed [--json]");
            Console.WriteLine("  aegiswall test-mode on|off");
            Console.WriteLine("  aegiswall test-beacon");
            Console.WriteLine("  aegiswall test-report [file]");
            Console.WriteLine("  aegiswall reset --force");
            Console.WriteLine("  aegiswall watchdog");
        }
    }
}





