import pytest
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src", "bridge_server"))

from contingency import ContingencyManager, ContingencyRule
from unittest.mock import AsyncMock


@pytest.mark.asyncio
async def test_contingency_update():
    """Test contingency rule update message."""
    mock_pipe = AsyncMock()
    mgr = ContingencyManager(mock_pipe)

    rules = [
        ContingencyRule("health < 30", "use_item", {"item": "bandage"}),
        ContingencyRule("temperature > 40", "use_item", {"item": "cooling_pack"}),
    ]
    await mgr.update_rules(rules)

    mock_pipe.send.assert_called_once()
    sent = mock_pipe.send.call_args[0][0]
    assert sent["type"] == "contingency_update"
    assert len(sent["data"]["rules"]) == 2
    assert sent["data"]["rules"][0]["condition"] == "health < 30"


@pytest.mark.asyncio
async def test_contingency_clear():
    """Test contingency clear message."""
    mock_pipe = AsyncMock()
    mgr = ContingencyManager(mock_pipe)

    await mgr.clear_rules()
    mock_pipe.send.assert_called_once()
    assert mock_pipe.send.call_args[0][0]["type"] == "contingency_clear"


@pytest.mark.asyncio
async def test_contingency_rule_serialization():
    """Test rule to_dict conversion."""
    rule = ContingencyRule("oxygen < 10", "use_item", {"item": "oxygen_tank"})
    d = rule.to_dict()
    assert d["condition"] == "oxygen < 10"
    assert d["action"] == "use_item"
    assert d["parameters"]["item"] == "oxygen_tank"
