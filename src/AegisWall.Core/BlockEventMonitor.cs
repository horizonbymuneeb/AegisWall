using System;
using System.Diagnostics;
using System.Globalization;

namespace AegisWall.Core
{
    public sealed class BlockEventMonitor : IDisposable
    {
        private readonly ProtectionEngine engine;
        private EventLog securityLog;
        private bool available;

        public BlockEventMonitor(ProtectionEngine engine)
        {
            this.engine = engine;
        }

        public bool Available
        {
            get { return available; }
        }

        public void Start()
        {
            try
            {
                securityLog = new EventLog("Security");
                securityLog.EnableRaisingEvents = true;
                securityLog.EntryWritten += OnEntryWritten;
                available = true;
                AuditLog.Append("monitor.start", "Security event monitor started", "service");
            }
            catch (Exception ex)
            {
                available = false;
                AuditLog.Append("monitor.unavailable", ex.Message, "service");
            }
        }

        private void OnEntryWritten(object sender, EntryWrittenEventArgs e)
        {
            try
            {
                if (e.Entry == null)
                {
                    return;
                }

                long instanceId = e.Entry.InstanceId;
                if ((instanceId & 0xFFFF) != 5157)
                {
                    return;
                }

                string[] values = e.Entry.ReplacementStrings ?? new string[0];
                string application = values.Length > 1 ? values[1] : string.Empty;
                string processIdText = values.Length > 0 ? values[0] : string.Empty;
                string filterName = string.Empty;
                for (int i = 0; i < values.Length; i++)
                {
                    if (!string.IsNullOrEmpty(values[i]) && values[i].StartsWith("AegisWall", StringComparison.OrdinalIgnoreCase))
                    {
                        filterName = values[i];
                        break;
                    }
                }

                if (string.IsNullOrEmpty(filterName))
                {
                    return;
                }

                int processId;
                int.TryParse(processIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out processId);
                engine.OnBlockedConnection(application, processId, filterName);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (securityLog != null)
            {
                try
                {
                    securityLog.EnableRaisingEvents = false;
                    securityLog.EntryWritten -= OnEntryWritten;
                    securityLog.Dispose();
                }
                catch
                {
                }
                securityLog = null;
            }
            available = false;
        }
    }
}
