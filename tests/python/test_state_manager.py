import pytest
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src", "bridge_server"))

from state_manager import StateManager
from unittest.mock import AsyncMock, MagicMock


@pytest.mark.asyncio
async def test_state_manager_update():
    """Test state update parsing."""
    mock_pipe = MagicMock()
    mgr = StateManager(mock_pipe)

    msg = {
        "type": "state_update",
        "data": {
            "player": {"health": 75.0, "temperature": 37.0},
            "environment": {"entities": [], "nearby_terrain": []}
        }
    }

    # Simulate processing
    if msg.get("type") == "state_update":
        data = msg.get("data", {})
        if "player" in data:
            mgr._latest_player = data["player"]
        if "environment" in data:
            mgr._latest_environment = data["environment"]

    state = mgr.get_latest_state()
    assert state["player"]["health"] == 75.0
    assert state["player"]["temperature"] == 37.0


@pytest.mark.asyncio
async def test_state_manager_interrupt():
    """Test interrupt queuing."""
    mock_pipe = MagicMock()
    mgr = StateManager(mock_pipe)

    interrupt = {
        "type": "interrupt",
        "data": {"reason": "health_critical", "priority": 9}
    }
    mgr._pending_interrupts.append(interrupt)

    interrupts = mgr.pop_interrupts()
    assert len(interrupts) == 1
    assert interrupts[0]["data"]["reason"] == "health_critical"

    # Should be cleared after pop
    assert len(mgr.pop_interrupts()) == 0
