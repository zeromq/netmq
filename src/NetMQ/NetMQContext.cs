using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ
{
	public partial class Factory : INetMQFactory
	{
		public INetMQContext CreateContext()
		{
			return NetMQContext.Create(this);
		}

		/// <summary>
		/// Context class of the NetMQ, you should have only one context in your application
		/// </summary>
		private class NetMQContext : INetMQContext
		{
			private readonly INetMQFactory m_factory;

			private readonly Ctx m_ctx;

			private int m_isClosed = 0;

			private NetMQContext(Ctx ctx, INetMQFactory factory)
			{
				m_factory = factory;
				m_ctx = ctx;
			}

			/// <summary>
			/// Create a new context
			/// </summary>
			/// <returns>The new context</returns>
			public static NetMQContext Create(INetMQFactory factory)
			{
				return new NetMQContext(new Ctx(), factory);
			}

			/// <summary>
			/// Gets the factory that is associated with this context.
			/// </summary>
			/// <value>
			/// The factory that created this context.
			/// </value>
			public INetMQFactory Factory
			{
				get
				{
					return m_factory;
				}
			}

			/// <summary>
			/// Number of IO Threads in the context, default is 1, 1 is good for most cases
			/// </summary>
			public int ThreadPoolSize
			{
				get
				{
					m_ctx.CheckDisposed();

					return m_ctx.Get(ContextOption.IOThreads);
				}

				set
				{
					m_ctx.CheckDisposed();

					m_ctx.Set(ContextOption.IOThreads, value);
				}
			}

			/// <summary>
			/// Maximum number of sockets
			/// </summary>
			public int MaxSockets
			{
				get
				{
					m_ctx.CheckDisposed();

					return m_ctx.Get(ContextOption.MaxSockets);
				}

				set
				{
					m_ctx.CheckDisposed();

					m_ctx.Set(ContextOption.MaxSockets, value);
				}
			}

			private SocketBase CreateHandle(ZmqSocketType socketType)
			{
				m_ctx.CheckDisposed();

				return m_ctx.CreateSocket(socketType);
			}

			public INetMQSocket CreateSocket(ZmqSocketType socketType)
			{
				var socketHandle = CreateHandle(socketType);

				switch (socketType)
				{
					case ZmqSocketType.Pair:
						return new PairSocket(socketHandle);
					case ZmqSocketType.Pub:
						return new PublisherSocket(socketHandle);
					case ZmqSocketType.Sub:
						return new SubscriberSocket(socketHandle);
					case ZmqSocketType.Req:
						return new RequestSocket(socketHandle);
					case ZmqSocketType.Rep:
						return new ResponseSocket(socketHandle);
					case ZmqSocketType.Dealer:
						return new DealerSocket(socketHandle);
					case ZmqSocketType.Router:
						return new RouterSocket(socketHandle);
					case ZmqSocketType.Pull:
						return new PullSocket(socketHandle);
					case ZmqSocketType.Push:
						return new PushSocket(socketHandle);
					case ZmqSocketType.Xpub:
						return new XPublisherSocket(socketHandle);
					case ZmqSocketType.Xsub:
						return new XSubscriberSocket(socketHandle);
					case ZmqSocketType.Stream:
						return new StreamSocket(socketHandle);
					default:
						throw new ArgumentOutOfRangeException("socketType");
				}
			}

			/// <summary>
			/// Create request socket
			/// </summary>
			/// <returns></returns>
			public IRequestSocket CreateRequestSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Req);

				return new RequestSocket(socketHandle);
			}

			/// <summary>
			/// Create response socket
			/// </summary>
			/// <returns></returns>
			public IResponseSocket CreateResponseSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Rep);

				return new ResponseSocket(socketHandle);
			}

			/// <summary>
			/// Create dealer socket
			/// </summary>
			/// <returns></returns>
			public IDealerSocket CreateDealerSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Dealer);

				return new DealerSocket(socketHandle);
			}

			/// <summary>
			/// Create router socket
			/// </summary>
			/// <returns></returns>
			public IRouterSocket CreateRouterSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Router);

				return new RouterSocket(socketHandle);
			}

			/// <summary>
			/// Create xpublisher socket
			/// </summary>
			/// <returns></returns>
			public XPublisherSocket CreateXPublisherSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Xpub);

				return new XPublisherSocket(socketHandle);
			}

			/// <summary>
			/// Create pair socket
			/// </summary>
			/// <returns></returns>
			public IPairSocket CreatePairSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Pair);

				return new PairSocket(socketHandle);
			}

			/// <summary>
			/// Create push socket
			/// </summary>
			/// <returns></returns>
			public IPushSocket CreatePushSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Push);

				return new PushSocket(socketHandle);
			}

			/// <summary>
			/// Create publisher socket
			/// </summary>
			/// <returns></returns>
			public IPublisherSocket CreatePublisherSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Pub);

				return new PublisherSocket(socketHandle);
			}

			/// <summary>
			/// Create pull socket
			/// </summary>
			/// <returns></returns>
			public IPullSocket CreatePullSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Pull);

				return new PullSocket(socketHandle);
			}

			/// <summary>
			/// Create subscriber socket
			/// </summary>
			/// <returns></returns>
			public ISubscriberSocket CreateSubscriberSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Sub);

				return m_factory.CreateSubscriberSocket(socketHandle);
			}

			/// <summary>
			/// Create xsub socket
			/// </summary>
			/// <returns></returns>
			public XSubscriberSocket CreateXSubscriberSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Xsub);

				return new XSubscriberSocket(socketHandle);
			}

			public IStreamSocket CreateStreamSocket()
			{
				var socketHandle = CreateHandle(ZmqSocketType.Stream);

				return new StreamSocket(socketHandle);
			}

			public INetMQMonitor CreateMonitorSocket(string endpoint)
			{
				if (endpoint == null)
				{
					throw new ArgumentNullException("endpoint");
				}

				if (endpoint == string.Empty)
				{
					throw new ArgumentException("Unable to monitor to an empty endpoint.", "endpoint");
				}

				return m_factory.CreateMonitor(CreatePairSocket(), endpoint);
			}

			/// <summary>
			/// Close the context
			/// </summary>
			public void Terminate()
			{
				if (Interlocked.CompareExchange(ref m_isClosed, 1, 0) == 0)
				{
					m_ctx.CheckDisposed();

					m_ctx.Terminate();
				}
			}

			public void Dispose()
			{
				Terminate();
			}
		}
	}
}