using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using CUMCP.Pipe;
using CUMCP.Protocol;
using NUnit.Framework;

namespace CUMCP.Tests.Pipe
{
    [TestFixture]
    public class PipeClientTests
    {
        private const string TestPipeName = "CU-MCP-Test-Pipe";

        [Test]
        public void Send_ValidMessage_ReceivedByServer()
        {
            string receivedJson = null;
            var serverReady = new ManualResetEvent(false);
            var msgReceived = new ManualResetEvent(false);

            var serverThread = new Thread(() =>
            {
                using var pipe = new NamedPipeServerStream(TestPipeName, PipeDirection.InOut, 1);
                serverReady.Set();
                pipe.WaitForConnection();
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                receivedJson = reader.ReadLine();
                msgReceived.Set();
            })
            { IsBackground = true };
            serverThread.Start();

            serverReady.WaitOne(2000);
            var client = new PipeClient(TestPipeName);
            client.Connect(2000);

            var msg = MessageBuilder.StateUpdate(new { test = true });
            client.Send(msg);

            Assert.IsTrue(msgReceived.WaitOne(2000), "Server did not receive message");
            Assert.IsNotNull(receivedJson);
            var deserialized = Message.FromJson(receivedJson);
            Assert.AreEqual("state_update", deserialized.Type);

            client.Disconnect();
        }
    }
}
