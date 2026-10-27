using System;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Threading;
using System.Xml.Linq;

namespace AegisWall.Core
{
    public sealed class BlockEventMonitor : IDisposable
    {
        private readonly ProtectionEngine engine;
        private Timer timer;
        private long lastRecordId;
        private bool available;
        private bool polling;

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
                EventLogQuery query = new EventLogQuery("Security", PathType.LogName, "*[System[(EventID=5157)]]");
                query.ReverseDirection = true;
                using (EventLogReader reader = new EventLogReader(query))
                {
                    EventRecord record = reader.ReadEvent();
                    if (record != null)
                    {
                        lastRecordId = record.RecordId ?? 0;
                        record.Dispose();
                    }
                }

                available = true;
                timer = new Timer(Poll, null, 500, 1500);
                AuditLog.Append("monitor.start", "Security 5157 polling monitor started", "service");
            }
            catch (Exception ex)
            {
                available = false;
                AuditLog.Append("monitor.unavailable", ex.Message, "service");
            }
        }

        private void Poll(object state)
        {
            if (polling || !available)
            {
                return;
            }

            polling = true;
            try
            {
                EventLogQuery query = new EventLogQuery("Security", PathType.LogName, "*[System[(EventID=5157)]]");
                query.ReverseDirection = true;
                using (EventLogReader reader = new EventLogReader(query))
                {
                    for (int i = 0; i < 40; i++)
                    {
                        EventRecord record = reader.ReadEvent();
                        if (record == null)
                        {
                            break;
                        }

                        long recordId = 0;
                        try
                        {
                            recordId = record.RecordId ?? 0;
                            if (recordId <= lastRecordId)
                            {
                                break;
                            }

                            XDocument document = XDocument.Parse(record.ToXml());
                            string application = ReadData(document, "Application");
                            string filterName = ReadData(document, "FilterName");
                            string filterOrigin = ReadData(document, "FilterOrigin");
                            string processIdText = ReadData(document, "ProcessID");
                            if (string.IsNullOrEmpty(processIdText)) { processIdText = ReadData(document, "ProcessId"); }

                            if (string.IsNullOrEmpty(filterName)) { filterName = filterOrigin; }                            bool ours = (!string.IsNullOrEmpty(filterName) && filterName.StartsWith("AegisWall", StringComparison.OrdinalIgnoreCase))
                                || string.Equals(filterOrigin, "Unknown", StringComparison.OrdinalIgnoreCase);

                            if (ours)
                            {                                int processId;
                                int.TryParse(processIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out processId);
                                engine.OnBlockedConnection(application, processId, filterName);
                            }
                        }
                        finally
                        {
                            if (recordId > lastRecordId)
                            {
                                lastRecordId = recordId;
                            }
                            record.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AuditLog.Append("monitor.error", ex.Message, "service");
            }
            finally
            {
                polling = false;
            }
        }

        private static string ReadData(XDocument document, string name)
        {
            foreach (XElement element in document.Descendants())
            {
                if (element.Name.LocalName != "Data") { continue; }
                XAttribute attribute = element.Attribute("Name");
                if (attribute != null && string.Equals(attribute.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return element.Value;
                }
            }
            return string.Empty;
        }

        public void Dispose()
        {
            if (timer != null)
            {
                try
                {
                    timer.Dispose();
                }
                catch
                {
                }
                timer = null;
            }
            available = false;
        }
    }
}







