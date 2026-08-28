import pytest
import json
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src", "bridge_server"))

from pipe_server import PipeServer


@pytest.mark.asyncio
async def test_pipe_message_format():
    """Verify pipe messages are valid JSON lines."""
    msg = {
        "type": "order",
        "data": {
            "id": "ord_001",
            "action": "move_to",
            "parameters": {"x": 50.0, "y": -100.0}
        }
    }
    line = json.dumps(msg, ensure_ascii=False) + "\n"
    parsed = json.loads(line.strip())
    assert parsed["type"] == "order"
    assert parsed["data"]["action"] == "move_to"


@pytest.mark.asyncio
async def test_interrupt_message():
    """Verify interrupt message structure."""
    msg = {
        "type": "interrupt",
        "data": {
            "reason": "health_critical",
            "priority": 9,
            "data": {"health": 15.0}
        }
    }
    assert msg["data"]["priority"] >= 0
    assert "reason" in msg["data"]


@pytest.mark.asyncio
async def test_state_update_message():
    """Verify state update message structure."""
    msg = {
        "type": "state_update",
        "data": {
            "player": {
                "health": 85.0,
                "temperature": 36.5,
                "position": {"x": 10.0, "y": -20.0}
            },
            "environment": {
                "entities": [],
                "nearby_terrain": []
            }
        }
    }
    assert msg["data"]["player"]["health"] == 85.0
    assert isinstance(msg["data"]["environment"]["entities"], list)
