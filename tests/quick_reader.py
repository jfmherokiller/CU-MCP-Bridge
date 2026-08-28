"""Reads game state from C# mod via Named Pipe, sending pings to unblock reads."""
import sys, json, time, os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

h = win32pipe.CreateNamedPipe(
    r"\\.\pipe\CU-MCP-Bridge",
    win32pipe.PIPE_ACCESS_DUPLEX,
    win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
    1, 65536, 65536, 0, None,
)
print("[READER] Waiting for C# client...")
win32pipe.ConnectNamedPipe(h, None)
print("[READER] Connected! Sending ping to unblock reads...")

# Send a ping to unblock C# ReadLoop
ping = json.dumps({"type": "ping", "data": {}}) + "\n"
win32file.WriteFile(h, ping.encode("utf-8"))

buffer = b""
timeout = time.time() + 30
count = 0
while time.time() < timeout:
    try:
        _, data = win32file.ReadFile(h, 4096)
        if not data:
            print("[READER] EOF")
            break
        buffer += data
        while b"\n" in buffer:
            idx = buffer.index(b"\n")
            line = buffer[:idx].decode("utf-8").strip()
            buffer = buffer[idx + 1 :]
            j = json.loads(line)
            t = j.get("type", "?")
            count += 1
            if t == "ack":
                print(f"[READER] #{count} ACK success={j.get('data',{}).get('success')}")
            elif t == "state_update":
                p = j.get("data", {}).get("player", {})
                pos = p.get("Position", {})
                print(f"[READER] #{count} STATE HP={p.get('Health','?')} "
                      f"T={p.get('Temperature','?'):.1f} "
                      f"O2={p.get('Oxygen','?')} "
                      f"Pos=({pos.get('X','?'):.1f},{pos.get('Y','?'):.1f}) "
                      f"Items={p.get('Items',[])}")
                env = j.get("data", {}).get("environment", {})
                ents = env.get("Entities", [])
                terr = env.get("NearbyTerrain", [])
                if ents or terr:
                    print(f"         Entities={len(ents)} Terrain={len(terr)}")
            else:
                print(f"[READER] #{count} {t}: {str(j.get('data',{}))[:100]}")
    except pywintypes.error as e:
        print(f"[READER] Error: {e}")
        break

try:
    win32file.CloseHandle(h)
except:
    pass
print(f"[READER] Done ({count} messages)")
