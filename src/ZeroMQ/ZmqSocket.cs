using zmq;

namespace ZeroMQ
{
	using System;
	using System.Diagnostics;
	using System.Threading;	

	/// <summary>
	/// Sends and receives messages across various transports to potentially multiple endpoints
	/// using the ZMQ protocol.
	/// </summary>
	public class ZmqSocket : IDisposable
	{
		/// <summary>
		/// The maximum buffer length when using the high performance Send/Receive methods (8192).
		/// </summary>        

		private readonly SocketBase _socketProxy;

		private bool _disposed;

		internal ZmqSocket(SocketBase socketProxy, SocketType socketType)
		{
			if (socketProxy == null)
			{
				throw new ArgumentNullException("socketProxy");
			}

			_socketProxy = socketProxy;
			SocketType = socketType;
		}

		/// <summary>
		/// Finalizes an instance of the <see cref="ZmqSocket"/> class.
		/// </summary>
		~ZmqSocket()
		{
			Dispose(false);
		}

		/// <summary>
		/// Occurs when at least one message may be received from the socket without blocking.
		/// </summary>
		public event EventHandler<SocketEventArgs> ReceiveReady;

		/// <summary>
		/// Occurs when at least one message may be sent via the socket without blocking.
		/// </summary>
		public event EventHandler<SocketEventArgs> SendReady;

		/// <summary>
		/// Gets the <see cref="ZeroMQ.SocketType"/> value for the current socket.
		/// </summary>
		public SocketType SocketType { get; private set; }

		/// <summary>
		/// Gets or sets the I/O thread affinity for newly created connections on this socket.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public ulong Affinity
		{
			get { return GetSocketOptionUInt64(SocketOption.AFFINITY); }
			set { SetSocketOption(SocketOption.AFFINITY, value); }
		}

		/// <summary>
		/// Gets or sets the maximum length of the queue of outstanding peer connections. (Default = 100 connections).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public int Backlog
		{
			get { return GetSocketOptionInt32(SocketOption.BACKLOG); }
			set { SetSocketOption(SocketOption.BACKLOG, value); }
		}

		/// <summary>
		/// Gets or sets the identity of the current socket.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public byte[] Identity
		{
			get { return GetSocketOptionBytes(SocketOption.IDENTITY); }
			set { SetSocketOption(SocketOption.IDENTITY, value); }
		}

		/// <summary>
		/// Gets or sets the linger period for socket shutdown. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public TimeSpan Linger
		{
			get { return TimeSpan.FromMilliseconds(GetSocketOptionInt32(SocketOption.LINGER)); }
			set { SetSocketOption(SocketOption.LINGER, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the maximum size for inbound messages (bytes). (Default = -1, no limit).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public long MaxMessageSize
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => GetSocketOptionInt64(SocketOption.MAX_MSG_SIZE)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.MAX_MSG_SIZE, value)); }
		}

