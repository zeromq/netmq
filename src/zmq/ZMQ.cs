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
using System.Collections;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace zmq
{
	public class ZMQ
	{

		/******************************************************************************/
		/*  0MQ versioning support.                                                   */
		/******************************************************************************/

		/*  Version macros for compile-time API version detection                     */
		public const int ZmqVersionMajor = 3;
		public const int ZmqVersionMinor = 2;
		public const int ZmqVersionPatch = 2;

		/*  Default for new contexts                                                  */
		public const int ZmqIOThreadsDflt = 1;
		public const int ZmqMaxSocketsDflt = 1024;

		public const int ZmqPollin = 1;
		public const int ZmqPollout = 2;
		public const int ZmqPollerr = 4;

		public const int ZmqStreamer = 1;
		public const int ZmqForwarder = 2;
		public const int ZmqQueue = 3;


		//  New context API
		public static Ctx CtxNew()
		{
			//  Create 0MQ context.
			Ctx ctx = new Ctx();
			return ctx;
		}

		private static void CtxDestroy(Ctx ctx)
		{
			if (ctx == null || !ctx.CheckTag())
			{
				throw new InvalidOperationException();
			}

			ctx.Terminate();
		}


		public static void CtxSet(Ctx ctx, ContextOption option, int optval)
		{
			if (ctx == null || !ctx.CheckTag())
			{
				throw new InvalidOperationException();
			}
			ctx.Set(option, optval);
		}

		public static int CtxGet(Ctx ctx, ContextOption option)
		{
			if (ctx == null || !ctx.CheckTag())
			{
				throw new InvalidOperationException();
			}
			return ctx.Get(option);
		}


		//  Stable/legacy context API
		public static Ctx Init(int ioThreads)
		{
			if (ioThreads >= 0)
			{
				Ctx ctx = CtxNew();
				CtxSet(ctx, ContextOption.IOThreads, ioThreads);
				return ctx;
			}
			throw new ArgumentException("io_threds must not be negative");
		}

		public static void Term(Ctx ctx)
		{
			CtxDestroy(ctx);
		}

		// Sockets
		public static SocketBase Socket(Ctx ctx, ZmqSocketType type)
		{
			if (ctx == null || !ctx.CheckTag())
			{
				throw new InvalidOperationException();
			}
			SocketBase s = ctx.CreateSocket(type);
			return s;
		}

		public static void Close(SocketBase s)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}
			s.Close();
		}

		public static void SetSocketOption(SocketBase s, ZmqSocketOptions option, Object optval)
		{

			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}

			s.SetSocketOption(option, optval);

		}

		public static Object GetSocketOptionX(SocketBase s, ZmqSocketOptions option)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}

			return s.GetSocketOptionX(option);
		}

		public static int GetSocketOption(SocketBase s, ZmqSocketOptions opt)
		{

			return s.GetSocketOption(opt);
		}

		public static bool SocketMonitor(SocketBase s, String addr, SocketEvent events)
		{

			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}

			return s.Monitor(addr, events);
		}


		public static bool Bind(SocketBase s, String addr)
		{

			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}

			return s.Bind(addr);
		}

		public static bool Connect(SocketBase s, String addr)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}
			return s.Connect(addr);
		}

		public static bool Unbind(SocketBase s, String addr)
		{

			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}
			return s.TermEndpoint(addr);
		}

		public static bool Disconnect(SocketBase s, String addr)
		{

			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}
			return s.TermEndpoint(addr);
		}

		// Sending functions.
		public static int Send(SocketBase s, String str, SendRecieveOptions flags)
		{
			byte[] data = Encoding.ASCII.GetBytes(str);
			return Send(s, data, data.Length, flags);
		}

		public static int Send(SocketBase s, Msg msg, SendRecieveOptions flags)
		{

			int rc = SendMsg(s, msg, flags);
			if (rc < 0)
			{
				return -1;
			}

			return rc;
		}

		public static int Send(SocketBase s, byte[] buf, int len, SendRecieveOptions flags)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}

			Msg msg = new Msg(len);
			msg.Put(buf, 0, len);

			int rc = SendMsg(s, msg, flags);
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
		public int SendIOv(SocketBase s, byte[][] a, int count, SendRecieveOptions flags)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}
			int rc = 0;
			Msg msg;

			for (int i = 0; i < count; ++i)
			{
				msg = new Msg(a[i]);
				if (i == count - 1)
					flags = flags & ~SendRecieveOptions.SendMore;
				rc = SendMsg(s, msg, flags);
				if (rc < 0)
				{
					rc = -1;
					break;
				}
			}
			return rc;

		}

		private static int SendMsg(SocketBase s, Msg msg, SendRecieveOptions flags)
		{
			int sz = MsgSize(msg);
			bool rc = s.Send(msg, flags);
			if (!rc)
				return -1;
			return sz;
		}


		// Receiving functions.

		public static Msg Recv(SocketBase s, SendRecieveOptions flags)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}
			Msg msg = RecvMsg(s, flags);
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
		public int RecvIOv(SocketBase s, byte[][] a, int count, SendRecieveOptions flags)
		{
			if (s == null || !s.CheckTag())
			{
				throw new InvalidOperationException();
			}

			int nread = 0;
			bool recvmore = true;

			for (int i = 0; recvmore && i < count; ++i)
			{
				// Cheat! We never close any msg
				// because we want to steal the buffer.
				Msg msg = RecvMsg(s, flags);
				if (msg == null)
				{
					nread = -1;
					break;
				}

				// Cheat: acquire zmq_msg buffer.
				a[i] = msg.Data;

				// Assume zmq_socket ZMQ_RVCMORE is properly set.
				recvmore = msg.HasMore;
			}
			return nread;
		}


		public static Msg RecvMsg(SocketBase s, SendRecieveOptions flags)
		{
			return s.Recv(flags);
		}

		public static Msg MsgInit()
		{
			return new Msg();
		}

		public static Msg MsgInitSize(int messageSize)
		{
			return new Msg(messageSize);
		}

		public static int MsgSize(Msg msg)
		{
			return msg.Size;
		}

		public static int MsgGet(Msg msg)
		{
			return ZmqMsgGet(msg, MsgFlags.More);
		}

		public static int ZmqMsgGet(Msg msg, MsgFlags option)
		{
			switch (option)
			{
				case MsgFlags.More:
					return msg.HasMore ? 1 : 0;
				default:
					throw new ArgumentException();
			}
		}

		public static void Sleep(int s)
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
		
		public static int Poll(PollItem[] items, int timeout)
		{
			if (items == null)
			{
				ZError.ErrorNumber = (ErrorNumber.EFAULT);
				throw new ArgumentException();
			}
			if (items.Length == 0)
			{
				if (timeout == 0)
					return 0;
				Thread.Sleep(timeout);
				return 0;
			}

			bool firstPass = true;		            

			Socket[] writeList =
					items.Where(p => (p.Events & PollEvents.PollOut) == PollEvents.PollOut).Select(
						p => p.Socket != null ? p.Socket.FD : p.FileDescriptor).ToArray();

			Socket[] readList =
				items.Where(p => (p.Events & PollEvents.PollOut) == PollEvents.PollIn).Select(
					p => p.Socket != null ? p.Socket.FD : p.FileDescriptor).ToArray();

			Socket[] errorList =
				items.Where(p => (p.Events & PollEvents.PollOut) == PollEvents.PollError).Select(
					p => p.Socket != null ? p.Socket.FD : p.FileDescriptor).ToArray();

			Socket[] inset = new Socket[writeList.Length];
			Socket[] outset = new Socket[readList.Length];
			Socket[] errorset = new Socket[errorList.Length];

			Dictionary<Socket, PollItem> socketsToItems = items.Select(
				p => new {Socket = p.Socket != null ? p.Socket.FD : p.FileDescriptor, Item = p}).
				ToDictionary(i => i.Socket, i => i.Item);			

            Stopwatch stopwatch = null;

			while (true)
			{				
                int currentTimeoutMicroSeconds;
				
				if (firstPass)
				{					
					currentTimeoutMicroSeconds = 0;
				}
				else
				{
                    currentTimeoutMicroSeconds = (int) ((timeout - stopwatch.ElapsedMilliseconds) % 1000 * 1000);
				}
			
				Buffer.BlockCopy(readList, 0, inset, 0, readList.Length );
				Buffer.BlockCopy(writeList, 0, inset, 0, writeList.Length);
				Buffer.BlockCopy(errorList, 0, inset, 0, errorList.Length);

				try
				{
					System.Net.Sockets.Socket.Select(inset, outset, errorset, currentTimeoutMicroSeconds);
				}
				catch (SocketException ex)
				{
					// TODO: change to right error
					ZError.ErrorNumber = ErrorNumber.ESOCKET;

					return -1;
				}

                foreach (var pollItem in items)
                {
                    pollItem.ResultEvent = PollEvents.None;
                }

				foreach (var socket in inset)
				{
                    if (socket != null)
                    {
                        var item = socketsToItems[socket];

                        if (item.Socket != null)
                        {
                            PollEvents value = (PollEvents)GetSocketOption(item.Socket, ZmqSocketOptions.Events);

                            if (value == PollEvents.PollIn)
                            {
                                item.ResultEvent |= PollEvents.PollIn;
                            }
                        }
                        else
                        {
                            item.ResultEvent |= PollEvents.PollIn;
                        }
                    }
				}

                foreach (var socket in outset)
                {
                    if (socket != null)
                    {
                        var item = socketsToItems[socket];

                        if (item.Socket != null)
                        {
                            PollEvents value = (PollEvents)GetSocketOption(item.Socket, ZmqSocketOptions.Events);

                            if (value == PollEvents.PollOut)
                            {
                                item.ResultEvent |= PollEvents.PollOut;
                            }
                        }
                        else
                        {
                            item.ResultEvent |= PollEvents.PollOut;
                        }
                    }
                }

                foreach (var socket in errorList)
                {
                    if (socket != null)
                    {
                        var item = socketsToItems[socket];

                        if (item.Socket == null)
                        {
                            item.ResultEvent |= PollEvents.PollError;
                        }
                    }
                }

				if (timeout == 0)
				{
					break;					
				}

				if (items.Any(i => i.ResultEvent != PollEvents.None))
				{
					break;
				}

				if (timeout < 0)
				{
					if (firstPass)
					{
						firstPass = false;
					}

					continue;
				}

				if (firstPass)
				{
                    stopwatch = Stopwatch.StartNew();
					firstPass = false;
					continue;
				}

				if (stopwatch.ElapsedMilliseconds > timeout)
				{
					break;					
				}
			}

			return items.Where(i => i.ResultEvent != PollEvents.None).Count();
		}
		
		public static int ZmqMakeVersion(int major, int minor, int patch)
		{
			return ((major) * 10000 + (minor) * 100 + (patch));
		}

		public static String ErrorText(ErrorNumber errno)
		{
			return "Errno = " + errno.Value.ToString();
		}
	}
}
