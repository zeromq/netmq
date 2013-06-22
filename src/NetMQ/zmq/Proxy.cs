/*
    Copyright (c) 2007-2011 iMatix Corporation
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
using NetMQ.zmq;

namespace NetMQ.zmq
{
	public class Proxy
	{

		public static bool CreateProxy(SocketBase frontend,
						SocketBase backend, SocketBase capture)
		{

			//  The algorithm below assumes ratio of requests and replies processed
			//  under full load to be 1:1.

			int more;
			int rc;
			Msg msg;
			PollItem[] items = new PollItem[2];

			items[0] = new PollItem(frontend, PollEvents.PollIn);
			items[1] = new PollItem(backend, PollEvents.PollIn);

			while (true)
			{
				//  Wait while there are either requests or replies to process.
				rc = ZMQ.Poll(items, -1);
				if (rc < 0)
					return false;

				//  Process a request.
				if ((items[0].ResultEvent & PollEvents.PollIn) == PollEvents.PollIn)
				{
					while (true)
					{
						try
						{
							msg = frontend.Recv(0);
						}
						catch (TerminatingException)
						{
							return false;
						}

						if (msg == null)
						{
							return false;
						}

						more = frontend.GetSocketOption(ZmqSocketOptions.ReceiveMore);

						if (more < 0)
							return false;

						//  Copy message to capture socket if any
						if (capture != null)
						{
							Msg ctrl = new Msg(msg);
							capture.Send(ctrl, more > 0 ? SendReceiveOptions.SendMore : 0);							
						}

						backend.Send(msg, more > 0 ? SendReceiveOptions.SendMore : 0);
						if (more == 0)
							break;
					}
				}
				//  Process a reply.
				if ((items[1].ResultEvent & PollEvents.PollIn) == PollEvents.PollIn)
				{
					while (true)
					{
						try
						{
							msg = backend.Recv(0);
						}
						catch (TerminatingException)
						{
							return false;
						}

						if (msg == null)
						{
							return false;
						}

						more = backend.GetSocketOption(ZmqSocketOptions.ReceiveMore);

						if (more < 0)
							return false;

						//  Copy message to capture socket if any
						if (capture != null)
						{
							Msg ctrl = new Msg(msg);
							capture.Send(ctrl, more > 0 ? SendReceiveOptions.SendMore : 0);
							
						}

						frontend.Send(msg, more > 0 ? SendReceiveOptions.SendMore : 0);
						if (more == 0)
							break;
					}
				}
			}
		}
	}
}