using System;
using JetBrains.Annotations;
using NetMQ.Core;

namespace NetMQ
{
    /// <summary>
    /// A SocketOptions is simply a convenient way to access the options of a particular socket.
    /// This class holds a reference to the socket, and it's properties provide a concise way
    /// to access that socket's option values -- instead of calling GetSocketOption/SetSocketOption.
    /// </summary>
    public class SocketOptions
    {
        /// <summary>
        /// The NetMQSocket that this SocketOptions is referencing.
        /// </summary>
        private readonly NetMQSocket m_socket;

        /// <summary>
        /// Create a new SocketOptions that references the given NetMQSocket.
        /// </summary>
        /// <param name="socket">the NetMQSocket for this SocketOptions to hold a reference to</param>
        public SocketOptions([NotNull] NetMQSocket socket)
        {
            m_socket = socket;
        }

        /// <summary>
        /// Get or set the I/O-thread affinity. This is a 64-bit value used to specify  which threads from the I/O thread-pool
        /// associated with the socket's context shall handle newly-created connections.
        /// 0 means no affinity, meaning that work shall be distributed fairly among all I/O threads.
        /// For non-zero values, the lowest bit corresponds to thread 1, second lowest bit to thread 2, and so on.
        /// </summary>
        public long Affinity
        {
            get => m_socket.GetSocketOptionLong(ZmqSocketOption.Affinity);
            set => m_socket.SetSocketOption(ZmqSocketOption.Affinity, value);
        }

        /// <summary>
        /// Get or set unique identity of the socket, from a message-queueing router's perspective.
        /// This is a byte-array of at most 255 bytes.
        /// </summary>
        public byte[] Identity
        {
            [CanBeNull]
            get { return m_socket.GetSocketOptionX<byte[]>(ZmqSocketOption.Identity); }
            [NotNull]
            set { m_socket.SetSocketOption(ZmqSocketOption.Identity, value); }
        }

