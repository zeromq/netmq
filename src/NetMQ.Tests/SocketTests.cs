// Note: To target a version of .NET earlier than 4.0, build this with the pragma PRE_4 defined.  jh
using System;
using System.Threading;
#if !PRE_4
using System.Threading.Tasks;
#endif
using NUnit.Framework;
using NetMQ.zmq;

namespace NetMQ.Tests
{
	[TestFixture]
	public class SocketTests
	{
		[Test, ExpectedException(typeof(AgainException))]
		public void CheckRecvAgainException()
		{
            // Create a socket and bind it, and immediately call Receive - and verify that this results in a AgainException.
			using (NetMQContext context = NetMQContext.Create())
			{
				using (var routerSocket = context.CreateRouterSocket())
				{
					routerSocket.Bind("tcp://127.0.0.1:5555");

					routerSocket.Receive(SendReceiveOptions.DontWait);
				}
			}
		}

		[Test, ExpectedException(typeof(AgainException))]
		public void CheckSendAgainException()
		{
			using (NetMQContext context = NetMQContext.Create())
			{
				using (var routerSocket = context.CreateRouterSocket())
				{
					routerSocket.Bind("tcp://127.0.0.1:5555");
					routerSocket.Options.Linger = TimeSpan.Zero;

					using (var dealerSocket = context.CreateDealerSocket())
					{
						dealerSocket.Options.SendHighWatermark = 1;
						dealerSocket.Options.Linger = TimeSpan.Zero;
						dealerSocket.Connect("tcp://127.0.0.1:5555");

						dealerSocket.Send("1", true, false);
						dealerSocket.Send("2", true, false);
					}
				}
			}
		}

		[Test]
		public void LargeMessage()
		{
            // Send a 300-byte message and verify that it is received.
			using (NetMQContext context = NetMQContext.Create())
			{
				using (var pubSocket = context.CreatePublisherSocket())
				{
					pubSocket.Bind("tcp://127.0.0.1:5556");

					using (var subSocket = context.CreateSubscriberSocket())
					{
						subSocket.Connect("tcp://127.0.0.1:5556");						
						subSocket.Subscribe("");
					
						Thread.Sleep(100);

						byte[] msg = new byte[300];

						pubSocket.Send(msg);

						byte [] msg2 = subSocket.Receive();

						Assert.AreEqual(300, msg2.Length);
					}
				}
			}
		}

        [Test, Timeout(60000)]
		public void ReceiveMessageWithTimeout() 
		{
            // Check the operation of the timeout parameter on Receive,
            // by trying to Receive a message with a 10ms timeout, which should return too soon
            // to receive a message, and verifying that what was received is null,
            // and then trying to Receive a message with a longer timeout which should be sufficient
            // to receive the subsequent message, and verifying that that message was correctly received.
			using (var context = NetMQContext.Create()) 
			{

				var pubSync = new AutoResetEvent(false);
				var msg = new byte[300];
				var waitTime = 500;

#if !PRE_4
				var t1 = new Task(() => {					
#else
                var t1 = new Thread(_ => {
#endif
					var pubSocket = context.CreatePublisherSocket();
					pubSocket.Bind("tcp://127.0.0.1:5556");
					pubSync.WaitOne();
					Thread.Sleep(waitTime);
					pubSocket.Send(msg);
					pubSync.WaitOne();
					pubSocket.Dispose();
#if !PRE_4
				}, TaskCreationOptions.LongRunning);
#else
                }) { IsBackground = true, Name = "t1" };
#endif

#if !PRE_4
				var t2 = new Task(() => {
#else
                var t2 = new Thread(_ => {
#endif
					var subSocket = context.CreateSubscriberSocket();
					subSocket.Connect("tcp://127.0.0.1:5556");
					subSocket.Subscribe("");
					Thread.Sleep(100);
					pubSync.Set();

					var msg2 = subSocket.ReceiveMessage(TimeSpan.FromMilliseconds(10));
					Assert.IsNull(msg2, "The first receive should be null!");
					
					msg2 = subSocket.ReceiveMessage(TimeSpan.FromMilliseconds(waitTime));

					Assert.AreEqual(1, msg2.FrameCount);
					Assert.AreEqual(300, msg2.First.MessageSize);
					pubSync.Set();
					subSocket.Dispose();
#if !PRE_4
				}, TaskCreationOptions.LongRunning);
#else
                }) { IsBackground = true, Name = "t2" };
#endif
				
				t1.Start();
				t2.Start();

#if !PRE_4
				Task.WaitAll(new[] {t1, t2});
#else
                t1.Join();
                t2.Join();
#endif
			}	
		}

