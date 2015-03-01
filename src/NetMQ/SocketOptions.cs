using System;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// A SocketOptions is simply a convenient way to accesss the options of a particular socket.
    /// This class holds a reference to the socket, and it's properties provide a concise way
    /// to access that socket's option values -- instead of calling GetSocketOption/SetSockeetOption.
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
            get { return m_socket.GetSocketOptionLong(ZmqSocketOptions.Affinity); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.Affinity, value); }
        }

        [Obsolete("This property doesn't effect NetMQ anymore")]
        public bool CopyMessages
        {
            get { return false; }
            set { }
        }

        public byte[] Identity
        {
            [CanBeNull]
            get { return m_socket.GetSocketOptionX<byte[]>(ZmqSocketOptions.Identity); }
            [NotNull]
            set { m_socket.SetSocketOption(ZmqSocketOptions.Identity, value); }
        }

        public int MulticastRate
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.Rate); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.Rate, value); }
        }

        public TimeSpan MulticastRecoveryInterval
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
        }

        public int SendBuffer
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.SendBuffer); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.SendBuffer, value); }
        }

        [Obsolete("Use ReceiveBuffer instead")]
        public int ReceivevBuffer
        {
            get { return ReceiveBuffer; }
            set { ReceiveBuffer = value; }
        }

        public int ReceiveBuffer
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.ReceiveBuffer); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.ReceiveBuffer, value); }
        }

        /// <summary>
        /// Gets whether the last frame received on the socket had the <em>more</em> flag set or not.
        /// </summary>
        /// <value><c>true</c> if receive more; otherwise, <c>false</c>.</value>
        public bool ReceiveMore
        {
            get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.ReceiveMore); }
        }

        public TimeSpan Linger
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.Linger); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.Linger, value); }
        }

        public TimeSpan ReconnectInterval
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvl, value); }
        }

        public TimeSpan ReconnectIntervalMax
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvlMax); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReconnectIvlMax, value); }
        }

        public int Backlog
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.Backlog); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.Backlog, value); }
        }

        public long MaxMsgSize
        {
            get { return m_socket.GetSocketOptionLong(ZmqSocketOptions.Maxmsgsize); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.Maxmsgsize, value); }
        }

        /// <summary>
        /// Get or set the high-water-mark for transmission.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int SendHighWatermark
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.SendHighWatermark); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.SendHighWatermark, value); }
        }

        /// <summary>
        /// Get or set the high-water-mark for reception.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int ReceiveHighWatermark
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.ReceiveHighWatermark); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.ReceiveHighWatermark, value); }
        }

        public int MulticastHops
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.MulticastHops); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.MulticastHops, value); }
        }

        public TimeSpan ReceiveTimeout
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.ReceiveTimeout); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.ReceiveTimeout, value); }
        }

        public TimeSpan SendTimeout
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.SendTimeout); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.SendTimeout, value); }
        }

        public bool IPv4Only
        {
            get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.IPv4Only); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.IPv4Only, value); }
        }

        [Obsolete("Use LastEndpoint instead")]
        [CanBeNull]
        public string GetLastEndpoint
        {
            get { return LastEndpoint; }
        }

        [CanBeNull]
        public string LastEndpoint
        {
            get { return m_socket.GetSocketOptionX<string>(ZmqSocketOptions.LastEndpoint); }
        }

        public bool RouterMandatory
        {
            set { m_socket.SetSocketOption(ZmqSocketOptions.RouterMandatory, value); }
        }

        public bool TcpKeepalive
        {
            get { return m_socket.GetSocketOption(ZmqSocketOptions.TcpKeepalive) == 1; }
            set { m_socket.SetSocketOption(ZmqSocketOptions.TcpKeepalive, value ? 1 : 0); }
        }

        [Obsolete("This option is not supported and has no effect")]
        public int TcpKeepaliveCnt
        {
            set { /* m_socket.SetSocketOption(ZmqSocketOptions.TcpKeepaliveCnt, value); */ }
        }

        public TimeSpan TcpKeepaliveIdle
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIdle); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIdle, value); }
        }

        public TimeSpan TcpKeepaliveInterval
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIntvl); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOptions.TcpKeepaliveIntvl, value); }
        }

        [CanBeNull]
        public string TcpAcceptFilter
        {
            // TODO the logic here doesn't really suit a setter -- set values are appended to a list, and null clear that list
            // get { return m_socket.GetSocketOptionX<string>(ZmqSocketOptions.TcpAcceptFilter); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.TcpAcceptFilter, value); }
        }

        public bool DelayAttachOnConnect
        {
            get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOptions.DelayAttachOnConnect); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.DelayAttachOnConnect, value); }
        }

        public bool XPubVerbose
        {
            set { m_socket.SetSocketOption(ZmqSocketOptions.XpubVerbose, value); }
        }

        public bool RouterRawSocket
        {
            set { m_socket.SetSocketOption(ZmqSocketOptions.RouterRawSocket, value); }
        }

        public Endianness Endian
        {
            get { return m_socket.GetSocketOptionX<Endianness>(ZmqSocketOptions.Endian); }
            set { m_socket.SetSocketOption(ZmqSocketOptions.Endian, value); }
        }

        public bool ManualPublisher
        {
            set { m_socket.SetSocketOption(ZmqSocketOptions.XPublisherManual, value); }
        }
    }
}
