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

public class Options
{
	public Options()
	{
		SendHighWatermark = 1000;
		ReceiveHighWatermark = 1000;
		Affinity = 0;
		IdentitySize = 0;
		Rate = 100;
		RecoveryIvl = 10000;
		MulticastHops = 1;
		SendBuffer = 0;
		ReceiveBuffer = 0;
		SocketType = ZmqSocketType.ZMQ_NONE;
		Linger = -1;
		ReconnectIvl = 100;
		ReconnectIvlMax = 0;
		Backlog = 100;
		Maxmsgsize = -1;
		ReceiveTimeout = -1;
		SendTimeout = -1;
		IPv4Only = 1;
		DelayAttachOnConnect = 0;
		DelayOnClose = true;
		DelayOnDisconnect = true;
		Filter = false;
		RecvIdentity = false;
		TcpKeepalive = -1;
		TcpKeepaliveCnt = -1;
		TcpKeepaliveIdle = -1;
		TcpKeepaliveIntvl = -1;
		SocketId = 0;

		Identity = null;
		TcpAcceptFilters = new List<TcpAddress.TcpAddressMask>();
	}

	//  High-water marks for message pipes.
	public int SendHighWatermark { get; set; }
	public int ReceiveHighWatermark { get; set; }

	//  I/O thread affinity.
	public long Affinity { get; set; }

	//  Socket identity
	public byte IdentitySize { get; set; }
	public byte[] Identity { get; set; } // [256];

	// Last socket endpoint resolved URI
	public String LastEndpoint { get; set; }

	//  Maximum tranfer rate [kb/s]. Default 100kb/s.
	public int Rate { get; set; }

	//  Reliability time interval [ms]. Default 10 seconds.
	public int RecoveryIvl { get; set; }

	// Sets the time-to-live field in every multicast packet sent.
	public int MulticastHops { get; set; }

	// SO_SNDBUF and SO_RCVBUF to be passed to underlying transport sockets.
	public int SendBuffer { get; set; }
	public int ReceiveBuffer { get; set; }

	//  Socket type.
	public ZmqSocketType SocketType { get; set; }

	//  Linger time, in milliseconds.
	public int Linger { get; set; }

	//  Minimum interval between attempts to reconnect, in milliseconds.
	//  Default 100ms
	public int ReconnectIvl { get; set; }
	//  Maximum interval between attempts to reconnect, in milliseconds.
	//  Default 0 (unused)
	public int ReconnectIvlMax { get; set; }

	//  Maximum backlog for pending connections.
	public int Backlog { get; set; }

	//  Maximal size of message to handle.
	public long Maxmsgsize { get; set; }

	// The timeout for send/recv operations for this socket.
	public int ReceiveTimeout { get; set; }
	public int SendTimeout { get; set; }

	//  If 1, indicates the use of IPv4 sockets only, it will not be
	//  possible to communicate with IPv6-only hosts. If 0, the socket can
	//  connect to and accept connections from both IPv4 and IPv6 hosts.
	public int IPv4Only { get; set; }

	//  If 1, connecting pipes are not attached immediately, meaning a send()
	//  on a socket with only connecting pipes would block
	public int DelayAttachOnConnect { get; set; }

	//  If true, session reads all the pending messages from the pipe and
	//  sends them to the network when socket is closed.
	public bool DelayOnClose { get; set; }

	//  If true, socket reads all the messages from the pipe and delivers
	//  them to the user when the peer terminates.
	public bool DelayOnDisconnect { get; set; }

	//  If 1, (X)SUB socket should filter the messages. If 0, it should not.
	public bool Filter { get; set; }

	//  If true, the identity message is forwarded to the socket.
	public bool RecvIdentity { get; set; }

	//  TCP keep-alive settings.
	//  Defaults to -1 = do not change socket options
	public int TcpKeepalive { get; set; }
	public int TcpKeepaliveCnt { get; set; }
	public int TcpKeepaliveIdle { get; set; }
	public int TcpKeepaliveIntvl { get; set; }

	// TCP accept() filters
	//typedef std::vector <tcp_address_mask_t> tcp_accept_filters_t;
	public List<TcpAddress.TcpAddressMask> TcpAcceptFilters;

	//  ID of the socket.
	public int SocketId { get; set; }


	public bool SetSocketOption(ZmqSocketOptions option_, Object optval_)
	{
		switch (option_)
		{
			case ZmqSocketOptions.ZMQ_SNDHWM:
				SendHighWatermark = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_RCVHWM:
				ReceiveHighWatermark = (int) optval_;
				return true;


			case ZmqSocketOptions.ZMQ_AFFINITY:
				Affinity = (long) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_IDENTITY:
				byte[] val;

				if (optval_ is String)
					val = Encoding.ASCII.GetBytes((String) optval_);
				else if (optval_ is byte[])
					val = (byte[]) optval_;
				else
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}

				if (val == null || val.Length > 255)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}
				Identity = new byte[val.Length];
				val.CopyTo(Identity, 0);
				IdentitySize = (byte) Identity.Length;
				return true;

