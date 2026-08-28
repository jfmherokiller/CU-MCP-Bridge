# -*- coding: utf-8 -*-
"""Test pathfinding with auto-reconnect handling."""
import sys, json, time, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
import win32pipe, win32file, pywintypes

PIPE = r"\\.\pipe\CU-MCP-Bridge"
h = None
buf = b""

def connect():
    global h, buf
    if h: 
        try: win32file.CloseHandle(h)
        except: pass
    h = win32pipe.CreateNamedPipe(PIPE, win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_READMODE_BYTE | win32pipe.PIPE_WAIT,
        1, 65536, 65536, 0, None)
    buf = b""
    win32pipe.ConnectNamedPipe(h, None)
    print(f"[PATH] Connected at {time.strftime('%H:%M:%S')}")

def send(msg):
    try:
        win32file.WriteFile(h, (json.dumps(msg) + "\n").encode("utf-8"))
        return True
    except pywintypes.error as e:
        print(f"[PATH] Send error: {e.winerror} - reconnecting...")
        connect()
        return False

def read_msg(timeout=3):
    global buf
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            _, data = win32file.ReadFile(h, 4096)
            if not data: return None
            buf += data
            while b"\n" in buf:
                idx = buf.index(b"\n")
                line = buf[:idx].decode("utf-8").strip()
                buf = buf[idx+1:]
                return json.loads(line)
        except pywintypes.error as e:
            if e.winerror == 536870913: continue
            return None
    return None

connect()

# Wait for state with reconnect handling
def wait_for_state(timeout=30):
    t0 = time.time()
    while time.time() - t0 < timeout:
        if not send({"type": "ping", "data": {}}): continue
        msg = read_msg(2)
        if msg and msg.get("type") == "state_update":
            p = msg["data"]["player"]
            pos = p.get("Position", {})
            return (pos.get("X", 0), pos.get("Y", 0))
    return None

print("[PATH] Getting initial position...")
pos = wait_for_state(40)
if pos is None:
    print("[PATH] FAIL: No state update")
    sys.exit(1)
print(f"[PATH] Start: ({pos[0]:.1f}, {pos[1]:.1f})")
start_pos = pos

# Send move_to
target_x = start_pos[0] + 8
print(f"[PATH] Move right -> ({target_x:.0f}, {start_pos[1]:.0f})")
send({"type": "order", "data": {
    "id": "path_test_001",
    "action": "move_to",
    "parameters": {"x": target_x, "y": start_pos[1]}
}})

# Monitor
print("[PATH] Monitoring...")
best_x = start_pos[0]
for i in range(20):
    time.sleep(1)
    if not send({"type": "ping", "data": {}}): break
    msg = read_msg(2)
    if msg and msg.get("type") == "state_update":
        p = msg["data"]["player"]
        cx = p.get("Position", {}).get("X", 0)
        dx = cx - start_pos[0]
        if cx > best_x: best_x = cx
        print(f"[PATH] +{i+1}s: x={cx:.1f} dx={dx:.1f}")

dx = best_x - start_pos[0]
print(f"\n[PATH] Moved {dx:.1f} units right")
if dx > 1:
    print(f"[PATH] PASS: moved right {dx:.1f} units")
else:
    print(f"[PATH] FAIL: didn't move right (dx={dx:.1f})")

try: win32file.CloseHandle(h)
except: pass
