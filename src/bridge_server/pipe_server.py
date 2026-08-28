import json
import threading
import win32pipe
import win32file
import pywintypes

PIPE_NAME = r"\\.\pipe\CU-MCP-Bridge"


class PipeServer:
    """Synchronous Named Pipe server using win32pipe API.

    Runs on a background thread.  Communicates with the C# BepInEx plugin
    inside the Unity game process.
    """

    def __init__(self, pipe_name: str = PIPE_NAME):
        self.pipe_name = pipe_name
        self._handle = None
        self._buffer = b""
        self._lock = threading.Lock()
        self._connected = False
        self._stop_event = threading.Event()

    @property
    def connected(self) -> bool:
        return self._connected

    def wait_for_connect(self, timeout: float = None):
        """Create the named pipe server and wait for the C# client to connect.

        Blocks until a client connects.
        """
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

    def read_line(self) -> str | None:
        """Read one JSON line (\\n-terminated) from the pipe, or None on EOF."""
        if not self._connected or self._handle is None:
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

    def send(self, message: dict):
        """Send a JSON message (one line) to the C# mod."""
        if not self._connected or self._handle is None:
            print(f"[PipeServer] Send skipped: connected={self._connected} handle={self._handle is not None}", file=__import__('sys').stderr)
            return
        payload = (json.dumps(message, ensure_ascii=False) + "\n").encode("utf-8")
        with self._lock:
            try:
                err, n = win32file.WriteFile(self._handle, payload)
                print(f"[PipeServer] WriteFile: err={err} bytes={n}", file=__import__('sys').stderr)
            except pywintypes.error as e:
                print(f"[PipeServer] WriteFile error: winerror={e.winerror} msg={e}", file=__import__('sys').stderr)
                if e.winerror == 109:
                    self._connected = False
                else:
                    raise

    def close(self):
        self._connected = False
        self._stop_event.set()
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