    [Test]
    public void LargeMessageLittleEndian()
    {
            // Check the operation of the socket Options.Endian property,
            // by setting a pub and a sub socket-pair both to little-endian
            // and verifying that sub receives a message that pub sends, and that it is the same length.
      using (NetMQContext context = NetMQContext.Create())
      {
        using (var pubSocket = context.CreatePublisherSocket())
        {
          pubSocket.Options.Endian = Endianness.Little;
          pubSocket.Bind("tcp://127.0.0.1:5556");

          using (var subSocket = context.CreateSubscriberSocket())
          {
            subSocket.Options.Endian = Endianness.Little;
            subSocket.Connect("tcp://127.0.0.1:5556");
            subSocket.Subscribe("");

            Thread.Sleep(100);

            byte[] msg = new byte[300];

            pubSocket.Send(msg);

            byte[] msg2 = subSocket.Receive();

            Assert.AreEqual(300, msg2.Length);
          }
        }
      }
    }

		[Test]
		public void TestKeepAlive()
		{
			// there is no way to test tcp keep alive without disconnect the cable, we just testing that is not crashing the system
			using (NetMQContext context = NetMQContext.Create())
			{
				using (var responseSocket = context.CreateResponseSocket())
				{
					responseSocket.Options.TcpKeepalive = true;
					responseSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
					responseSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

					responseSocket.Bind("tcp://127.0.0.1:5555");

					using (var requestSocket = context.CreateRequestSocket())
					{
						requestSocket.Options.TcpKeepalive = true;
						requestSocket.Options.TcpKeepaliveIdle = TimeSpan.FromSeconds(5);
						requestSocket.Options.TcpKeepaliveInterval = TimeSpan.FromSeconds(1);

						requestSocket.Connect("tcp://127.0.0.1:5555");

						requestSocket.Send("1");

						bool more;
						string m = responseSocket.ReceiveString(out more);

						Assert.IsFalse(more);
						Assert.AreEqual("1", m);

						responseSocket.Send("2");

						string m2 = requestSocket.ReceiveString(out more);

						Assert.IsFalse(more);
						Assert.AreEqual("2", m2);

						Assert.IsTrue(requestSocket.Options.TcpKeepalive);
						Assert.AreEqual(TimeSpan.FromSeconds(5), requestSocket.Options.TcpKeepaliveIdle);
						Assert.AreEqual(TimeSpan.FromSeconds(1), requestSocket.Options.TcpKeepaliveInterval);

						Assert.IsTrue(responseSocket.Options.TcpKeepalive);
						Assert.AreEqual(TimeSpan.FromSeconds(5), responseSocket.Options.TcpKeepaliveIdle);
						Assert.AreEqual(TimeSpan.FromSeconds(1), responseSocket.Options.TcpKeepaliveInterval);
					}
				}
			}
		}

	  [Test]
	  public void MultipleLargeMessages()
	  {
            // Check that a sub socket can receive multiple large messages from a pub socket,
            // by sending 100 messages, each of which is 12,000 bytes long,
            // and verifying that those exact messages are in fact received by the sub socket.
	    byte[] largeMessage = new byte[12000];

	    for (int i = 0; i < 12000; i++)
	    {
	      largeMessage[i] = (byte) (i%256);
	    }

	    using (NetMQContext context = NetMQContext.Create())
	    {
	      using (NetMQSocket pubSocket = context.CreatePublisherSocket())
	      {
          pubSocket.Bind("tcp://127.0.0.1:5558");

	        using (NetMQSocket subSocket = context.CreateSubscriberSocket())
	        {
            subSocket.Connect("tcp://127.0.0.1:5558");
            subSocket.Subscribe("");

            Thread.Sleep(1000);

            pubSocket.Send("");
	          subSocket.Receive();

	          for (int i = 0; i < 100; i++)
	          {
              pubSocket.Send(largeMessage);

	            byte[] recvMesage = subSocket.Receive();

              for (int j = 0; j < 12000; j++)
              {
                Assert.AreEqual(largeMessage[j], recvMesage[j]);                
              }
	          }
	        }
	      }
	    }
	  }
	}
}
