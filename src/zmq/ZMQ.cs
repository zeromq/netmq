/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
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
using System.Threading;
using System.Text;
using zmq;


public class ZMQ
{

	/******************************************************************************/
	/*  0MQ versioning support.                                                   */
	/******************************************************************************/

	/*  Version macros for compile-time API version detection                     */
	public const int ZMQ_VERSION_MAJOR = 3;
	public const int ZMQ_VERSION_MINOR = 2;
	public const int ZMQ_VERSION_PATCH = 2;

	/*  Default for new contexts                                                  */
	public const int ZMQ_IO_THREADS_DFLT = 1;
	public const int ZMQ_MAX_SOCKETS_DFLT = 1024;
	
	public const int ZMQ_POLLIN = 1;
	public const int ZMQ_POLLOUT = 2;
	public const int ZMQ_POLLERR = 4;

	public const int ZMQ_STREAMER = 1;
	public const int ZMQ_FORWARDER = 2;
	public const int ZMQ_QUEUE = 3;

	
	//  New context API
	public static Ctx zmq_ctx_new()
	{
		//  Create 0MQ context.
		Ctx ctx = new Ctx();
		return ctx;
	}

	private static void zmq_ctx_destroy(Ctx ctx_)
	{
		if (ctx_ == null || !ctx_.check_tag())
		{
			throw new InvalidOperationException();
		}

		ctx_.terminate();
	}


	public static void zmq_ctx_set(Ctx ctx_, ZmqContextOption option_, int optval_)
	{
		if (ctx_ == null || !ctx_.check_tag())
		{
			throw new InvalidOperationException();
		}
		ctx_.set(option_, optval_);
	}

	public static int zmq_ctx_get(Ctx ctx_, ZmqContextOption option_)
	{
		if (ctx_ == null || !ctx_.check_tag())
		{
			throw new InvalidOperationException();
		}
		return ctx_.get(option_);
	}


	//  Stable/legacy context API
	public static Ctx zmq_init(int io_threads_)
	{
		if (io_threads_ >= 0)
		{
			Ctx ctx = zmq_ctx_new();
			zmq_ctx_set(ctx, ZmqContextOption.ZMQ_IO_THREADS, io_threads_);
			return ctx;
		}
		throw new ArgumentException("io_threds must not be negative");
	}

	public static void zmq_term(Ctx ctx_)
	{
		zmq_ctx_destroy(ctx_);
	}

	// Sockets
	public static SocketBase zmq_socket(Ctx ctx_, ZmqSocketType type_)
	{
		if (ctx_ == null || !ctx_.check_tag())
		{
			throw new InvalidOperationException();
		}
		SocketBase s = ctx_.create_socket(type_);
		return s;
	}

