using System;
using Xunit;
using System.Text;
using System.Threading;
using NetMQ.Sockets;

namespace NetMQ.Tests
{
    public class PeerTests : IClassFixture<CleanupAfterFixture>
    {
        [Fact]
        public void SendReceive()
        {
            using (var peer1 = new PeerSocket("@inproc://peertopeer"))
            using (var peer2 = new PeerSocket())
            {
                var peer1Identity = peer2.ConnectPeer("inproc://peertopeer");

                peer2.SendMoreFrame(peer1Identity);
                peer2.SendFrame("Hello");

                // peer2 identity
                var peer2Identity = peer1.ReceiveFrameBytes();
                var msg = peer1.ReceiveFrameString();

                Assert.Equal("Hello",msg);

                peer1.SendMoreFrame(peer2Identity);
                peer1.SendFrame("World");

                peer2.ReceiveFrameBytes();
                msg = peer2.ReceiveFrameString();

                Assert.Equal("World", msg);

                peer1.SendMoreFrame(peer2Identity);
                peer1.SendFrame("World2");

                peer2.ReceiveFrameBytes();
                msg = peer2.ReceiveFrameString();

                Assert.Equal("World2", msg);
            }
        }

        [Fact]
        public void CanSendAfterUnreachableException()
        {
            using (var peer1 = new PeerSocket())
            {
                var wrongKey = new byte[] { 0, 0, 0, 0 };

                Assert.Throws<HostUnreachableException>(() => peer1.SendMoreFrame(wrongKey));

                using (var peer2 = new PeerSocket("@inproc://peertopeer"))
                {
                    var peer2Identity = peer1.ConnectPeer("inproc://peertopeer");

                    peer1.SendMoreFrame(peer2Identity);
                    peer1.SendFrame("Hello");

                    peer2.ReceiveFrameBytes();
                    var msg = peer2.ReceiveFrameString();

                    Assert.Equal("Hello",msg);
                }
            }
        }

        [Fact]
        public void ExceptionWhenSendingToPeerWhichDoesnExist()
        {
            using (var peer1 = new PeerSocket("@inproc://peertopeer2"))
            {
                Assert.Throws<HostUnreachableException>(() =>
                {
                    peer1.SendMoreFrame("hello"); //unexist peer
                    peer1.SendFrame("World");
                });
            }
        }


        [Fact]
        public void DropMultipartMessages()
        {
            using (var peer1 = new PeerSocket("@inproc://peertopeer3"))
            using (var dealer = new DealerSocket(">inproc://peertopeer3"))
            {
                dealer.SendMoreFrame("This should be dropped");
                dealer.SendFrame("This as well");
                dealer.SendFrame("Hello");

                peer1.ReceiveFrameBytes();
                var message = peer1.ReceiveFrameString();

                Assert.Equal("Hello", message);
            }
        }

    }
}