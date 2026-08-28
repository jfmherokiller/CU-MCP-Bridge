"""Raw test: write message to C# and see if it's processed."""
import sys, json, time, os, threading
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

h = win32pipe.CreateNamedPipe(r"\\.\pipe\CU-MCP-Bridge", win32pipe.PIPE_ACCESS_DUPLEX,
    win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
    1, 65536, 65536, 0, None)
print("[RAW] Connecting...")
win32pipe.ConnectNamedPipe(h, None)
print("[RAW] Connected!")

buf = b""
received = []
stop = False

def reader():
    global buf, received, stop
    while not stop:
        try:
            _, d = win32file.ReadFile(h, 4096)
            if not d: break
            buf += d
            while b"\n" in buf:
                idx = buf.index(b"\n")
                line = buf[:idx].decode("utf-8").strip()
                buf = buf[idx+1:]
                received.append(json.loads(line))
        except:
            break

t = threading.Thread(target=reader, daemon=True)
t.start()

time.sleep(2)
print(f"[RAW] Got {len(received)} msgs before write")

# Send a raw ping
test_msg = json.dumps({"type": "ping", "data": {}})
raw_bytes = (test_msg + "\n").encode("utf-8")
print(f"[RAW] Writing {len(raw_bytes)} bytes: {raw_bytes}")
win32file.WriteFile(h, raw_bytes)

time.sleep(3)
print(f"[RAW] Got {len(received)} msgs total")
for g in received:
    t = g.get("type", "?")
    s = g.get("seq", "?")
    d = str(g.get("data", {}))[:60]
    print(f"  type={t} seq={s} data={d}")

stop = True
try: win32file.CloseHandle(h)
except: pass
