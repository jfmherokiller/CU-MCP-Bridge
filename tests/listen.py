"""Reads messages from C# mod - no ping, just listen."""
import sys, json, time, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

h = win32pipe.CreateNamedPipe(r"\\.\pipe\CU-MCP-Bridge", win32pipe.PIPE_ACCESS_DUPLEX,
    win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
    1, 65536, 65536, 0, None)
print("[LISTEN] Waiting for C#...")
win32pipe.ConnectNamedPipe(h, None)
print("[LISTEN] Connected!")

buffer = b""
timeout = time.time() + 25
count = 0
while time.time() < timeout:
    try:
        _, data = win32file.ReadFile(h, 4096)
        if not data: break
        buffer += data
        while b"\n" in buffer:
            idx = buffer.index(b"\n")
            line = buffer[:idx].decode("utf-8").strip()
            buffer = buffer[idx+1:]
            j = json.loads(line)
            count += 1
            t = j["type"]
            seq = j.get("seq", "?")
            da = j.get("data", {})
            if t == "ack":
                print(f"[{count}] ACK seq={seq} ack_seq={da.get('ack_seq','?')} ok={da.get('success')}")
            elif t == "state_update":
                p = da.get("player", {})
                pos = p.get("Position", {})
                print(f"[{count}] STATE HP={p.get('Health','?')} "
                      f"Pos=({pos.get('X','?'):.1f},{pos.get('Y','?'):.1f})")
    except pywintypes.error as e:
        print(f"[LISTEN] Error: {e}"); break
try: win32file.CloseHandle(h)
except: pass
print(f"[LISTEN] Done ({count} messages in {time.time()- (timeout-25):.0f}s)")