	public static void zmq_close(SocketBase s_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}
		s_.close();
	}

	public static void zmq_setsockopt(SocketBase s_, ZmqSocketOptions option_, Object optval_)
	{

		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}

		s_.setsockopt(option_, optval_);

	}

	public static Object zmq_getsockoptx(SocketBase s_, ZmqSocketOptions option_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}

		return s_.getsockoptx(option_);
	}

	public static int zmq_getsockopt(SocketBase s_, ZmqSocketOptions opt)
	{

		return s_.getsockopt(opt);
	}

	public static bool zmq_socket_monitor(SocketBase s_, String addr_, ZmqSocketEvent events_)
	{

		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}

		return s_.monitor(addr_, events_);
	}


	public static bool zmq_bind(SocketBase s_, String addr_)
	{

		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}

		return s_.bind(addr_);
	}

	public static bool zmq_connect(SocketBase s_, String addr_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}
		return s_.connect(addr_);
	}

	public static bool zmq_unbind(SocketBase s_, String addr_)
	{

		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}
		return s_.term_endpoint(addr_);
	}

	public static bool zmq_disconnect(SocketBase s_, String addr_)
	{

		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}
		return s_.term_endpoint(addr_);
	}

	// Sending functions.
	public static int zmq_send(SocketBase s_, String str, ZmqSendRecieveOptions flags_)
	{
		byte[] data = Encoding.ASCII.GetBytes(str);
		return zmq_send(s_, data, data.Length, flags_);
	}

	public static int zmq_send(SocketBase s_, Msg msg, ZmqSendRecieveOptions flags_)
	{

		int rc = s_sendmsg(s_, msg, flags_);
		if (rc < 0)
		{
			return -1;
		}

		return rc;
	}

	public static int zmq_send(SocketBase s_, byte[] buf_, int len_, ZmqSendRecieveOptions flags_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}

		Msg msg = new Msg(len_);
		msg.put(buf_, 0, len_);

		int rc = s_sendmsg(s_, msg, flags_);
		if (rc < 0)
		{
			return -1;
		}

		return rc;
	}

	// Send multiple messages.
	//
	// If flag bit ZMQ_SNDMORE is set the vector is treated as
	// a single multi-part message, i.e. the last message has
	// ZMQ_SNDMORE bit switched off.
	//
	public int zmq_sendiov(SocketBase s_, byte[][] a_, int count_, ZmqSendRecieveOptions flags_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}
		int rc = 0;
		Msg msg;

		for (int i = 0; i < count_; ++i)
		{
			msg = new Msg(a_[i]);
			if (i == count_ - 1)
				flags_ = flags_ & ~ZmqSendRecieveOptions.ZMQ_SNDMORE;
			rc = s_sendmsg(s_, msg, flags_);
			if (rc < 0)
			{
				rc = -1;
				break;
			}
		}
		return rc;

	}

	private static int s_sendmsg(SocketBase s_, Msg msg_, ZmqSendRecieveOptions flags_)
	{
		int sz = zmq_msg_size(msg_);
		bool rc = s_.send(msg_, flags_);
		if (!rc)
			return -1;
		return sz;
	}


	// Receiving functions.

	public static Msg zmq_recv(SocketBase s_, ZmqSendRecieveOptions flags_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}
		Msg msg = s_recvmsg(s_, flags_);
		if (msg == null)
		{
			return null;
		}

		//  At the moment an oversized message is silently truncated.
		//  TODO: Build in a notification mechanism to report the overflows.
		//int to_copy = nbytes < len_ ? nbytes : len_;

		return msg;
	}

	// Receive a multi-part message
	// 
	// Receives up to *count_ parts of a multi-part message.
	// Sets *count_ to the actual number of parts read.
	// ZMQ_RCVMORE is set to indicate if a complete multi-part message was read.
	// Returns number of message parts read, or -1 on error.
	//
	// Note: even if -1 is returned, some parts of the message
	// may have been read. Therefore the client must consult
	// *count_ to retrieve message parts successfully read,
	// even if -1 is returned.
	//
	// The iov_base* buffers of each iovec *a_ filled in by this 
	// function may be freed using free().
	//
	// Implementation note: We assume zmq::msg_t buffer allocated
	// by zmq::recvmsg can be freed by free().
	// We assume it is safe to steal these buffers by simply
	// not closing the zmq::msg_t.
	//
	public int zmq_recviov(SocketBase s_, byte[][] a_, int count_, ZmqSendRecieveOptions flags_)
	{
		if (s_ == null || !s_.check_tag())
		{
			throw new InvalidOperationException();
		}

		int nread = 0;
		bool recvmore = true;

		for (int i = 0; recvmore && i < count_; ++i)
		{
			// Cheat! We never close any msg
			// because we want to steal the buffer.
			Msg msg = s_recvmsg(s_, flags_);
			if (msg == null)
			{
				nread = -1;
				break;
			}

			// Cheat: acquire zmq_msg buffer.
			a_[i] = msg.get_data();

			// Assume zmq_socket ZMQ_RVCMORE is properly set.
			recvmore = msg.has_more();
		}
		return nread;
	}


	public static Msg s_recvmsg(SocketBase s_, ZmqSendRecieveOptions flags_)
	{
		return s_.recv(flags_);
	}

	public static Msg zmq_msg_init()
	{
		return new Msg();
	}

	public static Msg zmq_msg_init_size(int message_size)
	{
		return new Msg(message_size);
	}

	public static int zmq_msg_size(Msg msg_)
	{
		return msg_.size;
	}

	public static Msg zmq_recvmsg(SocketBase s_, ZmqSendRecieveOptions flags_)
	{
		return zmq_recv(s_, flags_);
	}

	public static int zmq_sendmsg(SocketBase s_, Msg msg_, ZmqSendRecieveOptions flags_)
	{
		return zmq_send(s_, msg_, flags_);
	}

	public static int zmq_msg_get(Msg msg_)
	{
		return zmq_msg_get(msg_, MsgFlags.More);
	}

	public static int zmq_msg_get(Msg msg_, MsgFlags option_)
	{
		switch (option_)
		{
			case MsgFlags.More:
				return msg_.has_more() ? 1 : 0;
			default:
				throw new ArgumentException();
		}
	}

	public static void zmq_sleep(int s)
	{
		Thread.Sleep(s * (1000));
	}

	////  The proxy functionality
	//public static bool zmq_proxy(SocketBase frontend_, SocketBase backend_, SocketBase control_)
	//{
	//    if (frontend_ == null || backend_ == null)
	//    {
	//        ZError.errno = (ZError.EFAULT);
	//        throw new ArgumentException();
	//    }
	//    return Proxy.proxy(
	//        frontend_,
	//        backend_,
	//        control_);
	//}

	//[Obsolete]
	//public static bool zmq_device(int device_, SocketBase insocket_,
	//        SocketBase outsocket_)
	//{
	//    return Proxy.proxy(insocket_, outsocket_, null);
	//}

	// Polling.
	public static int zmq_poll(PollItem[] items_, long timeout_)
	{
		throw new NotImplementedException();
	}

	public static int zmq_poll(PollItem[] items_, int timeout_)
	{
		//if (items_ == null) 
		//{
		//    ZError.errno = (ZError.EFAULT);
		//    throw new ArgumentException();
		//}
		//if (items_.Length == 0) {
		//    if (timeout_ == 0)
		//        return 0;
		//    Thread.Sleep(timeout_);
		//    return 0;
		//}

		//long now = 0;
		//long end = 0;

		//foreach (PollItem item in items_)
		//{
		//    if (item.socket != null)
		//    {

		//    }
		//}

		throw new NotImplementedException();
	}

	//public static long zmq_stopwatch_start() {
	//    return System.nanoTime();
	//}

	//public static long zmq_stopwatch_stop(long watch) {
	//    return (System.nanoTime() - watch) / 1000;
	//}

	public static int ZMQ_MAKE_VERSION(int major, int minor, int patch)
	{
		return ((major) * 10000 + (minor) * 100 + (patch));
	}

	public static String zmq_strerror(int errno)
	{
		return "Errno = " + errno;
	}
}
