using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace AegisWall.Core
{
    public sealed class IpcRequest
    {
        public string Cmd { get; set; }
        public string Path { get; set; }
        public bool Outbound { get; set; }
        public bool Inbound { get; set; }
        public bool Enabled { get; set; }
        public bool Replace { get; set; }
        public string Notes { get; set; }
        public int Minutes { get; set; }
        public int Limit { get; set; }
        public string RecoveryCode { get; set; }
        public string Reason { get; set; }
        public string Payload { get; set; }
        public string Actor { get; set; }

        public IpcRequest()
        {
            Cmd = string.Empty;
            Path = string.Empty;
            Outbound = true;
            Inbound = false;
            Enabled = true;
            Replace = false;
            Notes = string.Empty;
            Limit = 100;
            RecoveryCode = string.Empty;
            Reason = string.Empty;
            Payload = string.Empty;
            Actor = string.Empty;
        }
    }

    public sealed class IpcResponse
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
        public StatusSnapshot Status { get; set; }

        public IpcResponse()
        {
            Message = string.Empty;
            Data = string.Empty;
        }

        public static IpcResponse Error(string message)
        {
            IpcResponse response = new IpcResponse();
            response.Ok = false;
            response.Message = message;
            return response;
        }

        public static IpcResponse Success(string message)
        {
            IpcResponse response = new IpcResponse();
            response.Ok = true;
            response.Message = message;
            return response;
        }
    }

    public static class IpcNames
    {
        public const string ControlPipe = "AegisWall.Control";
    }

    public static class IpcClient
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static bool IsServiceReachable()
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", IpcNames.ControlPipe, PipeDirection.InOut))
                {
                    client.Connect(300);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static IpcResponse Send(IpcRequest request, int timeoutMs)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", IpcNames.ControlPipe, PipeDirection.InOut))
                {
                    client.Connect(timeoutMs);
                    using (StreamReader reader = new StreamReader(client, Utf8, false, 8192, true))
                    using (StreamWriter writer = new StreamWriter(client, Utf8, 8192, true))
                    {
                        writer.WriteLine(Json.Serialize(request));
                        writer.Flush();
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            return IpcResponse.Error("Service returned an empty response.");
                        }
                        return Json.Deserialize<IpcResponse>(line);
                    }
                }
            }
            catch (Exception ex)
            {
                return IpcResponse.Error("Service unavailable: " + ex.Message);
            }
        }
    }

    public sealed class IpcServer : IDisposable
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly Func<IpcRequest, IpcResponse> handler;
        private Thread thread;
        private volatile bool running;

        public IpcServer(Func<IpcRequest, IpcResponse> handler)
        {
            this.handler = handler;
        }

        public void Start()
        {
            if (running)
            {
                return;
            }

            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "AegisWall IPC";
            thread.Start();
        }

        private void Loop()
        {
            while (running)
            {
                try
                {
                    using (NamedPipeServerStream pipe = CreatePipe())
                    {
                        pipe.WaitForConnection();
                        using (StreamReader reader = new StreamReader(pipe, Utf8, false, 8192, true))
                        using (StreamWriter writer = new StreamWriter(pipe, Utf8, 8192, true))
                        {
                            string line = reader.ReadLine();
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            IpcResponse response;
                            try
                            {
                                IpcRequest request = Json.Deserialize<IpcRequest>(line);
                                response = handler(request);
                            }
                            catch (Exception ex)
                            {
                                response = IpcResponse.Error(ex.Message);
                            }

                            writer.WriteLine(Json.Serialize(response));
                            writer.Flush();
                        }
                    }
                }
                catch
                {
                    if (running)
                    {
                        Thread.Sleep(250);
                    }
                }
            }
        }

        private static NamedPipeServerStream CreatePipe()
        {
            PipeSecurity security = new PipeSecurity();
            SecurityIdentifier system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new PipeAccessRule(system, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));

            return new NamedPipeServerStream(
                IpcNames.ControlPipe,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.None,
                65536,
                65536,
                security);
        }

        public void Stop()
        {
            running = false;
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", IpcNames.ControlPipe, PipeDirection.InOut))
                {
                    client.Connect(300);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
