using System.Threading;
using System.Threading.Tasks;
using NetMQ.zmq;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ProxyTests
    {
        [Test]
        public void TestInfinitePolling()
        {
            using (var context = NetMQContext.Create())
            {
                using (var socket = context.CreateDealerSocket())
                {
                    socket.Bind("inproc://test");
                    var items = new PollItem[1];
                    items[0] = new PollItem(socket.SocketHandle, PollEvents.PollIn);

                    Task.Factory.StartNew(() =>
                    {
                        Thread.Sleep(500);
                        using (var monitor = context.CreateDealerSocket())
                        {
                            monitor.Connect("inproc://test");
                            monitor.Send("ping");
                        }
                    });

                    Assert.DoesNotThrow(() => ZMQ.Poll(items, -1));
                    Assert.AreEqual(socket.ReceiveString(SendReceiveOptions.DontWait), "ping");
                }
            }
        }

        [Test]
        public void TestProxySendAndReceive()
        {
            using (var ctx = NetMQContext.Create())
            {
                using (var front = ctx.CreateRouterSocket())
                {
                    front.Bind("inproc://frontend");

                    using (var back = ctx.CreateDealerSocket())
                    {
                        back.Bind("inproc://backend");

                        var proxy = new Proxy(front, back, null);
                        Task.Factory.StartNew(proxy.Start);

                        using (var client = ctx.CreateRequestSocket())
                        {
                            client.Connect("inproc://frontend");

                            using (var server = ctx.CreateResponseSocket())
                            {
                                server.Connect("inproc://backend");

                                client.Send("hello");
                                Assert.AreEqual("hello", server.ReceiveString());
                            }
                        }
                    }
                }
            }
        }
    }
}
