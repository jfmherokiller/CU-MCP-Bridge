"""Monitor: dump all messages from C# mod."""
import sys, json, time, os, threading
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

h = win32pipe.CreateNamedPipe(r"\\.\pipe\CU-MCP-Bridge", win32pipe.PIPE_ACCESS_DUPLEX,
    win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
    1, 65536, 65536, 0, None)
print("[MON] Waiting for C# mod...")
win32pipe.ConnectNamedPipe(h, None)
print("[MON] Connected!")

buf = b""

def pinger():
    while True:
        try:
            win32file.WriteFile(h, json.dumps({"type":"ping"}).encode() + b"\n")
        except:
            break
        time.sleep(2)

t = threading.Thread(target=pinger, daemon=True)
t.start()

t0 = time.time()
count = 0
while time.time() - t0 < 40:
    try:
        _, data = win32file.ReadFile(h, 4096)
        if not data:
            break
        buf += data
        while b"\n" in buf:
            idx = buf.index(b"\n")
            line = buf[:idx].decode("utf-8").strip()
            buf = buf[idx+1:]
            j = json.loads(line)
            count += 1
            print(f"[{time.time()-t0:.0f}s] #{count} {j.get('type')}: {str(j.get('data',{}))[:120]}")
    except pywintypes.error as e:
        if e.winerror != 536870913:
            print(f"Error: {e}")
            break

print(f"[MON] Done ({count} msgs in {time.time()-t0:.0f}s)")
try:
    win32file.CloseHandle(h)
except:
    pass
