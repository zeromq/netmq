/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file
        
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
using System.Collections.Generic;
using System.Text;
using NetMQ.zmq.Transports.Tcp;

namespace NetMQ.zmq
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
            ReceiveTimeout = -1;
            ReconnectIvl = 100;
            RecoveryIvl = 10000;
            SendHighWatermark = 1000;
            SendTimeout = -1;
            SocketType = ZmqSocketType.None;
            TcpAcceptFilters = new List<TcpAddress.TcpAddressMask>();
            TcpKeepalive = -1;
            TcpKeepaliveCnt = -1;
            TcpKeepaliveIdle = -1;
            TcpKeepaliveIntvl = -1;
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
        public String LastEndpoint { get; set; }

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
        /// Get or set the maximum size of message to handle.
        /// </summary>
        [Obsolete("Use MaxMessageSize")]
        public long Maxmsgsize
        {
            get { return this.MaxMessageSize; }
            set { this.MaxMessageSize = value; }
        }

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
        /// The default value is false, except the Stream ctor initializes this to true.
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
        /// Get or set the timeout for send operations for this socket.
        /// The default value is -1, which means no timeout.
        /// </summary>
        public int SendTimeout { get; set; }

        /// <summary>
        /// Get or set the timeout for receive operations for this socket.
        /// The default value is -1, which means no timeout.
        /// </summary>
        public int ReceiveTimeout { get; set; }

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
        /// Get the TCP accept() filters,
        /// this being a list of TcpAddressMasks.
        /// </summary>
        public List<TcpAddress.TcpAddressMask> TcpAcceptFilters { get; private set; }

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
        /// Assign the given optionValue to the specified option.
        /// </summary>
        /// <param name="option">a ZmqSocketOptions that specifies what to set</param>
        /// <param name="optionValue">an Object that is the value to set that option to</param>
        public void SetSocketOption(ZmqSocketOptions option, Object optionValue)
        {
            switch (option)
            {
                case ZmqSocketOptions.SendHighWatermark:
                    SendHighWatermark = (int)optionValue;
                    break;

                case ZmqSocketOptions.ReceiveHighWatermark:
                    ReceiveHighWatermark = (int)optionValue;
                    break;

                case ZmqSocketOptions.Affinity:
                    Affinity = (long)optionValue;
                    break;

                case ZmqSocketOptions.Identity:
                    byte[] val;

                    if (optionValue is String)
                        val = Encoding.ASCII.GetBytes((String)optionValue);
                    else if (optionValue is byte[])
                        val = (byte[])optionValue;
                    else
                    {
                        String xMsg = String.Format("In Options.SetSocketOption(Identity, {0}) optionValue must be a String or byte-array.", optionValue == null ? "null" : optionValue.ToString());
                        throw new InvalidException(xMsg);
                    }

                    if (val.Length == 0 || val.Length > 255)
                    {
                        String xMsg = String.Format("In Options.SetSocketOption(Identity,) optionValue yielded a byte-array of length {0}, should be 1..255.", val.Length);
                        throw new InvalidException(xMsg);
                    }
                    Identity = new byte[val.Length];
                    val.CopyTo(Identity, 0);
                    IdentitySize = (byte)Identity.Length;
                    break;

                case ZmqSocketOptions.Rate:
                    Rate = (int)optionValue;
                    break;

                case ZmqSocketOptions.RecoveryIvl:
                    RecoveryIvl = (int)optionValue;
                    break;

                case ZmqSocketOptions.SendBuffer:
                    SendBuffer = (int)optionValue;
                    break;

                case ZmqSocketOptions.ReceiveBuffer:
                    ReceiveBuffer = (int)optionValue;
                    break;

                case ZmqSocketOptions.Linger:
                    Linger = (int)optionValue;
                    break;

                case ZmqSocketOptions.ReconnectIvl:
                    ReconnectIvl = (int)optionValue;
                    if (ReconnectIvl < -1)
                    {
                        throw new InvalidException(String.Format("Options.SetSocketOption(ReconnectIvl, {0}) optionValue must be >= -1.", ReconnectIvl));
                    }
                    break;

                case ZmqSocketOptions.ReconnectIvlMax:
                    ReconnectIvlMax = (int)optionValue;
                    if (ReconnectIvlMax < 0)
                    {
                        throw new InvalidException(String.Format("Options.SetSocketOption(ReconnectIvlMax, {0}) optionValue must be non-negative.", ReconnectIvlMax));
                    }
                    break;

                case ZmqSocketOptions.Backlog:
                    Backlog = (int)optionValue;
                    break;

                case ZmqSocketOptions.Maxmsgsize:
                    MaxMessageSize = (long)optionValue;
                    break;

                case ZmqSocketOptions.MulticastHops:
                    MulticastHops = (int)optionValue;
                    break;

                case ZmqSocketOptions.ReceiveTimeout:
                    ReceiveTimeout = (int)optionValue;
                    break;

                case ZmqSocketOptions.SendTimeout:
                    SendTimeout = (int)optionValue;
                    break;

                case ZmqSocketOptions.IPv4Only:
                    IPv4Only = (bool)optionValue;
                    break;

                case ZmqSocketOptions.TcpKeepalive:
                    TcpKeepalive = (int)optionValue;
                    if (TcpKeepalive != -1 && TcpKeepalive != 0 && TcpKeepalive != 1)
                    {
                        throw new InvalidException(String.Format("Options.SetSocketOption(TcpKeepalive, {0}) optionValue is neither -1, 0, nor 1.", TcpKeepalive));
                    }
                    break;

                case ZmqSocketOptions.DelayAttachOnConnect:
                    DelayAttachOnConnect = (bool)optionValue;
                    break;

                case ZmqSocketOptions.TcpKeepaliveIdle:
                    TcpKeepaliveIdle = (int)optionValue;
                    break;

                case ZmqSocketOptions.TcpKeepaliveIntvl:
                    TcpKeepaliveIntvl = (int)optionValue;
                    break;

                case ZmqSocketOptions.TcpAcceptFilter:
                    String filterStr = (String)optionValue;
                    if (filterStr == null)
                    {
                        TcpAcceptFilters.Clear();
                    }
                    else if (filterStr.Length == 0 || filterStr.Length > 255)
                    {
                        throw new InvalidException(String.Format("Options.SetSocketOption(TcpAcceptFilter,{0}), optionValue has invalid length of {1) but must be 1..255", filterStr, filterStr.Length));
                    }
                    else
                    {
                        TcpAddress.TcpAddressMask filter = new TcpAddress.TcpAddressMask();
                        filter.Resolve(filterStr, IPv4Only);
                        TcpAcceptFilters.Add(filter);
                    }
                    break;

                case ZmqSocketOptions.Endian:
                    Endian = (Endianness)optionValue;
                    break;

                default:
                    throw new InvalidException("Options.SetSocketOption called with invalid ZmqSocketOptions of " + option);
            }
        }

        /// <summary>
        /// Get the value of the specified option.
        /// </summary>
        /// <param name="option">a ZmqSocketOptions that specifies what to get</param>
        /// <returns>an Object that is the value of that option</returns>
        public Object GetSocketOption(ZmqSocketOptions option)
        {
            switch (option)
            {
                case ZmqSocketOptions.SendHighWatermark:
                    return SendHighWatermark;

                case ZmqSocketOptions.ReceiveHighWatermark:
                    return ReceiveHighWatermark;

                case ZmqSocketOptions.Affinity:
                    return Affinity;

                case ZmqSocketOptions.Identity:
                    return Identity;

                case ZmqSocketOptions.Rate:
                    return Rate;

                case ZmqSocketOptions.RecoveryIvl:
                    return RecoveryIvl;

                case ZmqSocketOptions.SendBuffer:
                    return SendBuffer;

                case ZmqSocketOptions.ReceiveBuffer:
                    return ReceiveBuffer;

                case ZmqSocketOptions.Type:
                    return SocketType;

                case ZmqSocketOptions.Linger:
                    return Linger;

                case ZmqSocketOptions.ReconnectIvl:
                    return ReconnectIvl;

                case ZmqSocketOptions.ReconnectIvlMax:
                    return ReconnectIvlMax;

                case ZmqSocketOptions.Backlog:
                    return Backlog;

                case ZmqSocketOptions.Maxmsgsize:
                    return MaxMessageSize;

                case ZmqSocketOptions.MulticastHops:
                    return MulticastHops;

                case ZmqSocketOptions.ReceiveTimeout:
                    return ReceiveTimeout;

                case ZmqSocketOptions.SendTimeout:
                    return SendTimeout;

                case ZmqSocketOptions.IPv4Only:
                    return IPv4Only;

                case ZmqSocketOptions.TcpKeepalive:
                    return TcpKeepalive;

                case ZmqSocketOptions.DelayAttachOnConnect:
                    return DelayAttachOnConnect;

                case ZmqSocketOptions.TcpKeepaliveIdle:
                    return TcpKeepaliveIdle;

                case ZmqSocketOptions.TcpKeepaliveIntvl:
                    return TcpKeepaliveIntvl;

                case ZmqSocketOptions.LastEndpoint:
                    return LastEndpoint;

                case ZmqSocketOptions.Endian:
                    return Endian;

                default:
                    throw new InvalidException("GetSocketOption called with invalid ZmqSocketOptions of " + option);
            }
        }
    }
}