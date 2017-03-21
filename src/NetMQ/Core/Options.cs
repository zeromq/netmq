/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Text;

namespace NetMQ.Core
{
    /// <summary>
    /// Class Options is essentially a container for socket-related option-settings.
    /// </summary>
    internal class Options
    {
        /// <summary>
        /// Create a new Options object with all default values.
        /// </summary>
        public Options()
        {
            Backlog = 100;
            DelayOnClose = true;
            DelayOnDisconnect = true;
            Endian = Endianness.Big;
            IPv4Only = true;
            Linger = -1;
            MaxMessageSize = -1;
            MulticastHops = 1;
            Rate = 100;
            ReceiveHighWatermark = 1000;
            ReceiveLowWatermark = 0;
            ReconnectIvl = 100;
            RecoveryIvl = 10000;
            SendHighWatermark = 1000;
            SendLowWatermark = 0;
            SendTimeout = -1;
            SocketType = ZmqSocketType.None;
            TcpKeepalive = -1;
            TcpKeepaliveCnt = -1;
            TcpKeepaliveIdle = -1;
            TcpKeepaliveIntvl = -1;
            DisableTimeWait = false;
            PgmMaxTransportServiceDataUnitLength = Config.PgmMaxTPDU;
        }

        /// <summary>
        /// Get or set the I/O-thread affinity.
        /// The default value is 0.
        /// </summary>
        public long Affinity { get; set; }

        /// <summary>
        /// Maximum backlog for pending connections.
        /// The default value is 100.
        /// </summary>
        public int Backlog { get; set; }

        /// <summary>
        /// Get or set whether connecting pipes are not attached immediately, meaning a send()
        /// on a socket with only connecting pipes would block.
        /// The default value is false.
        /// </summary>
        public bool DelayAttachOnConnect { get; set; }

        /// <summary>
        /// If true, session reads all the pending messages from the pipe and
        /// sends them to the network when socket is closed.
        /// The default value is true.
        /// </summary>
        public bool DelayOnClose { get; set; }

        /// <summary>
        /// If true, socket reads all the messages from the pipe and delivers
        /// them to the user when the peer terminates.
        /// The default value is true.
        /// </summary>
        public bool DelayOnDisconnect { get; set; }

        /// <summary>
        /// Get or set the Endian-ness, which indicates whether the most-significant bits are placed higher or lower in memory.
        /// The default value is Endianness.Big.
        /// </summary>
        public Endianness Endian { get; set; }

        /// <summary>
        /// If true, (X)SUB socket should filter the messages. If false (which is the default), it should not.
        /// </summary>
        public bool Filter { get; set; }

        /// <summary>
        /// Get or set the byte-array that represents the Identity.
        /// The initial value is null.
        /// </summary>
        public byte[] Identity { get; set; }

        /// <summary>
        /// Get or set the size of the socket-identity byte-array.
        /// The initial value is 0, until the Identity property is set.
        /// </summary>
        public byte IdentitySize { get; set; }

        /// <summary>
        /// Get or set whether this allows the use of IPv4 sockets only.
        /// If true (the default), it will not be possible to communicate with IPv6-only hosts.
        /// If false, the socket can connect to and accept connections from both IPv4 and IPv6 hosts.
        /// </summary>
        public bool IPv4Only { get; set; }

        /// <summary>
        /// Get or set the last socket endpoint resolved URI
        /// The initial value is null.
        /// </summary>
        public string LastEndpoint { get; set; }

        /// <summary>
        /// Get or set the Linger time, in milliseconds.
        /// The default value is -1; The XSub ctor sets this to 0.
        /// </summary>
        public int Linger { get; set; }

        /// <summary>
        /// Get or set the maximum size of message to handle.
        /// The default value is -1, which indicates no limit.
        /// </summary>
        public long MaxMessageSize { get; set; }

        /// <summary>
        /// Sets the time-to-live field in every multicast packet sent.
        /// The default value is 1.
        /// </summary>
        public int MulticastHops { get; set; }

        /// <summary>
        /// Get or set the maximum transfer rate [Kb/s]. The default is 100 Kb/s.
        /// </summary>
        public int Rate { get; set; }

        /// <summary>
        /// If true, router socket accepts non-zmq tcp connections
        /// The default value is false, except the Stream ctor initialises this to true.
        /// Setting this to true changes RecvIdentity to false.
        /// </summary>
        public bool RawSocket { get; set; }

        /// <summary>
        /// If true, the identity message is forwarded to the socket.
        /// The default value is false.
        /// </summary>
        public bool RecvIdentity { get; set; }

        /// <summary>
        /// Get or set the minimum interval between attempts to reconnect, in milliseconds.
        /// The default is 100 ms
        /// </summary>
        public int ReconnectIvl { get; set; }

        /// <summary>
        /// Get or set the maximum interval between attempts to reconnect, in milliseconds.
        /// The default is 0 (unused).
        /// </summary>
        public int ReconnectIvlMax { get; set; }

        /// <summary>
        /// Get or set the recovery time interval [ms]. The default is 10 seconds.
        /// </summary>
        public int RecoveryIvl { get; set; }

