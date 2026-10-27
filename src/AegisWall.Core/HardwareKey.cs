using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AegisWall.Core
{
    public interface IHardwareKey
    {
        string ProviderName { get; }
        string Serial { get; }
        bool IsPresent();
        byte[] HmacChallenge(byte[] challenge);
        void Provision(bool requireTouch);
    }

    public sealed class HardwareKeyException : Exception
    {
        public HardwareKeyException(string message) : base(message) { }
        public HardwareKeyException(string message, Exception inner) : base(message, inner) { }
    }

    public static class HardwareKeyFactory
    {
        public static IHardwareKey Create(AegisConfig config)
        {
            string provider = config == null ? "yubikey" : (config.KeyProvider ?? "yubikey");
            if (string.Equals(provider, "dev", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Environment.GetEnvironmentVariable("AEGISWALL_ALLOW_DEV_KEY"), "1", StringComparison.Ordinal))
            {
                return new DevFileKey();
            }

            if (string.Equals(provider, "usb", StringComparison.OrdinalIgnoreCase))
            {
                string label = config == null ? "AEGISWALL" : config.UsbVolumeLabel;
                return new UsbKeyProvider(label, Environment.GetEnvironmentVariable("AEGISWALL_USB_KEY_PATH"));
            }

            string path = config == null ? string.Empty : config.YkmanPath;
            string serial = config == null ? string.Empty : config.KeySerial;
            return new YubiKeyCli(path, serial);
        }
    }

    public sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public bool TimedOut { get; set; }

        public ProcessResult()
        {
            StdOut = string.Empty;
            StdErr = string.Empty;
        }
    }

    public static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, string arguments, int timeoutMs)
        {
            ProcessResult result = new ProcessResult();
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = fileName;
            info.Arguments = arguments;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.StandardOutputEncoding = new UTF8Encoding(false);
            info.StandardErrorEncoding = new UTF8Encoding(false);
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            using (Process process = new Process())
            {
                process.StartInfo = info;
                process.Start();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs))
                {
                    result.TimedOut = true;
                    try { process.Kill(); } catch { }
                    try { process.WaitForExit(2000); } catch { }
                }

                try { result.StdOut = output.Result; } catch { result.StdOut = string.Empty; }
                try { result.StdErr = error.Result; } catch { result.StdErr = string.Empty; }
                try { result.ExitCode = process.HasExited ? process.ExitCode : -1; } catch { result.ExitCode = -1; }
            }

            return result;
        }
    }

    public sealed class YubiKeyCli : IHardwareKey
    {
        private static readonly Regex SerialPattern = new Regex(@"Serial(?: Number)?:\s*([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HexPattern = new Regex(@"\b([0-9a-fA-F]{40})\b", RegexOptions.Compiled);
        private readonly string ykmanPath;
        private readonly string serial;

        public YubiKeyCli(string ykmanPath, string serial)
        {
            this.ykmanPath = string.IsNullOrWhiteSpace(ykmanPath) ? ResolveYkmanPath() : ykmanPath;
            this.serial = serial ?? string.Empty;
        }

        public string ProviderName
        {
            get { return "yubikey"; }
        }

        public string Serial
        {
            get
            {
                if (!string.IsNullOrEmpty(serial))
                {
                    return serial;
                }

                try
                {
                    ProcessResult result = ProcessRunner.Run(ykmanPath, "list", 8000);
                    Match match = SerialPattern.Match(result.StdOut + "\n" + result.StdErr);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
                catch
                {
                }
                return string.Empty;
            }
        }

        public bool IsPresent()
        {
            try
            {
                ProcessResult result = ProcessRunner.Run(ykmanPath, "list", 8000);
                if (result.TimedOut || result.ExitCode != 0)
                {
                    return false;
                }

                string output = result.StdOut + "\n" + result.StdErr;
                if (!output.ToLowerInvariant().Contains("yubikey"))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(serial))
                {
                    return output.Contains(serial);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] HmacChallenge(byte[] challenge)
        {
            if (challenge == null || challenge.Length == 0 || challenge.Length > 64)
            {
                throw new HardwareKeyException("HMAC challenge must be 1-64 bytes.");
            }

            string device = string.IsNullOrEmpty(serial) ? string.Empty : "--device " + serial + " ";
            string args = device + "otp calculate 2 " + Crypto.ToHex(challenge);
            ProcessResult result = ProcessRunner.Run(ykmanPath, args, 45000);
            if (result.TimedOut)
            {
                throw new HardwareKeyException("YubiKey challenge timed out. Touch the key when prompted.");
            }
            if (result.ExitCode != 0)
            {
                throw new HardwareKeyException("YubiKey challenge failed: " + FirstLine(result.StdErr + " " + result.StdOut));
            }

            Match match = HexPattern.Match(result.StdOut);
            if (!match.Success)
            {
                throw new HardwareKeyException("YubiKey did not return a 20-byte HMAC-SHA1 response.");
            }

            return Crypto.FromHex(match.Groups[1].Value);
        }

        public void Provision(bool requireTouch)
        {
            string args = "otp chalresp --generate --force " + (requireTouch ? "--touch " : string.Empty) + "2";
            ProcessResult result = ProcessRunner.Run(ykmanPath, args, 60000);
            if (result.TimedOut)
            {
                throw new HardwareKeyException("YubiKey programming timed out.");
            }
            if (result.ExitCode != 0)
            {
                throw new HardwareKeyException("Could not program YubiKey slot 2: " + FirstLine(result.StdErr + " " + result.StdOut));
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "unknown error";
            }

            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }
            return text.Trim();
        }

        public static string ResolveYkmanPath()
        {
            List<string> candidates = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            candidates.Add(Path.Combine(programFiles, "Yubico", "YubiKey Manager", "ykman.exe"));
            candidates.Add(Path.Combine(programFiles, "Yubico", "YubiKey Manager CLI", "ykman.exe"));
            candidates.Add(Path.Combine(programFiles, "Yubico", "YubiKey Manager (ykman)", "ykman.exe"));
            candidates.Add(Path.Combine(programFilesX86, "Yubico", "YubiKey Manager", "ykman.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "ykman.exe"));

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string part in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(part.Trim(), "ykman.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            throw new HardwareKeyException(
                "ykman.exe was not found. Install Yubico YubiKey Manager CLI, then set HardwareKey.YkmanPath in config.");
        }
    }

    public sealed class UsbKeyProvider : IHardwareKey
    {
        private const string KeyFileName = "aegiswall.key";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AegisWall/USB/v1");
        private readonly string volumeLabel;
        private readonly string directPath;
        private byte[] cachedKey;
        private string cachedSerial;

        public UsbKeyProvider(string volumeLabel, string directPath)
        {
            this.volumeLabel = string.IsNullOrWhiteSpace(volumeLabel) ? "AEGISWALL" : volumeLabel.Trim();
            this.directPath = directPath ?? string.Empty;
        }

        public string ProviderName
        {
            get { return "usb"; }
        }

        public string Serial
        {
            get
            {
                byte[] key = ReadKey();
                if (key == null)
                {
                    return string.Empty;
                }
                if (string.IsNullOrEmpty(cachedSerial))
                {
                    cachedSerial = "USB-" + Crypto.Sha256Hex(key).Substring(0, 12).ToUpperInvariant();
                }
                return cachedSerial;
            }
        }

        public bool IsPresent()
        {
            return ReadKey() != null;
        }

        public byte[] HmacChallenge(byte[] challenge)
        {
            byte[] key = ReadKey();
            if (key == null)
            {
                throw new HardwareKeyException("USB key is missing or was created on another PC.");
            }
            return Crypto.HmacSha256(key, challenge);
        }

        public void Provision(bool requireTouch)
        {
            string path = ResolveKeyPath(true);
            try
            {
                byte[] key = Crypto.RandomBytes(32);
                byte[] protectedKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.LocalMachine);
                File.WriteAllBytes(path, protectedKey);
                cachedKey = key;
                cachedSerial = null;
                try { ConfigStore.HardenFile(path, false); } catch { }
            }
            catch (Exception ex)
            {
                throw new HardwareKeyException("Could not write the USB key file: " + ex.Message, ex);
            }
        }

        private byte[] ReadKey()
        {
            try
            {
                string path = ResolveKeyPath(false);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return null;
                }

                byte[] protectedKey = File.ReadAllBytes(path);
                byte[] key = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.LocalMachine);
                if (key == null || key.Length != 32)
                {
                    return null;
                }
                cachedKey = key;
                return key;
            }
            catch
            {
                return null;
            }
        }

        private string ResolveKeyPath(bool forWrite)
        {
            if (!string.IsNullOrEmpty(directPath))
            {
                string path = directPath;
                bool directory = path.EndsWith("\\", StringComparison.Ordinal)
                    || path.EndsWith("/", StringComparison.Ordinal)
                    || Directory.Exists(path)
                    || Path.GetExtension(path).Length == 0;
                if (directory)
                {
                    if (forWrite) { Directory.CreateDirectory(path); }
                    path = Path.Combine(path, KeyFileName);
                }
                return path;
            }

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }
                    if (drive.DriveType != DriveType.Removable && drive.DriveType != DriveType.Fixed)
                    {
                        continue;
                    }
                    if (!string.Equals(drive.VolumeLabel, volumeLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string path = Path.Combine(drive.RootDirectory.FullName, KeyFileName);
                    if (forWrite || File.Exists(path))
                    {
                        return path;
                    }
                }
                catch
                {
                }
            }

            if (forWrite)
            {
                throw new HardwareKeyException("USB key not found. Insert a drive labeled '" + volumeLabel + "'.");
            }
            return null;
        }
    }

    public sealed class DevFileKey : IHardwareKey
    {
        private readonly string path;

        public DevFileKey()
        {
            string overridePath = Environment.GetEnvironmentVariable("AEGISWALL_DEV_KEY_PATH");
            path = string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(ConfigStore.DataDir, "devkey.bin")
                : Path.GetFullPath(overridePath);
        }

        public string ProviderName
        {
            get { return "dev"; }
        }

        public string Serial
        {
            get { return "DEV-FILE"; }
        }

        public bool IsPresent()
        {
            return File.Exists(path);
        }

        public byte[] HmacChallenge(byte[] challenge)
        {
            if (!File.Exists(path))
            {
                throw new HardwareKeyException("Development key file is missing.");
            }
            return Crypto.HmacSha1(File.ReadAllBytes(path), challenge);
        }

        public void Provision(bool requireTouch)
        {
            ConfigStore.EnsureDataDir();
            File.WriteAllBytes(path, Crypto.RandomBytes(20));
            ConfigStore.HardenFile(path, false);
        }
    }
}

