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

    /*  Context options  */
    public const int ZMQ_IO_THREADS = 1;
    public const int ZMQ_MAX_SOCKETS = 2;

    /*  Default for new contexts                                                  */
    public const int ZMQ_IO_THREADS_DFLT = 1;
    public const int ZMQ_MAX_SOCKETS_DFLT = 1024;



    /******************************************************************************/
    /*  0MQ socket definition.                                                    */
    /******************************************************************************/

    /*  Socket types.                                                             */
    public const int ZMQ_PAIR = 0;
    public const int ZMQ_PUB = 1;
    public const int ZMQ_SUB = 2;
    public const int ZMQ_REQ = 3;
    public const int ZMQ_REP = 4;
    public const int ZMQ_DEALER = 5;
    public const int ZMQ_ROUTER = 6;
    public const int ZMQ_PULL = 7;
    public const int ZMQ_PUSH = 8;
    public const int ZMQ_XPUB = 9;
    public const int ZMQ_XSUB = 10;

    /*  Deprecated aliases                                                        */
    [Obsolete]
    public const int ZMQ_XREQ = ZMQ_DEALER;
    [Obsolete]
    public const int ZMQ_XREP = ZMQ_ROUTER;

    /*  Socket options.                                                           */
    public const int ZMQ_AFFINITY = 4;
    public const int ZMQ_IDENTITY = 5;
    public const int ZMQ_SUBSCRIBE = 6;
    public const int ZMQ_UNSUBSCRIBE = 7;
    public const int ZMQ_RATE = 8;
    public const int ZMQ_RECOVERY_IVL = 9;
    public const int ZMQ_SNDBUF = 11;
    public const int ZMQ_RCVBUF = 12;
    public const int ZMQ_RCVMORE = 13;
    public const int ZMQ_FD = 14;
    public const int ZMQ_EVENTS = 15;
    public const int ZMQ_TYPE = 16;
    public const int ZMQ_LINGER = 17;
    public const int ZMQ_RECONNECT_IVL = 18;
    public const int ZMQ_BACKLOG = 19;
    public const int ZMQ_RECONNECT_IVL_MAX = 21;
    public const int ZMQ_MAXMSGSIZE = 22;
    public const int ZMQ_SNDHWM = 23;
    public const int ZMQ_RCVHWM = 24;
    public const int ZMQ_MULTICAST_HOPS = 25;
    public const int ZMQ_RCVTIMEO = 27;
    public const int ZMQ_SNDTIMEO = 28;
    public const int ZMQ_IPV4ONLY = 31;
    public const int ZMQ_LAST_ENDPOINT = 32;
    public const int ZMQ_ROUTER_MANDATORY = 33;
    public const int ZMQ_TCP_KEEPALIVE = 34;
    public const int ZMQ_TCP_KEEPALIVE_CNT = 35;
    public const int ZMQ_TCP_KEEPALIVE_IDLE = 36;
    public const int ZMQ_TCP_KEEPALIVE_INTVL = 37;
    public const int ZMQ_TCP_ACCEPT_FILTER = 38;
    public const int ZMQ_DELAY_ATTACH_ON_CONNECT = 39;
    public const int ZMQ_XPUB_VERBOSE = 40;

    /* Custom options */
    public const int ZMQ_ENCODER = 1001;
    public const int ZMQ_DECODER = 1002;

    /*  Message options                                                           */

    public const int ZMQ_MORE = 1;

    /*  Send/recv options.                                                        */
    public const int ZMQ_DONTWAIT = 1;
    public const int ZMQ_SNDMORE = 2;

    /*  Deprecated aliases                                                        */
    public const int ZMQ_NOBLOCK = ZMQ_DONTWAIT;
    public const int ZMQ_FAIL_UNROUTABLE = ZMQ_ROUTER_MANDATORY;
    public const int ZMQ_ROUTER_BEHAVIOR = ZMQ_ROUTER_MANDATORY;

    /******************************************************************************/
    /*  0MQ socket events and monitoring                                          */
    /******************************************************************************/

    /*  Socket transport events (tcp and ipc only)                                */
    public const int ZMQ_EVENT_CONNECTED = 1;
    public const int ZMQ_EVENT_CONNECT_DELAYED = 2;
    public const int ZMQ_EVENT_CONNECT_RETRIED = 4;
    public const int ZMQ_EVENT_CONNECT_FAILED = 1024;

    public const int ZMQ_EVENT_LISTENING = 8;
    public const int ZMQ_EVENT_BIND_FAILED = 16;

    public const int ZMQ_EVENT_ACCEPTED = 32;
    public const int ZMQ_EVENT_ACCEPT_FAILED = 64;

    public const int ZMQ_EVENT_CLOSED = 128;
    public const int ZMQ_EVENT_CLOSE_FAILED = 256;
    public const int ZMQ_EVENT_DISCONNECTED = 512;

    public const int ZMQ_EVENT_ALL = ZMQ_EVENT_CONNECTED | ZMQ_EVENT_CONNECT_DELAYED |
                ZMQ_EVENT_CONNECT_RETRIED | ZMQ_EVENT_LISTENING |
                ZMQ_EVENT_BIND_FAILED | ZMQ_EVENT_ACCEPTED |
                ZMQ_EVENT_ACCEPT_FAILED | ZMQ_EVENT_CLOSED |
                ZMQ_EVENT_CLOSE_FAILED | ZMQ_EVENT_DISCONNECTED;

    public const int ZMQ_POLLIN = 1;
    public const int ZMQ_POLLOUT = 2;
    public const int ZMQ_POLLERR = 4;

    public const int ZMQ_STREAMER = 1;
    public const int ZMQ_FORWARDER = 2;
    public const int ZMQ_QUEUE = 3;

    public class Event
    {
        private static int VALUE_INTEGER = 1;
        private static int VALUE_CHANNEL = 2;

        public int @event;
        public String addr;
        private Object arg;
        private int flag;

        public Event(int @event, String addr, Object arg)
        {
            this.@event = @event;
            this.addr = addr;
            this.arg = arg;
            if (arg is int)
                flag = VALUE_INTEGER;
            else if (arg is System.Net.Sockets.Socket)
                flag = VALUE_CHANNEL;
            else
                flag = 0;
        }

        public bool write(SocketBase s)
        {
            int size = 4 + 1 + addr.Length + 1; // event + len(addr) + addr + flag
            if (flag == VALUE_INTEGER)
                size += 4;

            int pos = 0;

            ByteArraySegment buffer = new byte[size];
            buffer.PutInteger(@event, pos);
            pos += 4;
            buffer[pos++] = (byte)addr.Length;

            // was not here originally

            buffer.PutString(addr, pos);
            pos += addr.Length;

            buffer[pos++] = ((byte)flag);
            if (flag == VALUE_INTEGER)
                buffer.PutInteger((int)arg, pos);
            pos += 4;

            Msg msg = new Msg((byte[]) buffer);
            return s.send(msg, 0);
        }

        public static Event read(SocketBase s)
        {
            Msg msg = s.recv(0);
            if (msg == null)
                return null;

            int pos = 0;
            byte[] data = msg.get_data();

            int @event = BitConverter.ToInt32(data, pos);
            pos += 4;
            int len = (int)data[pos++];
            string addr = Encoding.ASCII.GetString(data, pos, len);
            pos += len;
            int flag = (int)data[pos++];
            Object arg = null;

            if (flag == VALUE_INTEGER)
                arg = BitConverter.ToInt32(data, pos);

            return new Event(@event, addr, arg);
        }
    }
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


    public static void zmq_ctx_set(Ctx ctx_, int option_, int optval_)
    {
        if (ctx_ == null || !ctx_.check_tag())
        {
            throw new InvalidOperationException();
        }
        ctx_.set(option_, optval_);
    }

    public static int zmq_ctx_get(Ctx ctx_, int option_)
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
            zmq_ctx_set(ctx, ZMQ_IO_THREADS, io_threads_);
            return ctx;
        }
        throw new ArgumentException("io_threds must not be negative");
    }

    public static void zmq_term(Ctx ctx_)
    {
        zmq_ctx_destroy(ctx_);
    }

    // Sockets
    public static SocketBase zmq_socket(Ctx ctx_, int type_)
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

    public static void zmq_setsockopt(SocketBase s_, int option_, Object optval_)
    {

        if (s_ == null || !s_.check_tag())
        {
            throw new InvalidOperationException();
        }

        s_.setsockopt(option_, optval_);

    }

    public static Object zmq_getsockoptx(SocketBase s_, int option_)
    {
        if (s_ == null || !s_.check_tag())
        {
            throw new InvalidOperationException();
        }

        return s_.getsockoptx(option_);
    }

    public static int zmq_getsockopt(SocketBase s_, int opt)
    {

        return s_.getsockopt(opt);
    }

    public static bool zmq_socket_monitor(SocketBase s_, String addr_, int events_)
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
    public static int zmq_send(SocketBase s_, String str,
            int flags_)
    {
        byte[] data = Encoding.ASCII.GetBytes(str);
        return zmq_send(s_, data, data.Length, flags_);
    }

    public static int zmq_send(SocketBase s_, Msg msg,
            int flags_)
    {

        int rc = s_sendmsg(s_, msg, flags_);
        if (rc < 0)
        {
            return -1;
        }

        return rc;
    }

    public static int zmq_send(SocketBase s_, byte[] buf_, int len_,
            int flags_)
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
    public int zmq_sendiov(SocketBase s_, byte[][] a_, int count_, int flags_)
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
                flags_ = flags_ & ~ZMQ_SNDMORE;
            rc = s_sendmsg(s_, msg, flags_);
            if (rc < 0)
            {
                rc = -1;
                break;
            }
        }
        return rc;

    }

    private static int s_sendmsg(SocketBase s_, Msg msg_, int flags_)
    {
        int sz = zmq_msg_size(msg_);
        bool rc = s_.send(msg_, flags_);
        if (!rc)
            return -1;
        return sz;
    }


    // Receiving functions.

    public static Msg zmq_recv(SocketBase s_, int flags_)
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
    public int zmq_recviov(SocketBase s_, byte[][] a_, int count_, int flags_)
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


    public static Msg s_recvmsg(SocketBase s_, int flags_)
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

    public static Msg zmq_recvmsg(SocketBase s_, int flags_)
    {
        return zmq_recv(s_, flags_);
    }

    public static int zmq_sendmsg(SocketBase s_, Msg msg_, int flags_)
    {
        return zmq_send(s_, msg_, flags_);
    }

    public static int zmq_msg_get(Msg msg_)
    {
        return zmq_msg_get(msg_, ZMQ_MORE);
    }

    public static int zmq_msg_get(Msg msg_, int option_)
    {
        switch (option_)
        {
            case ZMQ_MORE:
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
