using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AegisWall.Core
{
    public sealed class AuditEntry
    {
        public long Seq { get; set; }
        public string Utc { get; set; }
        public string Event { get; set; }
        public string Detail { get; set; }
        public string Actor { get; set; }
        public string Prev { get; set; }
        public string Hash { get; set; }

        public AuditEntry()
        {
            Utc = string.Empty;
            Event = string.Empty;
            Detail = string.Empty;
            Actor = string.Empty;
            Prev = string.Empty;
            Hash = string.Empty;
        }
    }

    public static class AuditLog
    {
        private static readonly object Gate = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static void Append(string eventName, string detail, string actor)
        {
            try
            {
                lock (Gate)
                {
                    ConfigStore.EnsureDataDir();
                    AuditEntry previous = LastEntry();
                    AuditEntry entry = new AuditEntry();
                    entry.Seq = previous == null ? 1 : previous.Seq + 1;
                    entry.Utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    entry.Event = eventName ?? string.Empty;
                    entry.Detail = detail ?? string.Empty;
                    entry.Actor = actor ?? string.Empty;
                    entry.Prev = previous == null ? new string('0', 64) : previous.Hash;
                    entry.Hash = ComputeHash(entry);
                    File.AppendAllText(ConfigStore.AuditPath, Json.Serialize(entry) + Environment.NewLine, Utf8);
                    ConfigStore.HardenFile(ConfigStore.AuditPath, true);
                }
            }
            catch
            {
                // Audit failures must never take down enforcement.
            }
        }

        private static string ComputeHash(AuditEntry entry)
        {
            string material = entry.Prev + "|" + entry.Seq.ToString(CultureInfo.InvariantCulture) + "|" + entry.Utc + "|"
                + entry.Event + "|" + entry.Detail + "|" + entry.Actor;
            return Crypto.Sha256Hex(material);
        }

        private static AuditEntry LastEntry()
        {
            try
            {
                if (!File.Exists(ConfigStore.AuditPath))
                {
                    return null;
                }

                string[] lines = File.ReadAllLines(ConfigStore.AuditPath, Utf8);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        continue;
                    }

                    try
                    {
                        return Json.Deserialize<AuditEntry>(lines[i]);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public static List<AuditEntry> Read(int maxEntries)
        {
            List<AuditEntry> result = new List<AuditEntry>();
            try
            {
                if (!File.Exists(ConfigStore.AuditPath))
                {
                    return result;
                }

                string[] lines = File.ReadAllLines(ConfigStore.AuditPath, Utf8);
                int start = Math.Max(0, lines.Length - maxEntries);
                for (int i = start; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        continue;
                    }

                    try
                    {
                        result.Add(Json.Deserialize<AuditEntry>(lines[i]));
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        public static long VerifyChain()
        {
            try
            {
                if (!File.Exists(ConfigStore.AuditPath))
                {
                    return 0;
                }

                string[] lines = File.ReadAllLines(ConfigStore.AuditPath, Utf8);
                string previous = new string('0', 64);
                long expectedSeq = 1;
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    AuditEntry entry = Json.Deserialize<AuditEntry>(line);
                    if (entry.Seq != expectedSeq || entry.Prev != previous || entry.Hash != ComputeHash(entry))
                    {
                        return entry.Seq;
                    }

                    previous = entry.Hash;
                    expectedSeq++;
                }
            }
            catch
            {
                return -1;
            }
            return 0;
        }
    }
}
