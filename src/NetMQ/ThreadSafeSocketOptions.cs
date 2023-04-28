using System;
using System.Diagnostics.CodeAnalysis;
using NetMQ.Core;

namespace NetMQ
{
    /// <summary>
    /// A ThreadSafeSocketOptions is simply a convenient way to access the options of a particular thread-safe socket.
    /// This class holds a reference to the socket, and its properties provide a concise way
    /// to access that socket's option values -- instead of calling GetSocketOption/SetSocketOption.
    /// </summary>
    public class ThreadSafeSocketOptions
    {
        /// <summary>
        /// The NetMQSocket that this SocketOptions is referencing.
        /// </summary>
        private readonly SocketBase m_socket;

        /// <summary>
        /// Create a new SocketOptions that references the given NetMQSocket.
        /// </summary>
        /// <param name="socket">the NetMQSocket for this SocketOptions to hold a reference to</param>
        internal ThreadSafeSocketOptions(SocketBase socket)
        {
            m_socket = socket;
        }
        
        /// <summary>
        /// Assign the given integer value to the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="value">an integer that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal void SetSocketOption(ZmqSocketOption option, int value)
        {
            m_socket.CheckDisposed();
            m_socket.SetSocketOption(option, value);
        }
        
        /// <summary>
        /// Assign the given TimeSpan to the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="value">a TimeSpan that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal void SetSocketOptionTimeSpan(ZmqSocketOption option, TimeSpan value)
        {
            SetSocketOption(option, (int)value.TotalMilliseconds);
        }
        
        /// <summary>
        /// Get the integer-value of the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>an integer that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal int GetSocketOption(ZmqSocketOption option)
        {
            m_socket.CheckDisposed();
            return m_socket.GetSocketOption(option);
        }

        /// <summary>
        /// Get the (generically-typed) value of the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>an object of the given type, that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal T? GetSocketOptionX<T>(ZmqSocketOption option)
        {
            m_socket.CheckDisposed();
            return (T?)m_socket.GetSocketOptionX(option);
        }

        /// <summary>
        /// Get the <see cref="TimeSpan"/> value of the specified ZmqSocketOption.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>a TimeSpan that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal TimeSpan GetSocketOptionTimeSpan(ZmqSocketOption option)
        {
            return TimeSpan.FromMilliseconds(GetSocketOption(option));
        }

        /// <summary>
        /// Get the 64-bit integer-value of the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>a long that is the value of that option</returns>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        internal long GetSocketOptionLong(ZmqSocketOption option)
        {
            return GetSocketOptionX<long>(option);
        }

        /// <summary>
        /// Assign the given Object value to the specified <see cref="ZmqSocketOption"/>.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="value">an object that is the value to set that option to</param>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
        internal void SetSocketOption(ZmqSocketOption option, object value)
        {
            m_socket.CheckDisposed();
            m_socket.SetSocketOption(option, value);
        }

        /// <summary>
        /// Get or set the I/O-thread affinity. This is a 64-bit value used to specify  which threads from the I/O thread-pool
        /// associated with the socket's context shall handle newly-created connections.
        /// 0 means no affinity, meaning that work shall be distributed fairly among all I/O threads.
        /// For non-zero values, the lowest bit corresponds to thread 1, second lowest bit to thread 2, and so on.
        /// </summary>
        public long Affinity
        {
            get => GetSocketOptionLong(ZmqSocketOption.Affinity);
            set => SetSocketOption(ZmqSocketOption.Affinity, value);
        }

        /// <summary>
        /// Get or set the size of the transmit buffer for the specified socket.
        /// </summary>
        public int SendBuffer
        {
            get => GetSocketOption(ZmqSocketOption.SendBuffer);
            set => SetSocketOption(ZmqSocketOption.SendBuffer, value);
        }

        /// <summary>
        /// Get or set the size of the receive buffer for the specified socket.
        /// A value of zero means that the OS default is in effect.
        /// </summary>
        public int ReceiveBuffer
        {
            get => GetSocketOption(ZmqSocketOption.ReceiveBuffer);
            set => SetSocketOption(ZmqSocketOption.ReceiveBuffer, value);
        }

        /// <summary>
        /// Get or set the linger period for the specified socket,
        /// which determines how long pending messages which have yet to be sent to a peer
        /// shall linger in memory after a socket is closed.
        /// </summary>
        /// <remarks>
        /// If socket created with Context default is -1 if socket created without socket (using new keyword) default is zero.
        /// If context is used this also affects the termination of context, otherwise this affects the exit of the process.
        /// -1: Specifies an infinite linger period. Pending messages shall not be discarded after the socket is closed;
        /// attempting to terminate the socket's context shall block until all pending messages have been sent to a peer.
        /// 0: Specifies no linger period. Pending messages shall be discarded immediately when the socket is closed.
        /// Positive values specify an upper bound for the linger period. Pending messages shall not be discarded after the socket is closed;
        /// attempting to terminate the socket's context shall block until either all pending messages have been sent to a peer,
        /// or the linger period expires, after which any pending messages shall be discarded.
        /// </remarks>
        public TimeSpan Linger
        {
            get => GetSocketOptionTimeSpan(ZmqSocketOption.Linger);
            set => SetSocketOptionTimeSpan(ZmqSocketOption.Linger, value);
        }

