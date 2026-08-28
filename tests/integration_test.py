"""Integration test: connects to game mod via Named Pipe, verifies data flow."""
import json
import sys
import os
import time

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src", "bridge_server"))
from pipe_server import PipeServer

PIPE_NAME = r"\\.\pipe\CU-MCP-Bridge"


def main():
    print("=" * 50)
    print("CU-MCP-Bridge Integration Test")
    print("=" * 50)
    print()

    pipe = PipeServer(PIPE_NAME)

    print("[TEST] Connecting to Named Pipe...")
    timeout = 30.0
    start = time.time()
    connected = False

    while time.time() - start < timeout:
        try:
            pipe.wait_for_connect()
            connected = True
            print("[TEST] Connected!")
            break
        except Exception as e:
            elapsed = time.time() - start
            print(f"[TEST] Waiting ({elapsed:.0f}s)... {e}")
            time.sleep(2)

    if not connected:
        print("[TEST] FAILED: Could not connect within 30s")
        sys.exit(1)

    # Wait for state update
    print("[TEST] Waiting for game state...")
    timeout = 20.0
    start = time.time()
    state_received = False

    while time.time() - start < timeout:
        line = pipe.read_line()
        if line is None:
            print("[TEST] Pipe disconnected")
            break

        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            continue

        msg_type = msg.get("type", "")
        data = msg.get("data", {})

        if msg_type == "state_update":
            player = data.get("player", {})
            env = data.get("environment", {})
            pos = player.get("position", {})
            print(f"\n[TEST] State update received:")
            print(f"  Position: ({pos.get('x', '?')}, {pos.get('y', '?')})")
            print(f"  Health:   {player.get('health', '?')}")
            print(f"  Temp:     {player.get('temperature', '?')}")
            print(f"  Oxygen:   {player.get('oxygen', '?')}")
            print(f"  Items:    {player.get('items', [])}")
            print(f"  Entities: {len(env.get('entities', []))}")
            print(f"  Terrain:  {len(env.get('nearby_terrain', []))} blocks")
            state_received = True

        elif msg_type == "interrupt":
            reason = data.get("reason", "?")
            print(f"[TEST] Interrupt: {reason}")

        if state_received:
            # Send a test order
            print("\n[TEST] Sending test order (wait 2s)...")
            pipe.send({
                "type": "order",
                "data": {
                    "id": "test_001",
                    "action": "wait",
                    "parameters": {"seconds": 2},
                },
            })
            print("[TEST] Order sent!")

            # Read ACK
            line = pipe.read_line()
            if line:
                ack = json.loads(line)
                success = ack.get("data", {}).get("success")
                print(f"[TEST] ACK: success={success}")
            break

    pipe.close()

    if state_received:
        print("\n[PASS] State data received and order dispatched successfully!")
        sys.exit(0)
    else:
        print(f"\n[FAIL] No state update received within {timeout}s")
        sys.exit(1)


if __name__ == "__main__":
    main()
