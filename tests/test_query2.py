"""Test query: send query and print ALL messages to see response."""
import sys, json, time, os, threading
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

h = win32pipe.CreateNamedPipe(r"\\.\pipe\CU-MCP-Bridge", win32pipe.PIPE_ACCESS_DUPLEX,
    win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
    1, 65536, 65536, 0, None)
print("[Q] Connecting...")
win32pipe.ConnectNamedPipe(h, None)
print("[Q] Connected!")

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

t = threading.Thread(target=reader, daemon=True); t.start()
time.sleep(2)
print(f"[Q] {len(received)} msgs before send")

# Send query
payload = json.dumps({"type": "query", "data": {"x": 0, "y": 0, "range": 5}})
win32file.WriteFile(h, (payload + "\n").encode("utf-8"))
print(f"[Q] Sent query: {payload}")
time.sleep(8)
print(f"[Q] {len(received)} msgs total:")

last_type = None
for g in received[-15:]:
    t = g.get("type", "?")
    if t != last_type:
        d = str(g.get("data", {}))[:150]
        print(f"  type={t} data={d}")
        last_type = t

stop = True
try: win32file.CloseHandle(h)
except: pass