		/// <summary>
		/// Gets or sets the time-to-live field in every multicast packet sent from this socket (network hops). (Default = 1 hop).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public int MulticastHops
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => GetSocketOptionInt32(SocketOption.MULTICAST_HOPS)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.MULTICAST_HOPS, value)); }
		}

		/// <summary>
		/// Gets or sets the maximum send or receive data rate for multicast transports (kbps). (Default = 100 kbps).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public int MulticastRate
		{
			get { return GetLegacySocketOption(SocketOption.RATE, GetSocketOptionInt64); }
			set { SetLegacySocketOption(SocketOption.RATE, value, (long)value, SetSocketOption); }
		}

		/// <summary>
		/// Gets or sets the recovery interval for multicast transports. (Default = 10 seconds).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public TimeSpan MulticastRecoveryInterval
		{
			get { return TimeSpan.FromMilliseconds(GetLegacySocketOption(SocketOption.RECOVERY_IVL, GetSocketOptionInt64)); }
			set { SetLegacySocketOption(SocketOption.RECOVERY_IVL, (int)value.TotalMilliseconds, (long)value.TotalMilliseconds, SetSocketOption); }
		}

		/// <summary>
		/// Gets or sets the underlying kernel receive buffer size for the current socket (bytes). (Default = 0, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public int ReceiveBufferSize
		{
			get { return GetLegacySocketOption(SocketOption.RCVBUF, GetSocketOptionUInt64); }
			set { SetLegacySocketOption(SocketOption.RCVBUF, value, (ulong)value, SetSocketOption); }
		}

		/// <summary>
		/// Gets or sets the high water mark for inbound messages (number of messages). (Default = 0, no limit).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>If using 0MQ 2.x, will use the (deprecated) HWM socket option instead.</remarks>
		public int ReceiveHighWatermark
		{
			get { return GetLegacySocketOption(ReceiveHwmOpt, GetSocketOptionUInt64); }
			set { SetLegacySocketOption(ReceiveHwmOpt, value, (ulong)value, SetSocketOption); }
		}

		/// <summary>
		/// Gets a value indicating whether the multi-part message currently being read has more message parts to follow.
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public bool ReceiveMore
		{
			get { return GetLegacySocketOption(SocketOption.RCVMORE, GetSocketOptionInt64) == 1; }
		}

		/// <summary>
		/// Gets or sets the timeout for receive operations. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public TimeSpan ReceiveTimeout
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => TimeSpan.FromMilliseconds(GetSocketOptionInt32(SocketOption.RCVTIMEO))); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.RCVTIMEO, (int)value.TotalMilliseconds)); }
		}

		/// <summary>
		/// Gets or sets the initial reconnection interval. (Default = 100 milliseconds).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public TimeSpan ReconnectInterval
		{
			get { return TimeSpan.FromMilliseconds(GetSocketOptionInt32(SocketOption.RECONNECT_IVL)); }
			set { SetSocketOption(SocketOption.RECONNECT_IVL, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the maximum reconnection interval. (Default = 0, only use <see cref="ReconnectInterval"/>).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public TimeSpan ReconnectIntervalMax
		{
			get { return TimeSpan.FromMilliseconds(GetSocketOptionInt32(SocketOption.RECONNECT_IVL_MAX)); }
			set { SetSocketOption(SocketOption.RECONNECT_IVL_MAX, (int)value.TotalMilliseconds); }
		}

		/// <summary>
		/// Gets or sets the underlying kernel transmit buffer size for the current socket (bytes). (Default = 0, OS default).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public int SendBufferSize
		{
			get { return GetLegacySocketOption(SocketOption.SNDBUF, GetSocketOptionUInt64); }
			set { SetLegacySocketOption(SocketOption.SNDBUF, value, (ulong)value, SetSocketOption); }
		}

		/// <summary>
		/// Gets or sets the high water mark for outbound messages (number of messages). (Default = 0, no limit).
		/// </summary>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>If using 0MQ 2.x, will use the (deprecated) HWM socket option instead.</remarks>
		public int SendHighWatermark
		{
			get { return GetLegacySocketOption(SendHwmOpt, GetSocketOptionUInt64); }
			set { SetLegacySocketOption(SendHwmOpt, value, (ulong)value, SetSocketOption); }
		}

		/// <summary>
		/// Gets or sets the timeout for send operations. (Default = <see cref="TimeSpan.MaxValue"/>, infinite).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public TimeSpan SendTimeout
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => TimeSpan.FromMilliseconds(GetSocketOptionInt32(SocketOption.SNDTIMEO))); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.SNDTIMEO, (int)value.TotalMilliseconds)); }
		}

		/// <summary>
		/// Gets or sets the supported socket protocol(s) when using TCP transports. (Default = <see cref="ProtocolType.Ipv4Only"/>).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public ProtocolType SupportedProtocol
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => (ProtocolType)GetSocketOptionInt32(SocketOption.IPV4_ONLY)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.IPV4_ONLY, (int)value)); }
		}

		/// <summary>
		/// Gets or sets the disk offload (swap) size for the specified socket. (Default = 0).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 3 or above.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 3.</remarks>
		[Obsolete("Not supported in 3.x. Will be removed when 0MQ 3.1 is stable.")]
		public long Swap
		{
			get { return ZmqVersion.OnlyIfAtMost(2, () => GetSocketOptionInt64(SocketOption.SWAP)); }
			set { ZmqVersion.OnlyIfAtMost(2, () => SetSocketOption(SocketOption.SWAP, value)); }
		}

		/// <summary>
		/// Gets the last endpoint bound for TCP and IPC transports.
		/// The returned value will be a string in the form of a ZMQ DSN.
		/// </summary>
		/// <remarks>
		/// Note that if the TCP host is INADDR_ANY, indicated by a *, then the
		/// returned address will be 0.0.0.0 (for IPv4).</remarks>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public string LastEndpoint
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => GetSocketOptionString(SocketOption.LAST_ENDPOINT)); }
		}

		/// <summary>
		/// Sets the behavior when an unroutable message is encountered. (Default = <see cref="ZeroMQ.RouterBehavior.Discard"/>).
		/// Only applicable to the <see cref="ZeroMQ.SocketType.ROUTER"/> socket type.
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public RouterBehavior RouterBehavior
		{
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.ROUTER_BEHAVIOR, (int)value)); }
		}

		/// <summary>
		/// Gets or sets the override value for the SO_KEEPALIVE TCP socket option. (where supported by OS). (Default = -1, OS default).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public TcpKeepaliveBehaviour TcpKeepalive
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => (TcpKeepaliveBehaviour)GetSocketOptionInt32(SocketOption.TCP_KEEPALIVE)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.TCP_KEEPALIVE, (int)value)); }
		}

		/// <summary>
		/// Gets or sets the override value for the 'TCP_KEEPCNT' socket option (where supported by OS). (Default = -1, OS default).
		/// The default value of '-1' means to skip any overrides and leave it to OS default.
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public int TcpKeepaliveCnt
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => GetSocketOptionInt32(SocketOption.TCP_KEEPALIVE_CNT)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.TCP_KEEPALIVE_CNT, value)); }
		}

		/// <summary>
		/// Gets or sets the override value for the TCP_KEEPCNT (or TCP_KEEPALIVE on some OS). (Default = -1, OS default).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public int TcpKeepaliveIdle
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => GetSocketOptionInt32(SocketOption.TCP_KEEPALIVE_IDLE)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.TCP_KEEPALIVE_IDLE, value)); }
		}

		/// <summary>
		/// Gets or sets the override value for the TCP_KEEPINTVL socket option (where supported by OS). (Default = -1, OS default).
		/// </summary>
		/// <exception cref="ZmqVersionException">This socket option was used in ZeroMQ 2.x or lower.</exception>
		/// <exception cref="ZmqSocketException">An error occurred when getting or setting the socket option.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <remarks>Not supported in 0MQ version 2.</remarks>
		public int TcpKeepaliveIntvl
		{
			get { return ZmqVersion.OnlyIfAtLeast(LatestVersion, () => GetSocketOptionInt32(SocketOption.TCP_KEEPALIVE_INTVL)); }
			set { ZmqVersion.OnlyIfAtLeast(LatestVersion, () => SetSocketOption(SocketOption.TCP_KEEPALIVE_INTVL, value)); }
		}

		/// <summary>
		/// Gets the status of the last Receive operation.
		/// </summary>
		public ReceiveStatus ReceiveStatus { get; private set; }

		/// <summary>
		/// Gets the status of the last Send operation.
		/// </summary>
		public SendStatus SendStatus { get; private set; }

		internal SocketBase SocketProxy
		{
			get { return _socketProxy; }
		}

		internal IntPtr SocketHandle
		{
			get { return _socketProxy.SocketHandle; }
		}

		/// <summary>
		/// Create an endpoint for accepting connections and bind it to the current socket.
		/// </summary>
		/// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred binding the socket to an endpoint.</exception>
		/// <exception cref="System.ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public void Bind(string endpoint)
		{
			EnsureNotDisposed();

			if (endpoint == null)
			{
				throw new ArgumentNullException("endpoint");
			}

			if (endpoint == string.Empty)
			{
				throw new ArgumentException("Unable to Bind to an empty endpoint.", "endpoint");
			}

			HandleProxyResult(_socketProxy.Bind(endpoint));
		}

		/// <summary>
		/// Stop accepting connections for a previously bound endpoint on the current socket.
		/// </summary>
		/// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred unbinding the socket to an endpoint.</exception>
		/// <exception cref="System.ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public void Unbind(string endpoint)
		{
			EnsureNotDisposed();

			if (endpoint == null)
			{
				throw new ArgumentNullException("endpoint");
			}

			if (endpoint == string.Empty)
			{
				throw new ArgumentException("Unable to Unbind to an empty endpoint.", "endpoint");
			}

			int rc = _socketProxy.Unbind(endpoint);

			// TODO: Silently fail if EAGAIN is thrown. As of 3.2.0-rc1, this appears to be returned
			// if endpoint contains an address that was not previously bound.
			if (rc == -1 && !ErrorProxy.ShouldTryAgain)
			{
				HandleProxyResult(rc);
			}
		}

		/// <summary>
		/// Connect the current socket to the specified endpoint.
		/// </summary>
		/// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred connecting the socket to a remote endpoint.</exception>
		/// <exception cref="System.ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public void Connect(string endpoint)
		{
			EnsureNotDisposed();

			if (endpoint == null)
			{
				throw new ArgumentNullException("endpoint");
			}

			if (endpoint == string.Empty)
			{
				throw new ArgumentException("Unable to Connect to an empty endpoint.", "endpoint");
			}

			HandleProxyResult(_socketProxy.Connect(endpoint));
		}

		/// <summary>
		/// Disconnect the current socket from a previously connected endpoint.
		/// </summary>
		/// <param name="endpoint">A string consisting of a transport and an address, formatted as <c><em>transport</em>://<em>address</em></c>.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred disconnecting the socket from a remote endpoint.</exception>
		/// <exception cref="System.ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public void Disconnect(string endpoint)
		{
			EnsureNotDisposed();

			if (endpoint == null)
			{
				throw new ArgumentNullException("endpoint");
			}

			if (endpoint == string.Empty)
			{
				throw new ArgumentException("Unable to Disconnect to an empty endpoint.", "endpoint");
			}

			int rc = _socketProxy.Disconnect(endpoint);

			// TODO: Silently fail if EAGAIN is thrown. As of 3.2.0-rc1, this appears to be returned
			// if endpoint contains an address that was not previously connected.
			if (rc == -1 && !ErrorProxy.ShouldTryAgain)
			{
				HandleProxyResult(rc);
			}
		}

		/// <summary>
		/// Destroy the current socket.
		/// </summary>
		/// <remarks>
		/// Any outstanding messages physically received from the network but not yet received by the application
		/// with Receive shall be discarded. The behaviour for discarding messages sent by the application
		/// with Send but not yet physically transferred to the network depends on the value of
		/// the <see cref="Linger"/> socket option.
		/// </remarks>
		/// <exception cref="ZmqSocketException">The underlying socket object is not valid.</exception>
		public void Close()
		{
			HandleProxyResult(_socketProxy.Close());
		}

		/// <summary>
		/// Receive a single message-part from a remote socket in blocking mode (high performance).
		/// </summary>
		/// <remarks>
		/// Warning: This overload will only receive as much data as can fit in the supplied <paramref name="buffer"/>
		/// Up to <see cref="MaxMessageSize"/>. Any message data beyond the maximum length will be discarded.
		/// It is intended to be used when the maximum messaging performance is required; it will not allocate a new
		/// buffer (or copy received data) if the received message exceeds the current buffer size.
		/// If the maximum message size is not known in advance, use the <see cref="Receive(byte[],out int)"/> overload.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that will store the received data.</param>
		/// <returns>The number of bytes contained in the resulting message.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
		public int Receive(byte[] buffer)
		{
			return Receive(buffer, SocketFlags.None);
		}

		/// <summary>
		/// Receive a single message-part from a remote socket in non-blocking mode with a specified timeout (high performance).
		/// </summary>
		/// <remarks>
		/// Warning: This overload will only receive as much data as can fit in the supplied <paramref name="buffer"/>
		/// up to <see cref="MaxMessageSize"/>. Any message data beyond the maximum length will be discarded.
		/// It is intended to be used when the maximum messaging performance is required, as it does not perform
		/// any unnecessary memory allocation, copying or marshalling.
		/// If the maximum message size is not known in advance, use the <see cref="Receive(byte[],System.TimeSpan,out int)"/> overload.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that will store the received data.</param>
		/// <param name="timeout">A <see cref="TimeSpan"/> specifying the receive timeout.</param>
		/// <returns>
		/// The number of bytes contained in the resulting message or -1 if the timeout expired or an interrupt occurred.
		/// See <see cref="ReceiveStatus"/> for details.
		/// </returns>
		/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
		public int Receive(byte[] buffer, TimeSpan timeout)
		{
			return timeout == TimeSpan.MaxValue
								 ? Receive(buffer)
								 : ExecuteWithTimeout(() => Receive(buffer, SocketFlags.DontWait), timeout);
		}

		/// <summary>
		/// Receive a single message-part from a remote socket (high performance).
		/// </summary>
		/// <remarks>
		/// Warning: This overload will only receive as much data as can fit in the supplied <paramref name="buffer"/>
		/// up to <see cref="MaxMessageSize"/>. Any message data beyond the maximum length will be discarded.
		/// It is intended to be used when the maximum messaging performance is required; it will not allocate a new
		/// buffer (or copy received data) if the received message exceeds the current buffer size.
		/// If the maximum message size is not known in advance, use the <see cref="Receive(byte[],out int)"/> overload.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that will store the received data.</param>
		/// <param name="flags">A combination of <see cref="SocketFlags"/> values to use when receiving.</param>
		/// <returns>The number of bytes contained in the resulting message.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
		/// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
		public virtual int Receive(byte[] buffer, SocketFlags flags)
		{
			EnsureNotDisposed();

			if (buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}

			int receivedBytes = _socketProxy.Receive(buffer, (int)flags);

			if (receivedBytes >= 0)
			{
				ReceiveStatus = ReceiveStatus.Received;
				return receivedBytes;
			}

			if (ErrorProxy.ShouldTryAgain)
			{
				ReceiveStatus = ReceiveStatus.TryAgain;
				return -1;
			}

			if (ErrorProxy.ContextWasTerminated)
			{
				ReceiveStatus = ReceiveStatus.Interrupted;
				return -1;
			}

			throw new ZmqSocketException(ErrorProxy.GetLastError());
		}

		/// <summary>
		/// Receive a single message-part from a remote socket in blocking mode.
		/// </summary>
		/// <remarks>
		/// This overload will receive all available data in the message-part. If the size of <paramref name="buffer"/>
		/// is insufficient, a new buffer will be allocated.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that may store the received data.</param>
		/// <param name="size">An <see cref="int"/> that will contain the number of bytes in the received data.</param>
		/// <returns>
		/// A <see cref="byte"/> array containing the data received from the remote endpoint, which may or may
		/// not be the supplied <paramref name="buffer"/>.
		/// </returns>
		/// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
		public byte[] Receive(byte[] buffer, out int size)
		{
			return Receive(buffer, SocketFlags.None, out size);
		}

		/// <summary>
		/// Receive a single message-part from a remote socket in non-blocking mode with a specified timeout.
		/// </summary>
		/// <remarks>
		/// This overload will receive all available data in the message-part. If the size of <paramref name="buffer"/>
		/// is insufficient, a new buffer will be allocated.
		/// </remarks>
		/// <param name="buffer">A <see cref="Frame"/> that will store the received data.</param>
		/// <param name="timeout">A <see cref="TimeSpan"/> specifying the receive timeout.</param>
		/// <param name="size">An <see cref="int"/> that will contain the number of bytes in the received data.</param>
		/// <returns>
		/// A <see cref="byte"/> array containing the data received from the remote endpoint, which may or may
		/// not be the supplied <paramref name="buffer"/>.
		/// </returns>
		/// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
		public byte[] Receive(byte[] buffer, TimeSpan timeout, out int size)
		{
			if (timeout == TimeSpan.MaxValue)
			{
				return Receive(buffer, out size);
			}

			int receivedBytes = -1;

			byte[] message = ExecuteWithTimeout(() => Receive(buffer, SocketFlags.DontWait, out receivedBytes), timeout);
			size = receivedBytes;

			return message;
		}

		/// <summary>
		/// Receive a single message-part from a remote socket.
		/// </summary>
		/// <remarks>
		/// This overload will receive all available data in the message-part. If the size of <paramref name="buffer"/>
		/// is insufficient, a new buffer will be allocated.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that may store the received data.</param>
		/// <param name="flags">A combination of <see cref="SocketFlags"/> values to use when sending.</param>
		/// <param name="size">An <see cref="int"/> that will contain the number of bytes in the received data.</param>
		/// <returns>
		/// A <see cref="byte"/> array containing the data received from the remote endpoint, which may or may
		/// not be the supplied <paramref name="buffer"/>.
		/// </returns>
		/// <exception cref="ZmqSocketException">An error occurred receiving data from a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Receive operations.</exception>
		public virtual byte[] Receive(byte[] buffer, SocketFlags flags, out int size)
		{
			EnsureNotDisposed();

			if (buffer == null)
			{
				buffer = new byte[0];
			}

			buffer = _socketProxy.Receive(buffer, (int)flags, out size);

			if (size >= 0)
			{
				ReceiveStatus = ReceiveStatus.Received;
				return buffer;
			}

			if (ErrorProxy.ShouldTryAgain)
			{
				ReceiveStatus = ReceiveStatus.TryAgain;
				return buffer;
			}

			if (ErrorProxy.ContextWasTerminated)
			{
				ReceiveStatus = ReceiveStatus.Interrupted;
				return buffer;
			}

			throw new ZmqSocketException(ErrorProxy.GetLastError());
		}

		/// <summary>
		/// Queue a message buffer to be sent by the socket in blocking mode.
		/// </summary>
		/// <remarks>
		/// Performance tip: To increase send performance, especially on low-powered devices, restrict the
		/// size of <paramref name="buffer"/> to <see cref="MaxBufferSize"/>. This will reduce the number of
		/// P/Invoke calls required to send the message buffer.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
		/// <param name="size">The size of the message to send.</param>
		/// <param name="flags">A combination of <see cref="SocketFlags"/> values to use when sending.</param>
		/// <returns>The number of bytes sent by the socket.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is a negative value or is larger than the length of <paramref name="buffer"/>.</exception>
		/// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
		public virtual int Send(byte[] buffer, int size, SocketFlags flags)
		{
			EnsureNotDisposed();

			if (buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}

			if (size < 0 || size > buffer.Length)
			{
				throw new ArgumentOutOfRangeException("size", "Expected a non-negative value less than or equal to the buffer length.");
			}

			int sentBytes = _socketProxy.Send(buffer, size, (int)flags);

			if (sentBytes >= 0)
			{
				SendStatus = (sentBytes == size || LibZmq.MajorVersion < LatestVersion) ? SendStatus.Sent : SendStatus.Incomplete;
				return sentBytes;
			}

			if (ErrorProxy.ShouldTryAgain)
			{
				SendStatus = SendStatus.TryAgain;
				return -1;
			}

			if (ErrorProxy.ContextWasTerminated)
			{
				SendStatus = SendStatus.Interrupted;
				return -1;
			}

			throw new ZmqSocketException(ErrorProxy.GetLastError());
		}

		/// <summary>
		/// Queue a message buffer to be sent by the socket in non-blocking mode with a specified timeout.
		/// </summary>
		/// <remarks>
		/// Performance tip: To increase send performance, especially on low-powered devices, restrict the
		/// size of <paramref name="buffer"/> to <see cref="MaxBufferSize"/>. This will reduce the number of
		/// P/Invoke calls required to send the message buffer.
		/// </remarks>
		/// <param name="buffer">A <see cref="byte"/> array that contains the message to be sent.</param>
		/// <param name="size">The size of the message to send.</param>
		/// <param name="flags">A combination of <see cref="SocketFlags"/> values to use when sending.</param>
		/// <param name="timeout">A <see cref="TimeSpan"/> specifying the send timeout.</param>
		/// <returns>The number of bytes sent by the socket.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="size"/> is a negative value or is larger than the length of <paramref name="buffer"/>.
		/// </exception>
		/// <exception cref="ZmqSocketException">An error occurred sending data to a remote endpoint.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support Send operations.</exception>
		public int Send(byte[] buffer, int size, SocketFlags flags, TimeSpan timeout)
		{
			return timeout == TimeSpan.MaxValue
								 ? Send(buffer, size, flags & ~SocketFlags.DontWait)
								 : ExecuteWithTimeout(() => Send(buffer, size, flags | SocketFlags.DontWait), timeout);
		}

		/// <summary>
		/// Forwards a single-part or all parts of a multi-part message to a destination socket.
		/// </summary>
		/// <remarks>
		/// This method is useful for implementing devices as data is not marshalled into managed code; it
		/// is forwarded directly in the unmanaged layer. As an example, this method could forward all traffic
		/// from a device's front-end socket to its backend socket.
		/// </remarks>
		/// <param name="destination">A <see cref="ZmqSocket"/> that will receive the incoming message(s).</param>
		public void Forward(ZmqSocket destination)
		{
			if (destination == null)
			{
				throw new ArgumentNullException("destination");
			}

			if (_socketProxy.Forward(destination.SocketHandle) == -1)
			{
				throw new ZmqSocketException(ErrorProxy.GetLastError());
			}
		}

		/// <summary>
		/// Subscribe to all messages.
		/// </summary>
		/// <remarks>
		/// Only applies to <see cref="ZeroMQ.SocketType.SUB"/> and <see cref="ZeroMQ.SocketType.XSUB"/> sockets.
		/// </remarks>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
		public void SubscribeAll()
		{
			Subscribe(new byte[0]);
		}

		/// <summary>
		/// Subscribe to messages that begin with a specified prefix.
		/// </summary>
		/// <remarks>
		/// Only applies to <see cref="ZeroMQ.SocketType.SUB"/> and <see cref="ZeroMQ.SocketType.XSUB"/> sockets.
		/// </remarks>
		/// <param name="prefix">Prefix for subscribed messages.</param>
		/// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
		public virtual void Subscribe(byte[] prefix)
		{
			SetSocketOption(SocketOption.SUBSCRIBE, prefix);
		}

		/// <summary>
		/// Unsubscribe from all messages.
		/// </summary>
		/// <remarks>
		/// Only applies to <see cref="ZeroMQ.SocketType.SUB"/> and <see cref="ZeroMQ.SocketType.XSUB"/> sockets.
		/// </remarks>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
		public void UnsubscribeAll()
		{
			Unsubscribe(new byte[0]);
		}

		/// <summary>
		/// Unsubscribe from messages that begin with a specified prefix.
		/// </summary>
		/// <remarks>
		/// Only applies to <see cref="ZeroMQ.SocketType.SUB"/> and <see cref="ZeroMQ.SocketType.XSUB"/> sockets.
		/// </remarks>
		/// <param name="prefix">Prefix for subscribed messages.</param>
		/// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		/// <exception cref="NotSupportedException">The current socket type does not support subscriptions.</exception>
		public virtual void Unsubscribe(byte[] prefix)
		{
			SetSocketOption(SocketOption.UNSUBSCRIBE, prefix);
		}

		/// <summary>
		/// Add a filter that will be applied for each new TCP transport connection on a listening socket.
		/// Example: "127.0.0.1", "mail.ru/24", "::1", "::1/128", "3ffe:1::", "3ffe:1::/56"
		/// </summary>
		/// <seealso cref="ClearTcpAcceptFilter"/>
		/// <remarks>
		/// If no filters are applied, then TCP transport allows connections from any IP. If at least one
		/// filter is applied then new connection source IP should be matched.
		/// </remarks>
		/// <param name="filter">IPV6 or IPV4 CIDR filter.</param>
		/// <exception cref="ArgumentNullException"><paramref name="filter"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="filter"/> is empty string.</exception>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public void AddTcpAcceptFilter(string filter)
		{
			if (filter == null)
			{
				throw new ArgumentNullException("filter");
			}

			if (filter == string.Empty)
			{
				throw new ArgumentException("Unable to add empty filter", "filter");
			}

			SetSocketOption(SocketOption.TCP_ACCEPT_FILTER, filter);
		}

		/// <summary>
		/// Reset all TCP filters assigned by <see cref="AddTcpAcceptFilter"/> and allow TCP transport to accept connections from any IP.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The <see cref="ZmqSocket"/> has been closed.</exception>
		public void ClearTcpAcceptFilter()
		{
			SetSocketOption(SocketOption.TCP_ACCEPT_FILTER, (string)null);
		}

		/// <summary>
		/// Releases all resources used by the current instance of the <see cref="ZmqSocket"/> class.
		/// </summary>
		public virtual void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		internal static void HandleProxyResult(int result)
		{
			// Context termination (ETERM) is an allowable error state, occurring when the
			// ZmqContext was terminated during a socket method.
			if (result == -1 && zmq.ZError.ErrorNumber != ErrorNumber.ETERM)
			{
				throw new ZmqSocketException(ZError.ErrorNumber.Value, ZMQ.ErrorText(ZError.ErrorNumber));
			}
		}

		internal int GetSocketOptionInt32(SocketOption option)
		{
			EnsureNotDisposed();

			int value = ZMQ.GetSocketOption(_socketProxy, (ZmqSocketOptions)option);

			//HandleProxyResult();

			return value;
		}

		internal long GetSocketOptionInt64(SocketOption option)
		{
			EnsureNotDisposed();

			long value = (long)ZMQ.GetSocketOptionX(_socketProxy, (ZmqSocketOptions)option);

			//HandleProxyResult(_socketProxy.GetSocketOption((int)option, out value));

			return value;
		}

		internal ulong GetSocketOptionUInt64(SocketOption option)
		{
			EnsureNotDisposed();

			ulong value = (ulong)ZMQ.GetSocketOptionX(_socketProxy, (ZmqSocketOptions)option);

			//HandleProxyResult(_socketProxy.GetSocketOption((int)option, out value));

			return value;
		}

		internal byte[] GetSocketOptionBytes(SocketOption option)
		{
			EnsureNotDisposed();

			byte[] value =  (byte[])ZMQ.GetSocketOptionX(_socketProxy, (ZmqSocketOptions)option);

			//HandleProxyResult(_socketProxy.GetSocketOption((int)option, out value));

			return value;
		}

		internal string GetSocketOptionString(SocketOption option)
		{
			EnsureNotDisposed();

			string value = (string)ZMQ.GetSocketOptionX(_socketProxy, (ZmqSocketOptions)option);

			//HandleProxyResult(_socketProxy.GetSocketOption((int)option, out value));

			return value;
		}

		internal void SetSocketOption(SocketOption option, string value)
		{
			EnsureNotDisposed();

			ZMQ.SetSocketOption(_socketProxy, (ZmqSocketOptions)option, value);			

			//HandleProxyResult(_socketProxy.SetSocketOption((int)option, value));
		}

		internal void SetSocketOption(SocketOption option, int value)
		{
			EnsureNotDisposed();

			ZMQ.SetSocketOption(_socketProxy, (ZmqSocketOptions)option, value);			

			//HandleProxyResult(_socketProxy.SetSocketOption((int)option, value));
		}

		internal void SetSocketOption(SocketOption option, long value)
		{
			EnsureNotDisposed();

			ZMQ.SetSocketOption(_socketProxy, (ZmqSocketOptions)option, value);			

			//HandleProxyResult(_socketProxy.SetSocketOption((int)option, value));
		}

		internal void SetSocketOption(SocketOption option, ulong value)
		{
			EnsureNotDisposed();

			ZMQ.SetSocketOption(_socketProxy, (ZmqSocketOptions)option, value);			

			//HandleProxyResult(_socketProxy.SetSocketOption((int)option, value));
		}

		internal void SetSocketOption(SocketOption option, byte[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}

			EnsureNotDisposed();

			ZMQ.SetSocketOption(_socketProxy, (ZmqSocketOptions)option, value);			

			//HandleProxyResult(_socketProxy.SetSocketOption((int)option, value));
		}

		internal void InvokePollEvents(PollEvents readyEvents)
		{
			if (readyEvents.HasFlag(PollEvents.PollIn))
			{
				InvokeReceiveReady(readyEvents);
			}

			if (readyEvents.HasFlag(PollEvents.PollOut))
			{
				InvokeSendReady(readyEvents);
			}
		}

		internal PollEvents GetPollEvents()
		{
			var events = PollEvents.None;

			if (ReceiveReady != null)
			{
				events |= PollEvents.PollIn;
			}

			if (SendReady != null)
			{
				events |= PollEvents.PollOut;
			}

			return events;
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ZmqSocket"/>, and optionally disposes of the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					ZMQ.Close(_socketProxy);
				}
			}

			_disposed = true;
		}

		private int GetLegacySocketOption<TLegacy>(SocketOption option, Func<SocketOption, TLegacy> legacyGetter)
		{
			return ZmqVersion.Current.IsAtLeast(LatestVersion) ? GetSocketOptionInt32(option) : Convert.ToInt32(legacyGetter(option));
		}

		private void SetLegacySocketOption<TLegacy>(SocketOption option, int value, TLegacy legacyValue, Action<SocketOption, TLegacy> legacySetter)
		{
			if (ZmqVersion.Current.IsAtLeast(LatestVersion))
			{
				SetSocketOption(option, value);
			}
			else
			{
				legacySetter(option, legacyValue);
			}
		}

		private TResult ExecuteWithTimeout<TResult>(Func<TResult> method, TimeSpan timeout)
		{
			if ((int)timeout.TotalMilliseconds < 1)
			{
				return method();
			}

			TResult receiveResult;

			var timer = Stopwatch.StartNew();
			var spin = new SpinWait();

			do
			{
				receiveResult = method();

				if (ReceiveStatus != ReceiveStatus.TryAgain)
				{
					break;
				}

				spin.SpinOnce();
			}
			while (timer.Elapsed <= timeout);

			return receiveResult;
		}

		private void InvokeReceiveReady(PollEvents readyEvents)
		{
			EventHandler<SocketEventArgs> handler = ReceiveReady;
			if (handler != null)
			{
				handler(this, new SocketEventArgs(this, readyEvents));
			}
		}

		private void InvokeSendReady(PollEvents readyEvents)
		{
			EventHandler<SocketEventArgs> handler = SendReady;
			if (handler != null)
			{
				handler(this, new SocketEventArgs(this, readyEvents));
			}
		}

		private void EnsureNotDisposed()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}
		}
	}
}
