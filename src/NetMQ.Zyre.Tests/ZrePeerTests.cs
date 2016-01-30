using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace NetMQ.Zyre.Tests
{
    [TestFixture]
    public class ZrePeerTests
    {
        [Test]
        public void ConnectedTest()
        {

           using (var peer = ZrePeer.NewPeer(new Dictionary<Guid, ZrePeer>(), Guid.NewGuid()))
            {
                peer.Connected.Should().BeFalse();

                peer.Connect(Guid.NewGuid(), "tcp://127.0.0.1:5551");
                peer.Connected.Should().BeTrue();

                peer.Disconnect();
                peer.Connected.Should().BeFalse();
            }
        }

        [Test]
        public void NameTest()
        {
            using (var peer = ZrePeer.NewPeer(new Dictionary<Guid, ZrePeer>(), Guid.NewGuid()))
            {
                peer.SetName("TestName");
                peer.Name.Should().Be("TestName");
            }
        }

        [Test]
        public void SendMessageTest()
        {
            var peers = new Dictionary<Guid, ZrePeer>();
            var me = Guid.NewGuid();
            var you = Guid.NewGuid();
            using (var peerYou = ZrePeer.NewPeer(peers, you))
            using (var peerMe = ZrePeer.NewPeer(peers, me))
            {
                peerYou.Connected.Should().BeFalse();
                peerYou.SetName("PeerYou");
                peerMe.Connect(me, "tcp://127.0.0.1:5551");
                peerMe.Connected.Should().BeTrue();
                peerMe.SetName("PeerMe");
                var helloMsg = new ZreMsg
                {
                    Id = ZreMsg.MessageId.Hello,
                    Hello = { Endpoint = "tcp://127.0.0.1:5552" }
                };
                var sendTask = Task<bool>.Factory.StartNew(() => peerMe.Send(helloMsg));
                var success = sendTask.Result;
                success.Should().BeTrue();
            }
            Assert.Fail("Not done yet.");
        }


    }
}
