using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AegisWall.Core
{
    public sealed class InstalledApp
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public string Version { get; set; }

        public InstalledApp()
        {
            Name = string.Empty;
            Path = string.Empty;
            Source = string.Empty;
            Version = string.Empty;
        }
    }

    public static class InstalledAppScanner
    {
        private static readonly string[] SkipTokens = new string[]
        {
            "unins", "uninstall", "setup", "update", "updater", "repair", "crash", "report", "helper"
        };

        public static List<InstalledApp> Scan()
        {
            Dictionary<string, InstalledApp> seen = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
            ScanStartMenu(seen);
            ScanUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Registry", seen);
            ScanUninstall(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Registry (32-bit)", seen);
            ScanUninstall(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "User registry", seen);
            ScanAppPaths(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", "App Paths", seen);
            ScanAppPaths(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", "App Paths (32-bit)", seen);

            List<InstalledApp> result = new List<InstalledApp>(seen.Values);
            result.Sort(delegate(InstalledApp a, InstalledApp b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static void ScanStartMenu(Dictionary<string, InstalledApp> seen)
        {
            string[] roots = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
            };

            Type shellType = null;
            object shell = null;
            try
            {
                shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    shell = Activator.CreateInstance(shellType);
                }
            }
            catch
            {
                shellType = null;
                shell = null;
            }

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (string file in files)
                {
                    string target = string.Empty;
                    try
                    {
                        if (shellType != null && shell != null)
                        {
                            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { file });
                            if (shortcut != null)
                            {
                                target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                                Marshal.ReleaseComObject(shortcut);
                            }
                        }
                    }
                    catch
                    {
                        target = string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(target))
                    {
                        continue;
                    }

                    Add(seen, Path.GetFileNameWithoutExtension(file), target, "Start Menu");
                }
            }

            if (shell != null)
            {
                try { Marshal.ReleaseComObject(shell); } catch { }
            }
        }

        private static void ScanUninstall(RegistryHive hive, RegistryView view, string subKey, string source, Dictionary<string, InstalledApp> seen)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey root = baseKey.OpenSubKey(subKey))
                {
                    if (root == null)
                    {
                        return;
                    }

                    string[] names = root.GetSubKeyNames();
                    foreach (string name in names)
                    {
                        try
                        {
                            using (RegistryKey key = root.OpenSubKey(name))
                            {
                                if (key == null) { continue; }
                                object systemComponent = key.GetValue("SystemComponent");
                                if (systemComponent != null && Convert.ToInt32(systemComponent, System.Globalization.CultureInfo.InvariantCulture) == 1)
                                {
                                    continue;
                                }

                                string displayName = Convert.ToString(key.GetValue("DisplayName"));
                                if (string.IsNullOrWhiteSpace(displayName))
                                {
                                    continue;
                                }

                                string path = ResolveDisplayIcon(Convert.ToString(key.GetValue("DisplayIcon")));
                                if (string.IsNullOrEmpty(path))
                                {
                                    path = FindExecutable(Convert.ToString(key.GetValue("InstallLocation")));
                                }
                                if (string.IsNullOrEmpty(path))
                                {
                                    continue;
                                }

                                Add(seen, displayName, path, source);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void ScanAppPaths(RegistryHive hive, RegistryView view, string subKey, string source, Dictionary<string, InstalledApp> seen)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey root = baseKey.OpenSubKey(subKey))
                {
                    if (root == null)
                    {
                        return;
                    }

                    foreach (string name in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey key = root.OpenSubKey(name))
                            {
                                if (key == null) { continue; }
                                string path = Convert.ToString(key.GetValue(string.Empty));
                                if (string.IsNullOrWhiteSpace(path))
                                {
                                    string directory = Convert.ToString(key.GetValue("Path"));
                                    path = FindExecutable(directory);
                                }
                                if (string.IsNullOrWhiteSpace(path))
                                {
                                    continue;
                                }
                                Add(seen, Path.GetFileNameWithoutExtension(name), path, source);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string ResolveDisplayIcon(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string icon = value.Trim().Trim('"');
            int comma = icon.LastIndexOf(',');
            if (comma > 0)
            {
                icon = icon.Substring(0, comma).Trim().Trim('"');
            }
            if (icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(icon))
            {
                return icon;
            }
            return string.Empty;
        }

        private static string FindExecutable(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return string.Empty;
            }

            try
            {
                string full = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
                if (!Directory.Exists(full))
                {
                    return string.Empty;
                }

                string[] files = Directory.GetFiles(full, "*.exe", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file).ToLowerInvariant();
                    bool skip = false;
                    foreach (string token in SkipTokens)
                    {
                        if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (!skip)
                    {
                        return file;
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private static void Add(Dictionary<string, InstalledApp> seen, string name, string path, string source)
        {
            string full = PathUtil.Normalize(path);
            if (string.IsNullOrEmpty(full) || !File.Exists(full))
            {
                return;
            }
            if (!full.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string fileName = Path.GetFileName(full).ToLowerInvariant();
            foreach (string token in SkipTokens)
            {
                if (fileName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
            }

            if (PathUtil.IsWindowsComponent(full))
            {
                return;
            }

            string key = PathUtil.Key(full);
            if (seen.ContainsKey(key))
            {
                return;
            }

            InstalledApp app = new InstalledApp();
            app.Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(full) : name.Trim();
            app.Path = full;
            app.Source = source;
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(full);
                app.Version = info.FileVersion ?? string.Empty;
            }
            catch
            {
                app.Version = string.Empty;
            }
            seen[key] = app;
        }
    }
}
