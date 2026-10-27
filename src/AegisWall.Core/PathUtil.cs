using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;

namespace AegisWall.Core
{
    public static class PathUtil
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string value = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            try
            {
                return Path.GetFullPath(value);
            }
            catch
            {
                return value;
            }
        }

        public static string Key(string path)
        {
            return Normalize(path).ToLowerInvariant();
        }

        public static string Sha256File(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    return Crypto.ToHex(sha.ComputeHash(stream));
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static string WindowsDirectory
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
        }

        public static bool IsWindowsComponent(string path)
        {
            string full = Normalize(path);
            if (string.IsNullOrEmpty(full))
            {
                return false;
            }

            string windows = WindowsDirectory.TrimEnd('\\') + "\\";
            if (full.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\') + "\\";
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\') + "\\";
            string windowsApps = Path.Combine(programFiles, "WindowsApps") + "\\";
            if (full.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\') + "\\";
            if (full.StartsWith(Path.Combine(programData, "Microsoft") + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static string ProcessPath(int processId)
        {
            try
            {
                return System.Diagnostics.Process.GetProcessById(processId).MainModule.FileName;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsUnder(string path, string directory)
        {
            string full = Normalize(path);
            string root = Normalize(directory).TrimEnd('\\') + "\\";
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}
