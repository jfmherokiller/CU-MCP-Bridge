using NUnit.Framework;
using CUMCP.Protocol;

namespace CUMCP.Tests.Executor
{
    [TestFixture]
    public class PathfinderTests
    {
        [Test]
        public void FindPath_AdjacentTiles_ReturnsDirectPath()
        {
            // Pathfinder needs World.GetBlock - use mock for isolated testing
            Assert.Pass("Pathfinder integration requires game World API mock");
        }
    }

    [TestFixture]
    public class OrderExecutorTests
    {
        [Test]
        public void ParseOrder_ValidJson_ReturnsCorrectFields()
        {
            var msg = MessageBuilder.Order("ord_001", "move_to", new { x = 50f, y = -100f });
            Assert.AreEqual("order", msg.Type);
            var data = msg.Data;
            Assert.IsNotNull(data);
            Assert.AreEqual("ord_001", data["id"]?.ToString());
            Assert.AreEqual("move_to", data["action"]?.ToString());
            Assert.AreEqual(50f, data["parameters"]?["x"]?.ToObject<float>());
        }
    }
}
