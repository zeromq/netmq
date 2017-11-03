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
            using (var peer1 = new PeerSocket("@inproc://peer"))
            using (var peer2 = new PeerSocket())
            {
                var peer1Identity = peer2.ConnectPeer("inproc://peer");

                peer2.SendMoreFrame(peer1Identity);
                peer2.SendFrame("Hello");

                // peer2 identity
                var peer2Identity = peer1.ReceiveFrameBytes();
                var msg = peer1.ReceiveFrameString();
                
                Assert.Equal(msg, "Hello");
                
                peer1.SendMoreFrame(peer2Identity);
                peer1.SendFrame("World");
                
                peer2.ReceiveFrameBytes();
                msg = peer2.ReceiveFrameString();
                
                Assert.Equal(msg, "World");
            }
        }

        [Fact]
        public void ExceptionWhenSendingToPeerWhichDoesnExist()
        {
            using (var peer1 = new PeerSocket("@inproc://peer"))
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
            using (var peer1 = new PeerSocket("@inproc://peer"))
            using (var dealer = new DealerSocket(">inproc://peer"))
            {
                dealer.SendMoreFrame("This should be dropped");
                dealer.SendFrame("This as well");
                dealer.SendFrame("Hello");

                peer1.ReceiveFrameBytes();
                var message = peer1.ReceiveFrameString();

                Assert.Equal(message, "Hello");
            }
        }
        
    }
}