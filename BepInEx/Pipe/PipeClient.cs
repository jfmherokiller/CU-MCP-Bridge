using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using CUMCP.Protocol;

namespace CUMCP.Pipe
{
    public class PipeClient : IDisposable
    {
        public static Action<string> Log; // Set by BridgePlugin

        public event Action<Message> OnMessage;
        public event Action<string> OnError;
        public event Action OnDisconnected;

        public bool Connected => _connected;
        public long LastReadTicks => _lastReadTicks;

        private readonly string _pipeName;
        private NamedPipeClientStream _pipe;
        private Thread _connectThread;
        private Thread _readThread;
        private volatile bool _running;
        private volatile bool _connected;
        private long _lastReadTicks;
        private readonly object _writeLock = new object();
        private readonly byte[] _readBuffer = new byte[4096];
        private string _lineBuffer = "";

        public PipeClient(string pipeName = "CU-MCP-Bridge")
        {
            _pipeName = pipeName;
        }

        public void ConnectAsync()
        {
            if (_connected) return;
            _running = true;
            _connectThread = new Thread(ConnectLoop)
            {
                IsBackground = true,
                Name = "CU-MCP-Connector"
            };
            _connectThread.Start();
        }

        public void Disconnect()
        {
            _connected = false;
            try { _pipe?.Dispose(); } catch { }
        }

        public void Send(Message msg)
        {
            if (!_connected) return;
            try
            {
                var json = msg.ToJson() + "\n";
                var bytes = new UTF8Encoding(false).GetBytes(json);
                lock (_writeLock)
                {
                    _pipe.Write(bytes, 0, bytes.Length);
                    _pipe.Flush();
                }
                _lastReadTicks = DateTime.UtcNow.Ticks;
            }
            catch (Exception)
            {
                _connected = false;
                try { OnDisconnected?.Invoke(); } catch { }
                StartReconnect();
            }
        }

        private void ConnectLoop()
        {
            while (_running && !_connected)
            {
                try
                {
                    var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
                    pipe.Connect(3000);
                    _pipe = pipe;
                    _connected = true;
                    _lastReadTicks = DateTime.UtcNow.Ticks;
                    Log?.Invoke("[CU-MCP] Pipe connected, starting ReadLoop");

                    _readThread = new Thread(ReadLoop)
                    {
                        IsBackground = true,
                        Name = "CU-MCP-Reader"
                    };
                    _readThread.Start();
                    return;
                }
                catch
                {
                    Thread.Sleep(3000);
                }
            }
        }

        private void ReadLoop()
        {
            _pipe.ReadTimeout = 100;
            Log?.Invoke("[CU-MCP] ReadLoop started");
            while (_running && _connected)
            {
                try
                {
                    int bytesRead = _pipe.Read(_readBuffer, 0, _readBuffer.Length);
                    if (bytesRead == 0) { Log?.Invoke("[CU-MCP] ReadLoop EOF"); HandleDisconnect(); break; }

                    _lastReadTicks = DateTime.UtcNow.Ticks;
                    string chunk = Encoding.UTF8.GetString(_readBuffer, 0, bytesRead);
                    _lineBuffer += chunk;

                    while (true)
                    {
                        int idx = _lineBuffer.IndexOf('\n');
                        if (idx < 0) break;
                        var line = _lineBuffer.Substring(0, idx).Trim();
                        _lineBuffer = _lineBuffer.Substring(idx + 1);
                        if (line.Length > 0)
                        {
                            try
                            {
                                var msg = Message.FromJson(line);
                                Log?.Invoke($"[CU-MCP] ReadLoop: type={msg.Type} seq={msg.Seq}");
                                if (OnMessage != null)
                                    OnMessage.Invoke(msg);
                                else
                                    Log?.Invoke("[CU-MCP] ERROR: OnMessage is NULL!");
                            }
                            catch (Exception ex)
                            {
                                Log?.Invoke($"[CU-MCP] ReadLoop parse error: {ex.Message}");
                            }
                        }
                    }
                }
                catch (TimeoutException) { }
                catch (Exception ex) when (_running)
                {
                    Log?.Invoke($"[CU-MCP] ReadLoop exception: {ex.GetType().Name}");
                    HandleDisconnect();
                    break;
                }
            }
            Log?.Invoke("[CU-MCP] ReadLoop ended");
        }

        private void HandleDisconnect()
        {
            _connected = false;
            try { OnDisconnected?.Invoke(); } catch { }
            StartReconnect();
        }

        private void StartReconnect()
        {
            if (!_running) return;
            _connectThread = new Thread(ConnectLoop)
            {
                IsBackground = true,
                Name = "CU-MCP-Reconnector"
            };
            _connectThread.Start();
        }

        public void Dispose() => Disconnect();
    }
}
