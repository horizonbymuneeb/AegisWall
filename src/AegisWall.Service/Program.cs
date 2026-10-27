using System;
using System.ServiceProcess;
using System.Threading;
using AegisWall.Core;

namespace AegisWall.Service
{
    public sealed class AegisWallWindowsService : ServiceBase
    {
        private ProtectionEngine engine;

        public AegisWallWindowsService()
        {
            ServiceName = "AegisWall";
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            engine = new ProtectionEngine();
            engine.Start();
        }

        protected override void OnStop()
        {
            if (engine != null)
            {
                engine.Stop();
                engine.Dispose();
                engine = null;
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && string.Equals(args[0], "--console", StringComparison.OrdinalIgnoreCase))
            {
                RunConsole();
                return;
            }

            ServiceBase.Run(new AegisWallWindowsService());
        }

        private static void RunConsole()
        {
            Console.WriteLine("AegisWall service (console mode). Press Ctrl+C to stop.");
            ProtectionEngine engine = new ProtectionEngine();
            ManualResetEvent stop = new ManualResetEvent(false);
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                stop.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += delegate { stop.Set(); };

            engine.Start();
            stop.WaitOne();
            engine.Stop();
            engine.Dispose();
            Console.WriteLine("Stopped. Persistent filters are left in place while armed.");
        }
    }
}