        /// <summary>
        /// Get or set the maximum send or receive data rate for multicast transports on the specified socket.
        /// </summary>
        public int MulticastRate
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.Rate);
            set => m_socket.SetSocketOption(ZmqSocketOption.Rate, value);
        }

        /// <summary>
        /// Get or set the recovery-interval for multicast transports using the specified socket.
        /// This option determines the maximum time that a receiver can be absent from a multicast group
        /// before unrecoverable data loss will occur. Default is 10,000 ms (10 seconds).
        /// </summary>
        public TimeSpan MulticastRecoveryInterval
        {
            get => m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.RecoveryIvl);
            set => m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.RecoveryIvl, value);
        }

        /// <summary>
        /// Get or set the size of the transmit buffer for the specified socket.
        /// </summary>
        public int SendBuffer
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.SendBuffer);
            set => m_socket.SetSocketOption(ZmqSocketOption.SendBuffer, value);
        }

        /// <summary>
        /// Get or set the size of the receive buffer for the specified socket.
        /// A value of zero means that the OS default is in effect.
        /// </summary>
        public int ReceiveBuffer
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.ReceiveBuffer);
            set => m_socket.SetSocketOption(ZmqSocketOption.ReceiveBuffer, value);
        }

        /// <summary>
        /// Gets whether the last frame received on the socket had the <em>more</em> flag set or not.
        /// </summary>
        /// <value><c>true</c> if receive more; otherwise, <c>false</c>.</value>
        public bool ReceiveMore => m_socket.GetSocketOptionX<bool>(ZmqSocketOption.ReceiveMore);

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
            get => m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.Linger);
            set => m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.Linger, value);
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
            get => m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl);
            set => m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl, value);
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
            get => m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvlMax);
            set => m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvlMax, value);
        }

        /// <summary>
        /// Get or set the maximum length of the queue of outstanding peer connections
        /// for the specified socket. This only applies to connection-oriented transports.
        /// Default is 100.
        /// </summary>
        public int Backlog
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.Backlog);
            set => m_socket.SetSocketOption(ZmqSocketOption.Backlog, value);
        }

        /// <summary>
        /// Get or set the upper limit to the size for inbound messages.
        /// If a peer sends a message larger than this it is disconnected.
        /// The default value is -1, which means no limit.
        /// </summary>
        public long MaxMsgSize
        {
            get => m_socket.GetSocketOptionLong(ZmqSocketOption.MaxMessageSize);
            set => m_socket.SetSocketOption(ZmqSocketOption.MaxMessageSize, value);
        }

        /// <summary>
        /// Get or set the high-water-mark for transmission.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int SendHighWatermark
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.SendHighWatermark);
            set => m_socket.SetSocketOption(ZmqSocketOption.SendHighWatermark, value);
        }

        /// <summary>
        /// Get or set the high-water-mark for reception.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int ReceiveHighWatermark
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.ReceiveHighWatermark);
            set => m_socket.SetSocketOption(ZmqSocketOption.ReceiveHighWatermark, value);
        }

        /// <summary>
        /// The low-water mark for message transmission.
        /// This is the number of messages that should be processed before transmission is
        /// unblocked (in case it was blocked by reaching high-watermark). The default value is
        /// calculated using relevant high-watermark (HWM): HWM > 2048 ? HWM - 1024 : (HWM + 1) / 2
        /// </summary>
        public int SendLowWatermark
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.SendLowWatermark);
            set => m_socket.SetSocketOption(ZmqSocketOption.SendLowWatermark, value);
        }

        /// <summary>
        /// The low-water mark for message reception.
        /// This is the number of messages that should be processed  before reception is
        /// unblocked (in case it was blocked by reaching high-watermark). The default value is
        /// calculated using relevant high-watermark (HWM): HWM > 2048 ? HWM - 1024 : (HWM + 1) / 2
        /// </summary>
        public int ReceiveLowWatermark
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.ReceiveLowWatermark);
            set => m_socket.SetSocketOption(ZmqSocketOption.ReceiveLowWatermark, value);
        }

        /// <summary>
        /// Get or set the time-to-live (maximum number of hops) that outbound multicast packets
        /// are allowed to propagate.
        /// The default value of 1 means that the multicast packets don't leave the local network.
        /// </summary>
        public int MulticastHops
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.MulticastHops);
            set => m_socket.SetSocketOption(ZmqSocketOption.MulticastHops, value);
        }

        /// <summary>
        /// Get or set whether the underlying socket is for IPv4 only (not IPv6),
        /// as opposed to one that allows connections with either IPv4 or IPv6.
        /// </summary>
        public bool IPv4Only
        {
            get => m_socket.GetSocketOptionX<bool>(ZmqSocketOption.IPv4Only);
            set => m_socket.SetSocketOption(ZmqSocketOption.IPv4Only, value);
        }

        /// <summary>
        /// Get the last endpoint bound for TCP and IPC transports.
        /// The returned value will be a string in the form of a ZMQ DSN.
        /// </summary>
        /// <remarks>
        /// If the TCP host is ANY, indicated by a *, then the returned address
        /// will be 0.0.0.0 (for IPv4).
        /// </remarks>
        [CanBeNull]
        public string LastEndpoint => m_socket.GetSocketOptionX<string>(ZmqSocketOption.LastEndpoint);

        /// <summary>
        /// Set the RouterSocket behavior when an unroutable message is encountered.
        /// A value of false is the default and discards the message silently when it cannot be routed.
        /// A value of true causes throw of HostUnreachableException if the message cannot be routed.
        /// </summary>
        public bool RouterMandatory
        {
            set => m_socket.SetSocketOption(ZmqSocketOption.RouterMandatory, value);
        }

        /// <summary>
        /// Get or set whether to use TCP keepalive.
        /// </summary>
        /// <remarks>
        /// When Keepalive is enabled, then your socket will periodically send an empty keepalive probe packet
        /// with the ACK flag on. The remote endpoint does not need to support keepalive at all, just TCP/IP.
        /// If you receive a reply to your keepalive probe, you can assume that the connection is still up and running.
        /// This procedure is useful because if the other peers lose their connection (for example, by rebooting)
        /// you will notice that the connection is broken, even if you don't have traffic on it.
        /// If the keepalive probes are not replied to by your peer, you can assert that the connection
        /// cannot be considered valid and then take the corrective action.
        /// </remarks>
        public bool TcpKeepalive
        {
            get => m_socket.GetSocketOption(ZmqSocketOption.TcpKeepalive) == 1;
            set => m_socket.SetSocketOption(ZmqSocketOption.TcpKeepalive, value ? 1 : 0);
            // TODO: What about the value -1, which means use the OS default?  jh
            // See  http://api.zeromq.org/3-2:zmq-getsockopt
        }

        /// <summary>
        /// Get or set the keep-alive time - the duration between two keepalive transmissions in idle condition.
        /// The TCP keepalive period is required by socket implementers to be configurable and by default is
        /// set to no less than 2 hours.
        /// </summary>
        public TimeSpan TcpKeepaliveIdle
        {
            get => m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIdle);
            set => m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIdle, value);
            // TODO: What about the value -1, which means use the OS default?  jh
            // See  http://api.zeromq.org/3-2:zmq-getsockopt
        }

        /// <summary>
        /// Get or set the TCP keep-alive interval - the duration between two keepalive transmission if no response was received to a previous keepalive probe.
        /// </summary>
        /// <remarks>
        /// By default a keepalive packet is sent every 2 hours or 7,200,000 milliseconds
        /// (TODO: Check these comments concerning default values!  jh)
        /// if no other data have been carried over the TCP connection.
        /// If there is no response to a keepalive, it is repeated once every KeepAliveInterval seconds.
        /// The default is one second.
        /// </remarks>
        public TimeSpan TcpKeepaliveInterval
        {
            get => m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIntvl);
            set => m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIntvl, value);
        }

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
            get => m_socket.GetSocketOptionX<bool>(ZmqSocketOption.DelayAttachOnConnect);
            set => m_socket.SetSocketOption(ZmqSocketOption.DelayAttachOnConnect, value);
        }

        /// <summary>
        /// This applies only to publisher sockets.
        /// Set whether to send all subscription messages upstream, not just unique ones.
        /// The default is false.
        /// </summary>
        public bool XPubVerbose
        {
            set => m_socket.SetSocketOption(ZmqSocketOption.XpubVerbose, value);
        }

        /// <summary>
        /// This applies only to publisher sockets.
        /// Set whether to support broadcast functionality
        /// </summary>
        public bool XPubBroadcast
        {
            set => m_socket.SetSocketOption(ZmqSocketOption.XPublisherBroadcast, value);
        }

        /// <summary>
        /// This applies only to router sockets.
        /// Set whether RouterSocket allows non-zmq tcp connects.
        /// If true, router socket accepts non-zmq tcp connections
        /// </summary>
        public bool RouterRawSocket
        {
            set => m_socket.SetSocketOption(ZmqSocketOption.RouterRawSocket, value);
        }

        /// <summary>
        /// When enabled new router connections with same identity take over old ones
        /// </summary>
        public bool RouterHandover
        {
            set => m_socket.SetSocketOption(ZmqSocketOption.RouterHandover, value);
        }

        /// <summary>
        /// Get or set the byte-order: big-endian, vs little-endian.
        /// </summary>
        public Endianness Endian
        {
            get => m_socket.GetSocketOptionX<Endianness>(ZmqSocketOption.Endian);
            set => m_socket.SetSocketOption(ZmqSocketOption.Endian, value);
        }

        public bool ManualPublisher
        {
            set => m_socket.SetSocketOption(ZmqSocketOption.XPublisherManual, value);
        }

        public bool DisableTimeWait
        {
            get => m_socket.GetSocketOptionX<bool>(ZmqSocketOption.DisableTimeWait);
            set => m_socket.SetSocketOption(ZmqSocketOption.DisableTimeWait, value);
        }

        /// <summary>
        /// Controls the maximum datagram size for PGM.
        /// </summary>
        public int PgmMaxTransportServiceDataUnitLength
        {
            get => m_socket.GetSocketOptionX<int>(ZmqSocketOption.PgmMaxTransportServiceDataUnitLength);
            set => m_socket.SetSocketOption(ZmqSocketOption.PgmMaxTransportServiceDataUnitLength, value);
        }
    }
}
