import json
import os
import sys
import threading
import tempfile

# Platform detection
_IS_WINDOWS = sys.platform == "win32"

if _IS_WINDOWS:
    import win32pipe
    import win32file
    import pywintypes

PIPE_NAME = r"\\.\pipe\CU-MCP-Bridge"
SOCKET_PATH = os.path.join(tempfile.gettempdir(), "cu_mcp_bridge.sock")


class PipeServer:
    """Synchronous Named Pipe server.

    On Windows: uses win32pipe (named pipe).
    On Linux: uses Unix domain socket (fallback for Docker/remote).

    Runs on a background thread. Communicates with the C# BepInEx plugin.
    """

    def __init__(self, pipe_name: str = PIPE_NAME):
        self.pipe_name = pipe_name
        self._handle = None
        self._socket = None
        self._client_socket = None
        self._buffer = b""
        self._lock = threading.Lock()
        self._connected = False
        self._stop_event = threading.Event()

    @property
    def connected(self) -> bool:
        return self._connected

    def wait_for_connect(self, timeout: float = None):
        """Create the pipe/socket server and wait for the client to connect."""
        if _IS_WINDOWS:
            self._wait_for_connect_windows()
        else:
            self._wait_for_connect_unix()

    def _wait_for_connect_windows(self):
        try:
            self._handle = win32pipe.CreateNamedPipe(
                self.pipe_name,
                win32pipe.PIPE_ACCESS_DUPLEX,
                win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
                1, 65536, 65536, 0, None,
            )
            print(f"[PipeServer] Waiting for client on {self.pipe_name} ...")
            win32pipe.ConnectNamedPipe(self._handle, None)
            self._connected = True
            print("[PipeServer] Client connected")
        except pywintypes.error as e:
            print(f"[PipeServer] CreateNamedPipe error: {e}")
            raise

    def _wait_for_connect_unix(self):
        import socket
        if os.path.exists(SOCKET_PATH):
            os.unlink(SOCKET_PATH)
        self._socket = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        self._socket.bind(SOCKET_PATH)
        self._socket.listen(1)
        print(f"[PipeServer] Waiting for client on {SOCKET_PATH} ...")
        self._socket.settimeout(None)
        self._client_socket, _ = self._socket.accept()
        self._connected = True
        print("[PipeServer] Client connected")

    def read_line(self) -> str | None:
        """Read one JSON line (\\n-terminated) from the pipe, or None on EOF."""
        if not self._connected:
            return None
        if _IS_WINDOWS:
            return self._read_line_windows()
        else:
            return self._read_line_unix()

    def _read_line_windows(self) -> str | None:
        if self._handle is None:
            return None
        while b"\n" not in self._buffer:
            try:
                _, data = win32file.ReadFile(self._handle, 4096)
                if not data:
                    self._connected = False
                    return None
                self._buffer += data
            except pywintypes.error as e:
                if e.winerror == 109:  # ERROR_BROKEN_PIPE
                    self._connected = False
                    return None
                raise
        idx = self._buffer.index(b"\n")
        line = self._buffer[:idx].decode("utf-8").strip()
        self._buffer = self._buffer[idx + 1:]
        return line

    def _read_line_unix(self) -> str | None:
        if self._client_socket is None:
            return None
        while b"\n" not in self._buffer:
            try:
                data = self._client_socket.recv(4096)
                if not data:
                    self._connected = False
                    return None
                self._buffer += data
            except Exception:
                self._connected = False
                return None
        idx = self._buffer.index(b"\n")
        line = self._buffer[:idx].decode("utf-8").strip()
        self._buffer = self._buffer[idx + 1:]
        return line

    def send(self, message: dict) -> bool:
        """Send a JSON message (one line) to the C# mod. Returns True on success."""
        if not self._connected:
            return False
        payload = (json.dumps(message, ensure_ascii=False) + "\n").encode("utf-8")
        if _IS_WINDOWS:
            return self._send_windows(payload)
        else:
            return self._send_unix(payload)

    def _send_windows(self, payload: bytes) -> bool:
        if self._handle is None:
            return False
        with self._lock:
            try:
                err, n = win32file.WriteFile(self._handle, payload)
                return err == 0
            except pywintypes.error as e:
                if e.winerror == 109:
                    self._connected = False
                else:
                    raise
                return False

    def _send_unix(self, payload: bytes) -> bool:
        if self._client_socket is None:
            return False
        with self._lock:
            try:
                self._client_socket.sendall(payload)
                return True
            except Exception:
                self._connected = False
                return False

    def close(self):
        self._connected = False
        self._stop_event.set()
        if _IS_WINDOWS:
            self._close_windows()
        else:
            self._close_unix()

    def _close_windows(self):
        if self._handle:
            try:
                win32pipe.DisconnectNamedPipe(self._handle)
            except Exception:
                pass
            try:
                win32file.CloseHandle(self._handle)
            except Exception:
                pass
            self._handle = None

    def _close_unix(self):
        if self._client_socket:
            try:
                self._client_socket.close()
            except Exception:
                pass
            self._client_socket = None
        if self._socket:
            try:
                self._socket.close()
            except Exception:
                pass
            self._socket = None
        if os.path.exists(SOCKET_PATH):
            try:
                os.unlink(SOCKET_PATH)
            except Exception:
                pass
