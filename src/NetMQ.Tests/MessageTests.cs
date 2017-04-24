using System.Text;
using NetMQ.Sockets;
using Xunit;

namespace NetMQ.Tests
{
    public class MessageTests
    {
        [Fact]
        public void AddFrame()
        {
            var message = new NetMQMessage();

            Assert.Equal(0, message.FrameCount);
            Assert.True(message.IsEmpty);

            message.Append("Hello");

            Assert.Equal("Hello", message[0].ConvertToString());
            Assert.False(message.IsEmpty);
            Assert.Same(message[0], message.First);
            Assert.Same(message[0], message.Last);
            Assert.Equal(1, message.FrameCount);
        }

        [Fact]
        public void TwoFrames()
        {
            var message = new NetMQMessage();

            Assert.Equal(0, message.FrameCount);
            Assert.True(message.IsEmpty);

            message.Append("Hello");
            message.Append("Hello2");

            Assert.Equal("Hello", message[0].ConvertToString());
            Assert.Equal("Hello2", message[1].ConvertToString());
            Assert.False(message.IsEmpty);
            Assert.Same(message[0], message.First);
            Assert.Same(message[1], message.Last);
            Assert.NotSame(message[0], message[1]);
            Assert.Equal(2, message.FrameCount);
        }

        [Fact]
        public void PushMessage()
        {
            var message = new NetMQMessage();

            Assert.Equal(0, message.FrameCount);
            Assert.True(message.IsEmpty);

            message.Append("Hello");
            message.Push("Hello2");

            Assert.Equal("Hello", message[1].ConvertToString());
            Assert.Equal("Hello2", message[0].ConvertToString());
            Assert.False(message.IsEmpty);
            Assert.Same(message[0], message.First);
            Assert.Same(message[1], message.Last);
            Assert.NotSame(message[0], message[1]);
            Assert.Equal(2, message.FrameCount);
        }

        [Fact]
        public void EmptyFrames()
        {
            var message = new NetMQMessage();

            message.Append("middle");
            message.AppendEmptyFrame();
            message.PushEmptyFrame();

            Assert.Equal("middle", message[1].ConvertToString());
            Assert.Equal(0, message[0].MessageSize);
            Assert.Equal(0, message[2].MessageSize);
            Assert.Equal(3, message.FrameCount);
        }

        [Fact]
        public void RouterDealerMessaging()
        {
            using (var server = new RouterSocket())
            using (var client = new DealerSocket())
            {
                int port = server.BindRandomPort("tcp://127.0.0.1");
                client.Connect("tcp://127.0.0.1:" + port);

                var clientOutgoingMessage = new NetMQMessage();
                clientOutgoingMessage.Append("Hello");

                client.SendMultipartMessage(clientOutgoingMessage);

                NetMQMessage serverIncomingMessage = server.ReceiveMultipartMessage();

                // number of frames should be one because first message should be identity of client
                Assert.Equal(2, serverIncomingMessage.FrameCount);
                Assert.Equal("Hello", serverIncomingMessage[1].ConvertToString());

                var serverOutgoingMessage = new NetMQMessage();

                // first adding the identity
                serverOutgoingMessage.Append(serverIncomingMessage[0]);
                serverOutgoingMessage.Append("World");

                server.SendMultipartMessage(serverOutgoingMessage);

                var incomingClientMessage = client.ReceiveMultipartMessage();

                Assert.Equal(1, incomingClientMessage.FrameCount);
                Assert.Equal("World", incomingClientMessage[0].ConvertToString());
            }
        }

        [Fact]
        public void Issue52_ReqToRouterBug()
        {
            using (var router = new RouterSocket())
            using (var req = new RequestSocket())
            {
                router.Bind("inproc://example");
                req.Connect("inproc://example");

                const string testmessage = "Simple Messaging Test";

                req.SendFrame(testmessage);

                var msg = router.ReceiveMultipartMessage();
                Assert.Equal(3, msg.FrameCount);
                Assert.Equal(msg[2].ConvertToString(), testmessage);
            }
        }

        [Fact]
        public void MessageToString()
        {
            var message = new NetMQMessage();
            Assert.Equal("NetMQMessage[<no frames>]", message.ToString());

            message.Append("Hello");
            Assert.Equal("NetMQMessage[Hello]", message.ToString());

            message.AppendEmptyFrame();
            message.Append("World");
            Assert.Equal("NetMQMessage[Hello,,World]", message.ToString());
        }

        [Fact]
        public void SpecifyEncoding()
        {
            var frame = new NetMQFrame("Hello", Encoding.UTF32);

            // size should be 4 times the string length because of using utf32
            Assert.Equal(20, frame.MessageSize);

            Assert.Equal("Hello", frame.ConvertToString(Encoding.UTF32));
        }

        [Fact]
        public void AppendInt32()
        {
            var message = new NetMQMessage();

            message.Append("Hello");
            message.Append(5);

            Assert.Equal(4, message[1].MessageSize);
            Assert.Equal(5, message[1].ConvertToInt32());
        }

        [Fact]
        public void PushInt32()
        {
            var message = new NetMQMessage();

            message.Append("Hello");
            message.Push(5);

            Assert.Equal(4, message[0].MessageSize);
            Assert.Equal(5, message[0].ConvertToInt32());
        }

        [Fact]
        public void AppendInt64()
        {
            const long num = (long)int.MaxValue + 1;

            var message = new NetMQMessage();

            message.Append("Hello");
            message.Append(num);

            Assert.Equal(8, message[1].MessageSize);
            Assert.Equal(num, message[1].ConvertToInt64());
        }

        [Fact]
        public void PushInt64()
        {
            const long num = (long)int.MaxValue + 1;

            var message = new NetMQMessage();

            message.Append("Hello");
            message.Push(num);

            Assert.Equal(8, message[0].MessageSize);
            Assert.Equal(num, message[0].ConvertToInt64());
        }
    }
}
