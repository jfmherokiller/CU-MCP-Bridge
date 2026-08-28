using NUnit.Framework;
using CUMCP.Protocol;

namespace CUMCP.Tests.Contingency
{
    [TestFixture]
    public class ContingencyRuleTests
    {
        [Test]
        public void ContingencyRule_Serialize_RoundTrip()
        {
            var rule = new ContingencyRule
            {
                Condition = "health < 30",
                Action = "use_item",
                Parameters = Newtonsoft.Json.Linq.JObject.FromObject(new { item = "bandage" })
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(rule);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<ContingencyRule>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("health < 30", deserialized.Condition);
            Assert.AreEqual("use_item", deserialized.Action);
            Assert.AreEqual("bandage", deserialized.Parameters["item"]?.ToString());
        }
    }
}