        /// <summary>
        /// SO_SNDBUF and SO_RCVBUF to be passed to underlying transport sockets.
        /// The initial value is 0.
        /// </summary>
        public int SendBuffer { get; set; }

        /// <summary>
        /// Get or set the size of the receive-buffer.
        /// The initial value is 0.
        /// </summary>
        public int ReceiveBuffer { get; set; }

        /// <summary>
        /// Get or set the high-water marks for message pipes.
        /// The default value is 1000.
        /// </summary>
        public int SendHighWatermark { get; set; }

        /// <summary>
        /// Get or set the high-water mark for message reception.
        /// The default value is 1000.
        /// </summary>
        public int ReceiveHighWatermark { get; set; }

        /// <summary>
        /// The low-water mark for message transmission.
        /// </summary>
        public int SendLowWatermark { get; set; }

        /// <summary>
        /// The low-water mark for message reception.
        /// </summary>
        public int ReceiveLowWatermark { get; set; }

        /// <summary>
        /// Get or set the timeout for send operations for this socket.
        /// The default value is -1, which means no timeout.
        /// </summary>
        public int SendTimeout { get; set; }

        /// <summary>
        /// Get or set the ID of the socket.
        /// The default value is 0.
        /// </summary>
        public int SocketId { get; set; }

        /// <summary>
        /// Get or set the type of socket (ZmqSocketType).
        /// The default value is ZmqSocketType.None.
        /// </summary>
        public ZmqSocketType SocketType { get; set; }

        /// <summary>
        /// TCP keep-alive settings.
        /// Defaults to -1 = do not change socket options
        /// </summary>
        public int TcpKeepalive { get; set; }

        /// <summary>
        /// Get or set the TCP Keep-Alive Count.
        /// The default value is -1.
        /// </summary>
        public int TcpKeepaliveCnt { get; set; }

        /// <summary>
        /// Get or set the TCP Keep-Alive interval to use when at idle.
        /// The default value is -1.
        /// </summary>
        public int TcpKeepaliveIdle { get; set; }

        /// <summary>
        /// Get or set the TCP Keep-Alive Interval
        /// The default value is -1.
        /// </summary>
        public int TcpKeepaliveIntvl { get; set; }

        /// <summary>
        /// Disable TIME_WAIT tcp state when client disconnect.
        /// The default value is false.
        /// </summary>
        public bool DisableTimeWait { get; set; }

        /// <summary>
        /// Controls the maximum datagram size for PGM.
        /// </summary>
        public int PgmMaxTransportServiceDataUnitLength { get; set; }