			case ZmqSocketOptions.ZMQ_RATE:
				Rate = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_RECOVERY_IVL:
				RecoveryIvl = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_SNDBUF:
				SendBuffer = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_RCVBUF:
				ReceiveBuffer = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_LINGER:
				Linger = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_RECONNECT_IVL:
				ReconnectIvl = (int) optval_;

				if (ReconnectIvl < -1)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}

				return true;

			case ZmqSocketOptions.ZMQ_RECONNECT_IVL_MAX:
				ReconnectIvlMax = (int) optval_;

				if (ReconnectIvlMax < 0)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}

				return true;

			case ZmqSocketOptions.ZMQ_BACKLOG:
				Backlog = (int) optval_;
				return true;


			case ZmqSocketOptions.ZMQ_MAXMSGSIZE:
				Maxmsgsize = (long) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_MULTICAST_HOPS:
				MulticastHops = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_RCVTIMEO:
				ReceiveTimeout = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_SNDTIMEO:
				SendTimeout = (int) optval_;
				return true;

			case ZmqSocketOptions.ZMQ_IPV4ONLY:

				IPv4Only = (int) optval_;
				if (IPv4Only != 0 && IPv4Only != 1)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}
				return true;

			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE:

				TcpKeepalive = (int) optval_;
				if (TcpKeepalive != -1 && TcpKeepalive != 0 && TcpKeepalive != 1)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}
				return true;

			case ZmqSocketOptions.ZMQ_DELAY_ATTACH_ON_CONNECT:

				DelayAttachOnConnect = (int) optval_;
				if (DelayAttachOnConnect != 0 && DelayAttachOnConnect != 1)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}
				return true;

			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE_CNT:
			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE_IDLE:
			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE_INTVL:
				// not supported
				return true;

			case ZmqSocketOptions.ZMQ_TCP_ACCEPT_FILTER:
				String filter_str = (String) optval_;
				if (filter_str == null)
				{
					TcpAcceptFilters.Clear();
				}
				else if (filter_str.Length == 0 || filter_str.Length > 255)
				{
					ZError.errno = (ZError.EINVAL);
					return false;
				}
				else
				{
					TcpAddress.TcpAddressMask filter = new TcpAddress.TcpAddressMask();
					filter.resolve(filter_str, IPv4Only == 1 ? true : false);
					TcpAcceptFilters.Add(filter);
				}
				return true;

			default:
				ZError.errno = (ZError.EINVAL);
				return false;
		}
	}


	public Object GetSocketOption(ZmqSocketOptions option_)
	{
		switch (option_)
		{
			case ZmqSocketOptions.ZMQ_SNDHWM:
				return SendHighWatermark;

			case ZmqSocketOptions.ZMQ_RCVHWM:
				return ReceiveHighWatermark;

			case ZmqSocketOptions.ZMQ_AFFINITY:
				return Affinity;

			case ZmqSocketOptions.ZMQ_IDENTITY:
				return Identity;

			case ZmqSocketOptions.ZMQ_RATE:
				return Rate;

			case ZmqSocketOptions.ZMQ_RECOVERY_IVL:
				return RecoveryIvl;

			case ZmqSocketOptions.ZMQ_SNDBUF:
				return SendBuffer;

			case ZmqSocketOptions.ZMQ_RCVBUF:
				return ReceiveBuffer;

			case ZmqSocketOptions.ZMQ_TYPE:
				return SocketType;

			case ZmqSocketOptions.ZMQ_LINGER:
				return Linger;

			case ZmqSocketOptions.ZMQ_RECONNECT_IVL:
				return ReconnectIvl;

			case ZmqSocketOptions.ZMQ_RECONNECT_IVL_MAX:
				return ReconnectIvlMax;

			case ZmqSocketOptions.ZMQ_BACKLOG:
				return Backlog;

			case ZmqSocketOptions.ZMQ_MAXMSGSIZE:
				return Maxmsgsize;

			case ZmqSocketOptions.ZMQ_MULTICAST_HOPS:
				return MulticastHops;

			case ZmqSocketOptions.ZMQ_RCVTIMEO:
				return ReceiveTimeout;

			case ZmqSocketOptions.ZMQ_SNDTIMEO:
				return SendTimeout;

			case ZmqSocketOptions.ZMQ_IPV4ONLY:
				return IPv4Only;

			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE:
				return TcpKeepalive;

			case ZmqSocketOptions.ZMQ_DELAY_ATTACH_ON_CONNECT:
				return DelayAttachOnConnect;

			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE_CNT:
			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE_IDLE:
			case ZmqSocketOptions.ZMQ_TCP_KEEPALIVE_INTVL:
				// not supported
				return 0;

			case ZmqSocketOptions.ZMQ_LAST_ENDPOINT:
				return LastEndpoint;

			default:
				throw new ArgumentException("option=" + option_);
		}
	}
}