        /// <summary>
        /// Get or set the initial reconnection interval for the specified socket.
        /// This is the period to wait between attempts to reconnect disconnected peers
        /// when using connection-oriented transports. The default is 100 ms.
        /// -1 means no reconnection.
        /// </summary>
        /// <remarks>
        /// With ZeroMQ, the reconnection interval may be randomized to prevent reconnection storms
        /// in topologies with a large number of peers per socket.
        /// </remarks>
        public TimeSpan ReconnectInterval
        {
            get => GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl);
            set => SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl, value);
        }

        /// <summary>
        /// Get or set the maximum reconnection interval for the specified socket.
        /// This is the maximum period to shall wait between attempts
        /// to reconnect. On each reconnect attempt, the previous interval shall be doubled
        /// until this maximum period is reached.
        /// The default value of zero means no exponential backoff is performed.
        /// </summary>
        /// <remarks>
        /// This is the maximum period NetMQ shall wait between attempts
        /// to reconnect. On each reconnect attempt, the previous interval shall be doubled
        /// until this maximum period is reached.
        /// This allows for an exponential backoff strategy.
        /// The default value of zero means no exponential backoff is performed
        /// and reconnect interval calculations are only based on ReconnectIvl.
        /// </remarks>
        public TimeSpan ReconnectIntervalMax
        {
            get => GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvlMax);
            set => SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvlMax, value);
        }

        /// <summary>
        /// Get or set the maximum length of the queue of outstanding peer connections
        /// for the specified socket. This only applies to connection-oriented transports.
        /// Default is 100.
        /// </summary>
        public int Backlog
        {
            get => GetSocketOption(ZmqSocketOption.Backlog);
            set => SetSocketOption(ZmqSocketOption.Backlog, value);
        }

        /// <summary>
        /// Get or set the upper limit to the size for inbound messages.
        /// If a peer sends a message larger than this it is disconnected.
        /// The default value is -1, which means no limit.
        /// </summary>
        public long MaxMsgSize
        {
            get => GetSocketOptionLong(ZmqSocketOption.MaxMessageSize);
            set => SetSocketOption(ZmqSocketOption.MaxMessageSize, value);
        }

        /// <summary>
        /// Get or set the high-water-mark for transmission.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int SendHighWatermark
        {
            get => GetSocketOption(ZmqSocketOption.SendHighWatermark);
            set => SetSocketOption(ZmqSocketOption.SendHighWatermark, value);
        }

        /// <summary>
        /// Get or set the high-water-mark for reception.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int ReceiveHighWatermark
        {
            get => GetSocketOption(ZmqSocketOption.ReceiveHighWatermark);
            set => SetSocketOption(ZmqSocketOption.ReceiveHighWatermark, value);
        }

        /// <summary>
        /// The low-water mark for message transmission.
        /// This is the number of messages that should be processed before transmission is
        /// unblocked (in case it was blocked by reaching high-watermark). The default value is
        /// calculated using relevant high-watermark (HWM): HWM > 2048 ? HWM - 1024 : (HWM + 1) / 2
        /// </summary>
        public int SendLowWatermark
        {
            get => GetSocketOption(ZmqSocketOption.SendLowWatermark);
            set => SetSocketOption(ZmqSocketOption.SendLowWatermark, value);
        }

        /// <summary>
        /// The low-water mark for message reception.
        /// This is the number of messages that should be processed  before reception is
        /// unblocked (in case it was blocked by reaching high-watermark). The default value is
        /// calculated using relevant high-watermark (HWM): HWM > 2048 ? HWM - 1024 : (HWM + 1) / 2
        /// </summary>
        public int ReceiveLowWatermark
        {
            get => GetSocketOption(ZmqSocketOption.ReceiveLowWatermark);
            set => SetSocketOption(ZmqSocketOption.ReceiveLowWatermark, value);
        }

        /// <summary>
        /// Get or set whether the underlying socket is for IPv4 only (not IPv6),
        /// as opposed to one that allows connections with either IPv4 or IPv6.
        /// </summary>
        public bool IPv4Only
        {
            get => GetSocketOptionX<bool>(ZmqSocketOption.IPv4Only);
            set => SetSocketOption(ZmqSocketOption.IPv4Only, value);
        }

        /// <summary>
        /// Get the last endpoint bound for TCP and IPC transports.
        /// The returned value will be a string in the form of a ZMQ DSN.
        /// </summary>
        /// <remarks>
        /// If the TCP host is ANY, indicated by a *, then the returned address
        /// will be 0.0.0.0 (for IPv4).
        /// </remarks>
        public string? LastEndpoint => GetSocketOptionX<string>(ZmqSocketOption.LastEndpoint);
        
        /// <summary>
        /// Get or set the attach-on-connect value.
        /// If set to true, this will delay the attachment of a pipe on connect until
        /// the underlying connection has completed. This will cause the socket
        /// to block if there are no other connections, but will prevent queues
        /// from filling on pipes awaiting connection.
        /// Default is false.
        /// </summary>
        public bool DelayAttachOnConnect
        {
            get => GetSocketOptionX<bool>(ZmqSocketOption.DelayAttachOnConnect);
            set => SetSocketOption(ZmqSocketOption.DelayAttachOnConnect, value);
        }
        
        /// <summary>
        /// Disable socket time-wait
        /// </summary>
        public bool DisableTimeWait
        {
            get => GetSocketOptionX<bool>(ZmqSocketOption.DisableTimeWait);
            set => SetSocketOption(ZmqSocketOption.DisableTimeWait, value);
        }
        
        /// <summary>
        /// Defines whether the socket will act as server for CURVE security.
        /// A value of true means the socket will act as CURVE server.
        /// A value of false means the socket will not act as CURVE server, and its security role then depends on other option settings.
        /// Setting this to false shall reset the socket security to NULL.
        /// When you set this you must also set the server's secret key. A server socket does not need to know its own public key.
        /// </summary>
        public bool CurveServer
        {
            get => GetSocketOptionX<bool>(ZmqSocketOption.CurveServer);
            set => SetSocketOption(ZmqSocketOption.CurveServer, value);
        }

        /// <summary>
        /// Sets the socket's long term curve key pair.
        /// You must set this on both CURVE client and server sockets.
        /// You can provide the key as 32 binary bytes.
        /// To generate a certificate, use <see cref="NetMQCertificate"/>.
        /// </summary>
        public NetMQCertificate CurveCertificate
        {
            set
            {
                if (value.SecretKey == null)
                    throw new ArgumentException("NetMQCertificate must have a secret key", nameof(value));
                
                SetSocketOption(ZmqSocketOption.CurveSecretKey, value.SecretKey);
                SetSocketOption(ZmqSocketOption.CurvePublicKey, value.PublicKey);
            } 
        }
        
        /// <summary>
        /// Sets the socket's long term server key.
        /// You must set this on CURVE client sockets.
        /// You can provide the key as 32 binary bytes.
        /// This key must have been generated together with the server's secret key.
        /// To generate a public/secret key pair, use <see cref="NetMQCertificate"/>.
        /// </summary>
        [DisallowNull]
        public byte[]? CurveServerKey
        {
            get => GetSocketOptionX<byte[]>(ZmqSocketOption.CurveServerKey);
            set => SetSocketOption(ZmqSocketOption.CurveServerKey, value);
        }

        /// <summary>
        /// Sets the socket's long term server certificate.
        /// You must set this on CURVE client sockets.
        /// You can provide the key as 32 binary bytes.
        /// This key must have been generated together with the server's secret key.
        /// To generate a certificate, use <see cref="NetMQCertificate"/>.
        /// </summary>
        public NetMQCertificate CurveServerCertificate
        {
            set => SetSocketOption(ZmqSocketOption.CurveServerKey, value.PublicKey);
        }
        
        /// <summary>
        /// If remote peer receives a PING message and doesn't receive another
        /// message within the ttl value, it should close the connection
        /// (measured in tenths of a second)
        /// </summary>
        public TimeSpan HeartbeatTtl
        {
            get => GetSocketOptionTimeSpan(ZmqSocketOption.HeartbeatTtl);
            set => SetSocketOptionTimeSpan(ZmqSocketOption.HeartbeatTtl, value);
        }

        /// <summary>
        /// Time in milliseconds between sending heartbeat PING messages.
        /// </summary>
        public TimeSpan HeartbeatInterval
        {
            get => GetSocketOptionTimeSpan(ZmqSocketOption.HeartbeatInterval);
            set => SetSocketOptionTimeSpan(ZmqSocketOption.HeartbeatInterval, value);
        }
        
        /// <summary>
        /// Time in milliseconds to wait for a PING response before disconnecting
        /// </summary>
        public TimeSpan HeartbeatTimeout
        {
            get => GetSocketOptionTimeSpan(ZmqSocketOption.HeartbeatTimeout);
            set => SetSocketOptionTimeSpan(ZmqSocketOption.HeartbeatTimeout, value);
        }

        /// <summary>
        /// Set message to send to peer upon connecting
        /// </summary>
        public byte[] HelloMessage
        {
            set => SetSocketOption(ZmqSocketOption.HelloMessage, value);
        }
    }
}
