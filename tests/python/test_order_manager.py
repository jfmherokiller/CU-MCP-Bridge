import pytest
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src", "bridge_server"))

from order_manager import OrderManager, Order
from unittest.mock import AsyncMock, MagicMock


@pytest.mark.asyncio
async def test_order_dispatch():
    """Test order dispatch creates correct message."""
    mock_pipe = AsyncMock()
    mgr = OrderManager(mock_pipe)

    order = Order(action="move_to", parameters={"x": 50.0, "y": -100.0})
    await mgr.dispatch(order)

    mock_pipe.send.assert_called_once()
    sent = mock_pipe.send.call_args[0][0]
    assert sent["type"] == "order"
    assert sent["data"]["action"] == "move_to"
    assert sent["data"]["parameters"]["x"] == 50.0
    assert order.status == "dispatched"


@pytest.mark.asyncio
async def test_pipeline_variables():
    """Test pipeline input/output variable passing."""
    mock_pipe = AsyncMock()
    mgr = OrderManager(mock_pipe)

    order_a = Order(
        action="get_position", parameters={},
        pipeline_output="ord_get_pos"
    )
    await mgr.dispatch(order_a)
    mgr.mark_completed(order_a.id, result={"x": 10.0, "y": -20.0})

    order_b = Order(
        action="move_to", parameters={},
        pipeline_input="ord_get_pos"
    )
    result = mgr.resolve_pipeline_input(order_b)
    assert result is not None
    assert result["x"] == 10.0
    assert result["y"] == -20.0


@pytest.mark.asyncio
async def test_order_tracking():
    """Test order status tracking."""
    mock_pipe = AsyncMock()
    mgr = OrderManager(mock_pipe)

    order = Order(action="wait", parameters={"seconds": 1})
    await mgr.dispatch(order)
    assert order.status == "dispatched"

    assert order.id in mgr._orders

    mgr.mark_completed(order.id)
    assert order.status == "completed"