        /// <summary>
        /// Assign the given optionValue to the specified option.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="optionValue">an Object that is the value to set that option to</param>
        /// <exception cref="InvalidException">The option and optionValue must be valid.</exception>
        public void SetSocketOption(ZmqSocketOption option, object optionValue)
        {
            switch (option)
            {
                case ZmqSocketOption.SendHighWatermark:
                    SendHighWatermark = (int)optionValue;
                    break;

                case ZmqSocketOption.ReceiveHighWatermark:
                    ReceiveHighWatermark = (int)optionValue;
                    break;

                case ZmqSocketOption.SendLowWatermark:
                    SendLowWatermark = (int)optionValue;
                    break;

                case ZmqSocketOption.ReceiveLowWatermark:
                    ReceiveLowWatermark = (int)optionValue;
                    break;

                case ZmqSocketOption.Affinity:
                    Affinity = (long)optionValue;
                    break;

                case ZmqSocketOption.Identity:
                    byte[] val;

                    if (optionValue is string)
                        val = Encoding.ASCII.GetBytes((string)optionValue);
                    else if (optionValue is byte[])
                        val = (byte[])optionValue;
                    else
                        throw new InvalidException($"In Options.SetSocketOption(Identity, {optionValue?.ToString() ?? "null"}) optionValue must be a string or byte-array.");

                    if (val.Length == 0 || val.Length > 255)
                        throw new InvalidException($"In Options.SetSocketOption(Identity,) optionValue yielded a byte-array of length {val.Length}, should be 1..255.");
                    Identity = new byte[val.Length];
                    val.CopyTo(Identity, 0);
                    IdentitySize = (byte)Identity.Length;
                    break;

                case ZmqSocketOption.Rate:
                    Rate = (int)optionValue;
                    break;

                case ZmqSocketOption.RecoveryIvl:
                    RecoveryIvl = (int)optionValue;
                    break;

                case ZmqSocketOption.SendBuffer:
                    SendBuffer = (int)optionValue;
                    break;

                case ZmqSocketOption.ReceiveBuffer:
                    ReceiveBuffer = (int)optionValue;
                    break;

                case ZmqSocketOption.Linger:
                    Linger = (int)optionValue;
                    break;

                case ZmqSocketOption.ReconnectIvl:
                    var reconnectIvl = (int)optionValue;
                    if (reconnectIvl < -1)
                        throw new InvalidException($"Options.SetSocketOption(ReconnectIvl, {reconnectIvl}) optionValue must be >= -1.");
                    ReconnectIvl = reconnectIvl;
                    break;

                case ZmqSocketOption.ReconnectIvlMax:
                    var reconnectIvlMax = (int)optionValue;
                    if (reconnectIvlMax < 0)
                        throw new InvalidException($"Options.SetSocketOption(ReconnectIvlMax, {reconnectIvlMax}) optionValue must be non-negative.");
                    ReconnectIvlMax = reconnectIvlMax;
                    break;

                case ZmqSocketOption.Backlog:
                    Backlog = (int)optionValue;
                    break;

                case ZmqSocketOption.MaxMessageSize:
                    MaxMessageSize = (long)optionValue;
                    break;

                case ZmqSocketOption.MulticastHops:
                    MulticastHops = (int)optionValue;
                    break;

                case ZmqSocketOption.SendTimeout:
                    SendTimeout = (int)optionValue;
                    break;

                case ZmqSocketOption.IPv4Only:
                    IPv4Only = (bool)optionValue;
                    break;

                case ZmqSocketOption.TcpKeepalive:
                    var tcpKeepalive = (int)optionValue;
                    if (tcpKeepalive != -1 && tcpKeepalive != 0 && tcpKeepalive != 1)
                        throw new InvalidException($"Options.SetSocketOption(TcpKeepalive, {tcpKeepalive}) optionValue is neither -1, 0, nor 1.");
                    TcpKeepalive = tcpKeepalive;
                    break;

                case ZmqSocketOption.DelayAttachOnConnect:
                    DelayAttachOnConnect = (bool)optionValue;
                    break;

                case ZmqSocketOption.TcpKeepaliveIdle:
                    TcpKeepaliveIdle = (int)optionValue;
                    break;

                case ZmqSocketOption.TcpKeepaliveIntvl:
                    TcpKeepaliveIntvl = (int)optionValue;
                    break;

                case ZmqSocketOption.Endian:
                    Endian = (Endianness)optionValue;
                    break;

                case ZmqSocketOption.DisableTimeWait:
                    DisableTimeWait = (bool)optionValue;
                    break;

                case ZmqSocketOption.PgmMaxTransportServiceDataUnitLength:
                    PgmMaxTransportServiceDataUnitLength = (int)optionValue;
                    break;

                default:
                    throw new InvalidException("Options.SetSocketOption called with invalid ZmqSocketOption of " + option);
            }
        }

        /// <summary>
        /// Get the value of the specified option.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to get</param>
        /// <returns>an Object that is the value of that option</returns>
        /// <exception cref="InvalidException">A valid option must be specified.</exception>
        public object GetSocketOption(ZmqSocketOption option)
        {
            switch (option)
            {
                case ZmqSocketOption.SendHighWatermark:
                    return SendHighWatermark;

                case ZmqSocketOption.ReceiveHighWatermark:
                    return ReceiveHighWatermark;

                case ZmqSocketOption.SendLowWatermark:
                    return SendLowWatermark;

                case ZmqSocketOption.ReceiveLowWatermark:
                    return ReceiveLowWatermark;

                case ZmqSocketOption.Affinity:
                    return Affinity;

                case ZmqSocketOption.Identity:
                    return Identity;

                case ZmqSocketOption.Rate:
                    return Rate;

                case ZmqSocketOption.RecoveryIvl:
                    return RecoveryIvl;

                case ZmqSocketOption.SendBuffer:
                    return SendBuffer;

                case ZmqSocketOption.ReceiveBuffer:
                    return ReceiveBuffer;

                case ZmqSocketOption.Type:
                    return SocketType;

                case ZmqSocketOption.Linger:
                    return Linger;

                case ZmqSocketOption.ReconnectIvl:
                    return ReconnectIvl;

                case ZmqSocketOption.ReconnectIvlMax:
                    return ReconnectIvlMax;

                case ZmqSocketOption.Backlog:
                    return Backlog;

                case ZmqSocketOption.MaxMessageSize:
                    return MaxMessageSize;

                case ZmqSocketOption.MulticastHops:
                    return MulticastHops;

                case ZmqSocketOption.SendTimeout:
                    return SendTimeout;

                case ZmqSocketOption.IPv4Only:
                    return IPv4Only;

                case ZmqSocketOption.TcpKeepalive:
                    return TcpKeepalive;

                case ZmqSocketOption.DelayAttachOnConnect:
                    return DelayAttachOnConnect;

                case ZmqSocketOption.TcpKeepaliveIdle:
                    return TcpKeepaliveIdle;

                case ZmqSocketOption.TcpKeepaliveIntvl:
                    return TcpKeepaliveIntvl;

                case ZmqSocketOption.LastEndpoint:
                    return LastEndpoint;

                case ZmqSocketOption.Endian:
                    return Endian;

                case ZmqSocketOption.DisableTimeWait:
                    return DisableTimeWait;

                default:
                    throw new InvalidException("GetSocketOption called with invalid ZmqSocketOption of " + option);
            }
        }
    }
}