using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Zyre.Tests
{
    [TestFixture]
    public class ZrePeerTests
    {
        [Test]
        public void SendMessageTest()
        {
            var peers = new Dictionary<Guid, ZrePeer>();
            var me = Guid.NewGuid();
            var you = Guid.NewGuid();
            using (var peer = ZrePeer.NewPeer(peers, you))
            using (var mailbox = new RouterSocket("tcp://127.0.0.1:5551")) // RouterSocket default action binds to the address
            {
                peer.Connected.Should().BeFalse();
                peer.SetName("PeerYou");
                peer.Name.Should().Be("PeerYou");
                peer.Connect(me, "tcp://127.0.0.1:5551");
                peer.Connected.Should().BeTrue();
                var helloMsg = new ZreMsg
                {
                    Id = ZreMsg.MessageId.Hello,
                    Hello = { Endpoint = "tcp://127.0.0.1:5552" }
                };
                var success = peer.Send(helloMsg);
                success.Should().BeTrue();
                var receiveMessage = mailbox.ReceiveMultipartMessage();
                receiveMessage.FrameCount.Should().Be(2);
            }
        }
    }
}
