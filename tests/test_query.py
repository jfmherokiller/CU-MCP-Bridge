"""Test query_position: ask C# mod for entities and terrain at a coordinate."""
import sys, json, time, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

h = win32pipe.CreateNamedPipe(r"\\.\pipe\CU-MCP-Bridge", win32pipe.PIPE_ACCESS_DUPLEX,
    win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
    1, 65536, 65536, 0, None)
print("[QUERY] Connecting...")
win32pipe.ConnectNamedPipe(h, None)
print("[QUERY] Connected!")

buf = b""

def send(msg):
    win32file.WriteFile(h, (json.dumps(msg) + "\n").encode("utf-8"))

def read_all(timeout=15):
    global buf
    deadline = time.time() + timeout
    msgs = []
    while time.time() < deadline:
        try:
            _, data = win32file.ReadFile(h, 4096)
            if not data: break
            buf += data
            while b"\n" in buf:
                idx = buf.index(b"\n")
                line = buf[:idx].decode("utf-8").strip()
                buf = buf[idx+1:]
                msgs.append(json.loads(line))
        except pywintypes.error as e:
            if e.winerror == 536870913: continue
            break
    return msgs

# Send query
query_x, query_y, query_range = 0, 0, 15
print(f"[QUERY] Querying ({query_x}, {query_y}) range={query_range}...")
send({"type": "query", "data": {"x": query_x, "y": query_y, "range": query_range}})

# Read ALL messages until we get query_result or timeout
msgs = read_all(15)
print(f"[QUERY] Got {len(msgs)} messages")

found = False
for i, msg in enumerate(msgs):
    d = msg.get("data", {})
    qr = d.get("query_result")
    if qr:
        ents = qr.get("Entities", [])
        terr = qr.get("NearbyTerrain", [])
        print(f"\n[QUERY] RESULT #{i+1}: {len(ents)} entities, {len(terr)} blocks")
        if ents:
            for e in ents[:10]:
                print(f"  {e.get('Type')} id={e.get('Id','?')[:20]} at ({e.get('X',0):.1f},{e.get('Y',0):.1f}) HP={e.get('Health','?')}")
        if terr:
            for t in terr[:10]:
                print(f"  BLOCK ({t['TileX']},{t['TileY']}) {t.get('Material','?')}")
        found = True
        break

if not found:
    print("[QUERY] No query_result in any message")

try: win32file.CloseHandle(h)
except: pass
