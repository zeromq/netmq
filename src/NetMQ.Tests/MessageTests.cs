using System.Text;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void AddFrame()
        {
            var message = new NetMQMessage();

            Assert.AreEqual(0, message.FrameCount);
            Assert.True(message.IsEmpty);

            message.Append("Hello");

            Assert.AreEqual("Hello", message[0].ConvertToString());
            Assert.False(message.IsEmpty);
            Assert.AreSame(message[0], message.First);
            Assert.AreSame(message[0], message.Last);
            Assert.AreEqual(1, message.FrameCount);
        }

        [Test]
        public void TwoFrames()
        {
            var message = new NetMQMessage();

            Assert.AreEqual(0, message.FrameCount);
            Assert.True(message.IsEmpty);

            message.Append("Hello");
            message.Append("Hello2");

            Assert.AreEqual("Hello", message[0].ConvertToString());
            Assert.AreEqual("Hello2", message[1].ConvertToString());
            Assert.False(message.IsEmpty);
            Assert.AreSame(message[0], message.First);
            Assert.AreSame(message[1], message.Last);
            Assert.AreNotSame(message[0], message[1]);
            Assert.AreEqual(2, message.FrameCount);
        }

        [Test]
        public void PushMessage()
        {
            var message = new NetMQMessage();

            Assert.AreEqual(0, message.FrameCount);
            Assert.True(message.IsEmpty);

            message.Append("Hello");
            message.Push("Hello2");

            Assert.AreEqual("Hello", message[1].ConvertToString());
            Assert.AreEqual("Hello2", message[0].ConvertToString());
            Assert.False(message.IsEmpty);
            Assert.AreSame(message[0], message.First);
            Assert.AreSame(message[1], message.Last);
            Assert.AreNotSame(message[0], message[1]);
            Assert.AreEqual(2, message.FrameCount);
        }

        [Test]
        public void EmptyFrames()
        {
            var message = new NetMQMessage();

            message.Append("middle");
            message.AppendEmptyFrame();
            message.PushEmptyFrame();

            Assert.AreEqual("middle", message[1].ConvertToString());
            Assert.AreEqual(0, message[0].MessageSize);
            Assert.AreEqual(0, message[2].MessageSize);
            Assert.AreEqual(3, message.FrameCount);
        }

        [Test]
        public void RouterDealerMessaging()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (var server = context.CreateRouterSocket())
                {
                    int port = server.BindRandomPort("tcp://127.0.0.1");

                    using (var client = context.CreateDealerSocket())
                    {
                        client.Connect("tcp://127.0.0.1:" + port);

                        NetMQMessage clientOutgoingMessage = new NetMQMessage();
                        clientOutgoingMessage.Append("Hello");

                        client.SendMessage(clientOutgoingMessage);

                        NetMQMessage serverIncomingMessage = server.ReceiveMessage();

                        // number of frames should be one because first message should be identity of client
                        Assert.AreEqual(2, serverIncomingMessage.FrameCount);
                        Assert.AreEqual("Hello", serverIncomingMessage[1].ConvertToString());

                        NetMQMessage serverOutgoingMessage = new NetMQMessage();

                        // first adding the identity
                        serverOutgoingMessage.Append(serverIncomingMessage[0]);
                        serverOutgoingMessage.Append("World");

                        server.SendMessage(serverOutgoingMessage);

                        NetMQMessage incomingClientMessage = new NetMQMessage();
                        client.ReceiveMessage(incomingClientMessage);

                        Assert.AreEqual(1, incomingClientMessage.FrameCount);
                        Assert.AreEqual("World", incomingClientMessage[0].ConvertToString());
                    }
                }
            }
        }

        [Test]
        public void Issue52_ReqToRouterBug()
        {
            using (var ctx = NetMQContext.Create())
            {
                using (NetMQSocket router = ctx.CreateRouterSocket(), req = ctx.CreateRequestSocket())
                {
                    router.Bind("inproc://example");
                    req.Connect("inproc://example");

                    string testmessage = "Simple Messaging Test";
                    req.Send(testmessage);

                    var msg = router.ReceiveMessage();
                    Assert.AreEqual(3, msg.FrameCount);
                    Assert.AreEqual(msg[2].ConvertToString(), testmessage);
                }
            }
        }

        [Test]
        public void MessageToString()
        {
            var message = new NetMQMessage();
            Assert.AreEqual("NetMQMessage[<no frames>]", message.ToString());

            message.Append("Hello");
            Assert.AreEqual("NetMQMessage[Hello]", message.ToString());

            message.AppendEmptyFrame();
            message.Append("World");
            Assert.AreEqual("NetMQMessage[Hello,,World]", message.ToString());
        }

        [Test]
        public void SpecifyEncoding()
        {
            NetMQFrame frame =new NetMQFrame("Hello", Encoding.UTF32);
            
            // size should be 4 times the string length because of using utf32
            Assert.AreEqual(20, frame.MessageSize);

            Assert.AreEqual("Hello", frame.ConvertToString(Encoding.UTF32));
        }

        [Test]
        public void AppendInt32()
        {
            NetMQMessage message = new NetMQMessage();

            message.Append("Hello");
            message.Append(5);

            Assert.AreEqual(4, message[1].MessageSize);
            Assert.AreEqual(5, message[1].ConvertToInt32());
        }

        [Test]
        public void PushInt32()
        {
            NetMQMessage message = new NetMQMessage();

            message.Append("Hello");
            message.Push(5);

            Assert.AreEqual(4, message[0].MessageSize);
            Assert.AreEqual(5, message[0].ConvertToInt32());
        }

        [Test]
        public void AppendInt64()
        {
            long num = (long)int.MaxValue + 1;            

            NetMQMessage message = new NetMQMessage();

            message.Append("Hello");
            message.Append(num);

            Assert.AreEqual(8, message[1].MessageSize);
            Assert.AreEqual(num, message[1].ConvertToInt64());
        }

        [Test]
        public void PushInt64()
        {
            long num = (long)int.MaxValue + 1;            

            NetMQMessage message = new NetMQMessage();

            message.Append("Hello");
            message.Push(num);

            Assert.AreEqual(8, message[0].MessageSize);
            Assert.AreEqual(num, message[0].ConvertToInt64());
        }
    }
}
