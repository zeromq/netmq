using System;
using JetBrains.Annotations;
using NetMQ.zmq;

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
            get { return m_socket.GetSocketOptionLong(ZmqSocketOption.Affinity); }
            set { m_socket.SetSocketOption(ZmqSocketOption.Affinity, value); }
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
            get { return m_socket.GetSocketOptionX<byte[]>(ZmqSocketOption.Identity); }
            [NotNull]
            set { m_socket.SetSocketOption(ZmqSocketOption.Identity, value); }
        }

        public int MulticastRate
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.Rate); }
            set { m_socket.SetSocketOption(ZmqSocketOption.Rate, value); }
        }

        public TimeSpan MulticastRecoveryInterval
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl, value); }
        }

        public int SendBuffer
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.SendBuffer); }
            set { m_socket.SetSocketOption(ZmqSocketOption.SendBuffer, value); }
        }

        [Obsolete("Use ReceiveBuffer instead")]
        public int ReceivevBuffer
        {
            get { return ReceiveBuffer; }
            set { ReceiveBuffer = value; }
        }

        public int ReceiveBuffer
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.ReceiveBuffer); }
            set { m_socket.SetSocketOption(ZmqSocketOption.ReceiveBuffer, value); }
        }

        /// <summary>
        /// Gets whether the last frame received on the socket had the <em>more</em> flag set or not.
        /// </summary>
        /// <value><c>true</c> if receive more; otherwise, <c>false</c>.</value>
        public bool ReceiveMore
        {
            get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOption.ReceiveMore); }
        }

        public TimeSpan Linger
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.Linger); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.Linger, value); }
        }

        public TimeSpan ReconnectInterval
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvl, value); }
        }

        public TimeSpan ReconnectIntervalMax
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvlMax); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.ReconnectIvlMax, value); }
        }

        public int Backlog
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.Backlog); }
            set { m_socket.SetSocketOption(ZmqSocketOption.Backlog, value); }
        }

        public long MaxMsgSize
        {
            get { return m_socket.GetSocketOptionLong(ZmqSocketOption.MaxMessageSize); }
            set { m_socket.SetSocketOption(ZmqSocketOption.MaxMessageSize, value); }
        }

        /// <summary>
        /// Get or set the high-water-mark for transmission.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int SendHighWatermark
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.SendHighWatermark); }
            set { m_socket.SetSocketOption(ZmqSocketOption.SendHighWatermark, value); }
        }

        /// <summary>
        /// Get or set the high-water-mark for reception.
        /// This is a hard limit on the number of messages that are allowed to queue up
        /// before mitigative action is taken.
        /// The default is 1000.
        /// </summary>
        public int ReceiveHighWatermark
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.ReceiveHighWatermark); }
            set { m_socket.SetSocketOption(ZmqSocketOption.ReceiveHighWatermark, value); }
        }

        public int MulticastHops
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.MulticastHops); }
            set { m_socket.SetSocketOption(ZmqSocketOption.MulticastHops, value); }
        }

        [Obsolete("Pass a TimeSpan value directly to socket receive methods instead.")]
        public TimeSpan ReceiveTimeout
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.ReceiveTimeout); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.ReceiveTimeout, value); }
        }

        public TimeSpan SendTimeout
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.SendTimeout); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.SendTimeout, value); }
        }

        public bool IPv4Only
        {
            get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOption.IPv4Only); }
            set { m_socket.SetSocketOption(ZmqSocketOption.IPv4Only, value); }
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
            get { return m_socket.GetSocketOptionX<string>(ZmqSocketOption.LastEndpoint); }
        }

        public bool RouterMandatory
        {
            set { m_socket.SetSocketOption(ZmqSocketOption.RouterMandatory, value); }
        }

        public bool TcpKeepalive
        {
            get { return m_socket.GetSocketOption(ZmqSocketOption.TcpKeepalive) == 1; }
            set { m_socket.SetSocketOption(ZmqSocketOption.TcpKeepalive, value ? 1 : 0); }
        }

        [Obsolete("This option is not supported and has no effect")]
        public int TcpKeepaliveCnt
        {
            set { /* m_socket.SetSocketOption(ZmqSocketOption.TcpKeepaliveCnt, value); */ }
        }

        public TimeSpan TcpKeepaliveIdle
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIdle); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIdle, value); }
        }

        public TimeSpan TcpKeepaliveInterval
        {
            get { return m_socket.GetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIntvl); }
            set { m_socket.SetSocketOptionTimeSpan(ZmqSocketOption.TcpKeepaliveIntvl, value); }
        }

        [CanBeNull]
        public string TcpAcceptFilter
        {
            // TODO the logic here doesn't really suit a setter -- set values are appended to a list, and null clear that list
            // get { return m_socket.GetSocketOptionX<string>(ZmqSocketOption.TcpAcceptFilter); }
            set { m_socket.SetSocketOption(ZmqSocketOption.TcpAcceptFilter, value); }
        }

        public bool DelayAttachOnConnect
        {
            get { return m_socket.GetSocketOptionX<bool>(ZmqSocketOption.DelayAttachOnConnect); }
            set { m_socket.SetSocketOption(ZmqSocketOption.DelayAttachOnConnect, value); }
        }

        public bool XPubVerbose
        {
            set { m_socket.SetSocketOption(ZmqSocketOption.XpubVerbose, value); }
        }

        public bool RouterRawSocket
        {
            set { m_socket.SetSocketOption(ZmqSocketOption.RouterRawSocket, value); }
        }

        public Endianness Endian
        {
            get { return m_socket.GetSocketOptionX<Endianness>(ZmqSocketOption.Endian); }
            set { m_socket.SetSocketOption(ZmqSocketOption.Endian, value); }
        }

        public bool ManualPublisher
        {
            set { m_socket.SetSocketOption(ZmqSocketOption.XPublisherManual, value); }
        }
    }
}
