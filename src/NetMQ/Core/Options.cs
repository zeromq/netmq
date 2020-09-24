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
            Mechanism = MechanismType.Null;
            AsServer = false;
            CurvePublicKey = new byte[32];
            CurveSecretKey = new byte[32];
            CurveServerKey = new byte[32];
            HeartbeatTtl = 0;
            HeartbeatInterval = 0;
            HeartbeatTimeout = -1;
            HelloMsg = null;
            CanSendHelloMsg = false;
            Correlate = false;
            Relaxed = false;
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
        public byte[]? Identity { get; set; }

        /// <summary>
        /// Get or set the size of the socket-identity byte-array.
        /// The initial value is 0, until the Identity property is set.
        /// </summary>
        public byte IdentitySize {
            get
            {
                if (Identity != null)
                    return (byte)Identity.Length;

                return 0;
            }
        }
        
        public byte[]? LastPeerRoutingId { get; set; }

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
        public string? LastEndpoint { get; set; }

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
        /// Security mechanism for all connections on this socket
        /// </summary>
        public MechanismType Mechanism { get; set; }
        
        /// <summary>
        /// If peer is acting as server for PLAIN or CURVE mechanisms
        /// </summary>
        public bool AsServer { get; set; }
        
        /// <summary>
        /// Security credentials for CURVE mechanism
        /// </summary>
        public byte[] CurvePublicKey { get; set; }
        
        /// <summary>
        /// Security credentials for CURVE mechanism
        /// </summary>
        public byte[] CurveSecretKey { get; set; }
        
        /// <summary>
        /// Security credentials for CURVE mechanism
        /// </summary>
        public byte[] CurveServerKey { get; set; }
        
        /// <summary>
        /// If remote peer receives a PING message and doesn't receive another
        /// message within the ttl value, it should close the connection
        /// (measured in tenths of a second)
        /// </summary>
        public int HeartbeatTtl { get; set; }
        
        /// <summary>
        /// Time in milliseconds between sending heartbeat PING messages.
        /// </summary>
        public int HeartbeatInterval { get; set; }
        
        /// <summary>
        /// Time in milliseconds to wait for a PING response before disconnecting
        /// </summary>
        public int HeartbeatTimeout { get; set; }
        
        /// <summary>
        /// Hello msg to send to peer upon connecting
        /// </summary>
        public byte[]? HelloMsg { get; set; }
        
        /// <summary>
        /// Indicate of socket can send an hello msg
        /// </summary>
        public bool CanSendHelloMsg { get; set; }

        public bool Correlate { get; set; }
        public bool Relaxed { get; set; }

        /// <summary>
        /// Assign the given optionValue to the specified option.
        /// </summary>
        /// <param name="option">a ZmqSocketOption that specifies what to set</param>
        /// <param name="optionValue">an Object that is the value to set that option to</param>
        /// <exception cref="InvalidException">The option and optionValue must be valid.</exception>
        public void SetSocketOption(ZmqSocketOption option, object? optionValue)
        {
            T Get<T>() => optionValue is T v ? v : throw new ArgumentException($"Value for option {option} have type {typeof(T).Name}.", nameof(optionValue));

            switch (option)
            {
                case ZmqSocketOption.SendHighWatermark:
                    SendHighWatermark = Get<int>();
                    break;

                case ZmqSocketOption.ReceiveHighWatermark:
                    ReceiveHighWatermark = Get<int>();
                    break;

                case ZmqSocketOption.SendLowWatermark:
                    SendLowWatermark = Get<int>();
                    break;

                case ZmqSocketOption.ReceiveLowWatermark:
                    ReceiveLowWatermark = Get<int>();
                    break;

                case ZmqSocketOption.Affinity:
                    Affinity = Get<long>();
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
                    break;

                case ZmqSocketOption.Rate:
                    Rate = Get<int>();
                    break;

                case ZmqSocketOption.RecoveryIvl:
                    RecoveryIvl = Get<int>();
                    break;

                case ZmqSocketOption.SendBuffer:
                    SendBuffer = Get<int>();
                    break;

                case ZmqSocketOption.ReceiveBuffer:
                    ReceiveBuffer = Get<int>();
                    break;

                case ZmqSocketOption.Linger:
                    Linger = Get<int>();
                    break;

                case ZmqSocketOption.ReconnectIvl:
                    var reconnectIvl = Get<int>();
                    if (reconnectIvl < -1)
                        throw new InvalidException($"Options.SetSocketOption(ReconnectIvl, {reconnectIvl}) optionValue must be >= -1.");
                    ReconnectIvl = reconnectIvl;
                    break;

                case ZmqSocketOption.ReconnectIvlMax:
                    var reconnectIvlMax = Get<int>();
                    if (reconnectIvlMax < 0)
                        throw new InvalidException($"Options.SetSocketOption(ReconnectIvlMax, {reconnectIvlMax}) optionValue must be non-negative.");
                    ReconnectIvlMax = reconnectIvlMax;
                    break;

                case ZmqSocketOption.Backlog:
                    Backlog = Get<int>();
                    break;

                case ZmqSocketOption.MaxMessageSize:
                    MaxMessageSize = Get<long>();
                    break;

                case ZmqSocketOption.MulticastHops:
                    MulticastHops = Get<int>();
                    break;

                case ZmqSocketOption.SendTimeout:
                    SendTimeout = Get<int>();
                    break;

                case ZmqSocketOption.IPv4Only:
                    IPv4Only = Get<bool>();
                    break;

                case ZmqSocketOption.TcpKeepalive:
                    var tcpKeepalive = Get<int>();
                    if (tcpKeepalive != -1 && tcpKeepalive != 0 && tcpKeepalive != 1)
                        throw new InvalidException($"Options.SetSocketOption(TcpKeepalive, {tcpKeepalive}) optionValue is neither -1, 0, nor 1.");
                    TcpKeepalive = tcpKeepalive;
                    break;

                case ZmqSocketOption.DelayAttachOnConnect:
                    DelayAttachOnConnect = Get<bool>();
                    break;

                case ZmqSocketOption.TcpKeepaliveIdle:
                    TcpKeepaliveIdle = Get<int>();
                    break;

                case ZmqSocketOption.TcpKeepaliveIntvl:
                    TcpKeepaliveIntvl = Get<int>();
                    break;

                case ZmqSocketOption.Endian:
                    Endian = Get<Endianness>();
                    break;

                case ZmqSocketOption.DisableTimeWait:
                    DisableTimeWait = Get<bool>();
                    break;

                case ZmqSocketOption.PgmMaxTransportServiceDataUnitLength:
                    PgmMaxTransportServiceDataUnitLength = Get<int>();
                    break;

                case ZmqSocketOption.HeartbeatInterval:
                    HeartbeatInterval = Get<int>();
                    break;

                case ZmqSocketOption.HeartbeatTtl:
                    // Convert this to deciseconds from milliseconds
                    HeartbeatTtl = Get<int>();
                    HeartbeatTtl /= 100;
                    break;

                case ZmqSocketOption.HeartbeatTimeout:
                    HeartbeatTimeout = Get<int>();
                    break;

                case ZmqSocketOption.CurveServer:
                    AsServer = Get<bool>();
                    Mechanism = AsServer ? MechanismType.Curve : MechanismType.Null;
                    break;
                
                case ZmqSocketOption.CurvePublicKey:
                {
                    var key = Get<byte[]>();
                    if (key.Length != 32)
                        throw new InvalidException("Curve key size must be 32 bytes");
                    Mechanism = MechanismType.Curve;
                    Buffer.BlockCopy(key, 0, CurvePublicKey, 0, 32);
                    break;
                }

                case ZmqSocketOption.CurveSecretKey:
                {
                    var key = Get<byte[]>();
                    if (key.Length != 32)
                        throw new InvalidException("Curve key size must be 32 bytes");
                    Mechanism = MechanismType.Curve;
                    Buffer.BlockCopy(key, 0, CurveSecretKey, 0, 32);
                    break;
                }

                case ZmqSocketOption.CurveServerKey:
                {
                    var key = Get<byte[]>();
                    if (key.Length != 32)
                        throw new InvalidException("Curve key size must be 32 bytes");
                    Mechanism = MechanismType.Curve;
                    Buffer.BlockCopy(key, 0, CurveServerKey, 0, 32);
                    break;
                }

                case ZmqSocketOption.HelloMessage:
                {
                    if (optionValue == null)
                    {
                        HelloMsg = null;
                    }
                    else
                    {
                        var helloMsg = Get<byte[]>();
                        HelloMsg = new byte[helloMsg.Length];
                    
                        Buffer.BlockCopy(helloMsg, 0, HelloMsg, 0, helloMsg.Length);
                    }
                    break;
                }

                case ZmqSocketOption.Relaxed:
                    {
                        Relaxed = Get<bool>();
                        break;
                    }

                case ZmqSocketOption.Correlate:
                    {
                        Correlate = Get<bool>();
                        break;
                    }
                
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
        public object? GetSocketOption(ZmqSocketOption option)
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
                    
                case ZmqSocketOption.LastPeerRoutingId:
                    return LastPeerRoutingId;
                
                case ZmqSocketOption.HeartbeatInterval:
                    return HeartbeatInterval;

                case ZmqSocketOption.HeartbeatTtl:
                    return HeartbeatTtl * 100;

                case ZmqSocketOption.HeartbeatTimeout:
                    if (HeartbeatTimeout == -1)
                        return HeartbeatInterval;
                    return HeartbeatTimeout;
                
                case ZmqSocketOption.CurveServer:
                    return Mechanism == MechanismType.Curve && AsServer;
                
                case ZmqSocketOption.CurvePublicKey:
                    return CurvePublicKey;
                    
                case ZmqSocketOption.CurveSecretKey:
                    return CurveSecretKey;
                
                case ZmqSocketOption.CurveServerKey:
                    return CurveServerKey;
                
                default:
                    throw new InvalidException("GetSocketOption called with invalid ZmqSocketOption of " + option);
            }
        }
    }
}