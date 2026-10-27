using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AegisWall.Core;

namespace AegisWall.Tests
{
    public static class Program
    {
        private static int failures;
        private static int checks;
        private static string tempDir;

        public static int Main(string[] args)
        {
            tempDir = Path.Combine(Path.GetTempPath(), "AegisWall.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            Environment.SetEnvironmentVariable("AEGISWALL_DATA_DIR", tempDir);
            Environment.SetEnvironmentVariable("AEGISWALL_SKIP_ACL", "1");
            Environment.SetEnvironmentVariable("AEGISWALL_ALLOW_DEV_KEY", "1");
            Environment.SetEnvironmentVariable("AEGISWALL_DEV_KEY_PATH", Path.Combine(tempDir, "devkey.bin"));

            try
            {
                TestBase32();
                TestCrypto();
                TestConfigSignature();
                TestConfigStore();
                TestHardwareVault();
                TestUsbKey();
                TestPathUtil();
                TestInstalledScanner();
                TestStatus();
                TestWfpLayout();
                TestAuditChain();
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine("Unhandled test exception: " + ex);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine(checks + " checks, " + failures + " failures");
            return failures == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string name)
        {
            checks++;
            if (!condition)
            {
                failures++;
                Console.Error.WriteLine("FAIL: " + name);
            }
        }

        private static void TestBase32()
        {
            byte[] data = Crypto.RandomBytes(32);
            string code = Base32.Encode(data);
            byte[] decoded = Base32.Decode(code);
            Check(decoded.Length == data.Length, "base32 length");
            bool same = true;
            for (int i = 0; i < data.Length; i++) { if (data[i] != decoded[i]) { same = false; break; } }
            Check(same, "base32 roundtrip");
            Check(Base32.Decode(code.ToLowerInvariant().Replace("-", " ")).Length == 32, "base32 normalization");
        }

        private static void TestCrypto()
        {
            byte[] key = Crypto.RandomBytes(32);
            byte[] plaintext = Encoding.UTF8.GetBytes("AegisWall test payload");
            string blob = Crypto.Wrap(plaintext, key);
            byte[] unwrapped = Crypto.Unwrap(blob, key);
            Check(unwrapped != null, "wrap/unwrap");
            Check(Encoding.UTF8.GetString(unwrapped) == "AegisWall test payload", "wrap payload");
            string tampered = blob.Substring(0, blob.Length - 4) + "AAAA";
            Check(Crypto.Unwrap(tampered, key) == null, "tamper detection");
            Check(Crypto.Unwrap(blob, Crypto.RandomBytes(32)) == null, "wrong key rejected");
            Check(Crypto.FixedTimeEquals("abc", "abc"), "fixed time equal");
            Check(!Crypto.FixedTimeEquals("abc", "abd"), "fixed time not equal");
        }

        private static void TestConfigSignature()
        {
            byte[] key = Crypto.RandomBytes(32);
            AegisConfig config = AegisConfig.Default();
            config.Armed = true;
            config.AllowedApps.Add(new AllowedApp { Name = "Test", Path = @"C:\Test\App.exe", Inbound = false });
            ConfigSigner.Sign(config, key);
            Check(ConfigSigner.Verify(config, key), "config signature valid");
            config.LockdownMinutes = 99;
            Check(!ConfigSigner.Verify(config, key), "config tamper detected");
            Check(!ConfigSigner.Verify(config, Crypto.RandomBytes(32)), "config wrong key rejected");
        }

        private static void TestConfigStore()
        {
            byte[] key = Crypto.RandomBytes(32);
            AegisConfig config = AegisConfig.Default();
            config.Armed = true;
            config.LockdownMinutes = 15;
            ConfigStore.Save(config, key);
            AegisConfig loaded = ConfigStore.Load();
            Check(loaded.Armed, "config store armed");
            Check(loaded.LockdownMinutes == 15, "config store minutes");
            Check(ConfigStore.Validate(loaded, key), "config store signature");
        }

        private static void TestHardwareVault()
        {
            AegisConfig config = AegisConfig.Default();
            DevFileKey key = new DevFileKey();
            EnrollResult result = KeyVault.Enroll(key, config, true, false);
            Check(result.VaultKey != null && result.VaultKey.Length == 32, "vault key length");
            Check(!string.IsNullOrEmpty(result.RecoveryCode), "recovery code generated");
            Check(config.IsKeyEnrolled, "config key enrolled");
            byte[] unlocked = KeyVault.Unlock(config, key);
            Check(unlocked != null, "vault unlock");
            bool same = unlocked != null && unlocked.Length == result.VaultKey.Length;
            if (same)
            {
                for (int i = 0; i < unlocked.Length; i++) { if (unlocked[i] != result.VaultKey[i]) { same = false; break; } }
            }
            Check(same, "vault key matches");
            byte[] recovered = KeyVault.Recover(config, result.RecoveryCode);
            Check(recovered != null, "recovery unlock");
            Check(KeyVault.Recover(config, "AAAA-BBBB-CCCC") == null, "wrong recovery rejected");
        }

        private static void TestUsbKey()
        {
            string usbDir = Path.Combine(tempDir, "usb");
            Directory.CreateDirectory(usbDir);
            Environment.SetEnvironmentVariable("AEGISWALL_USB_KEY_PATH", usbDir);

            AegisConfig config = AegisConfig.Default();
            config.KeyProvider = "usb";
            config.UsbVolumeLabel = "TEST";
            UsbKeyProvider usb = new UsbKeyProvider("TEST", usbDir);
            Check(!usb.IsPresent(), "usb key missing before provision");
            EnrollResult result = KeyVault.Enroll(usb, config, true, false);
            Check(result.VaultKey != null, "usb vault key");
            Check(usb.IsPresent(), "usb key present after provision");
            Check(config.KeyProvider == "usb", "usb provider");
            Check(config.KeySerial.StartsWith("USB-"), "usb serial");
            byte[] unlocked = KeyVault.Unlock(config, usb);
            Check(unlocked != null, "usb unlock");
            byte[] recovered = KeyVault.Recover(config, result.RecoveryCode);
            Check(recovered != null, "usb recovery");
        }

        private static void TestInstalledScanner()
        {
            List<InstalledApp> apps = InstalledAppScanner.Scan();
            Check(apps != null, "installed scanner returns list");
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool allExist = true;
            foreach (InstalledApp app in apps)
            {
                if (!File.Exists(app.Path)) { allExist = false; break; }
                if (!paths.Add(PathUtil.Key(app.Path))) { allExist = false; break; }
            }
            Check(allExist, "installed scanner paths exist and are unique");
        }
        private static void TestPathUtil()
        {
            Check(PathUtil.Key(@"C:\Test\App.exe") == PathUtil.Key(@"c:\test\app.exe"), "path key case");
            Check(PathUtil.IsWindowsComponent(Path.Combine(PathUtil.WindowsDirectory, "System32", "cmd.exe")), "windows component");
            Check(!PathUtil.IsWindowsComponent(@"C:\Program Files\Example\app.exe"), "user program");
        }

        private static void TestStatus()
        {
            StatusSnapshot status = new StatusSnapshot();
            status.State = "Protected";
            status.Armed = true;
            status.AllowedApps = 3;
            StatusStore.Write(status);
            StatusSnapshot read = StatusStore.Read();
            Check(read.State == "Protected", "status state");
            Check(read.Armed, "status armed");
            Check(read.AllowedApps == 3, "status apps");
        }

        private static void TestWfpLayout()
        {
            Check(Marshal.SizeOf(typeof(WfpNative.FWP_VALUE0)) == 16, "FWP_VALUE0 size");
            Check(Marshal.SizeOf(typeof(WfpNative.FWPM_ACTION0)) == 20, "FWPM_ACTION0 size");
            Check(Marshal.SizeOf(typeof(WfpNative.FWPM_FILTER_CONDITION0)) == 40, "FWPM_FILTER_CONDITION0 size");
            Check(Marshal.SizeOf(typeof(WfpNative.FWPM_SUBLAYER0)) == 72, "FWPM_SUBLAYER0 size");
            Check(Marshal.SizeOf(typeof(WfpNative.FWPM_FILTER0)) == 200, "FWPM_FILTER0 size");
        }

        private static void TestAuditChain()
        {
            AuditLog.Append("test.one", "first", "tests");
            AuditLog.Append("test.two", "second", "tests");
            AuditLog.Append("test.three", "third", "tests");
            Check(AuditLog.VerifyChain() == 0, "audit chain valid");
            List<AuditEntry> entries = AuditLog.Read(10);
            Check(entries.Count >= 3, "audit entries");
            Check(entries[entries.Count - 1].Event == "test.three", "audit last event");
        }
    }
}

