/*
    Copyright other contributors as noted in the AUTHORS file.

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
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace NetMQ.zmq
{
	public static class Utils
	{

		private static readonly Random s_random = new Random();

		public static int GenerateRandom()
		{
			return s_random.Next();
		}


		public static void TuneTcpSocket(Socket fd)
		{
			//  Disable Nagle's algorithm. We are doing data batching on 0MQ level,
			//  so using Nagle wouldn't improve throughput in anyway, but it would
			//  hurt latency.
			try
			{
				fd.NoDelay = (true);
			}
			catch (SocketException)
			{
			}
		}

		
		public static void TuneTcpKeepalives(Socket fd, int tcpKeepalive,
																					 int tcpKeepaliveCnt, int tcpKeepaliveIdle,
																					 int tcpKeepaliveIntvl)
		{

			if (tcpKeepalive != -1)
			{
				fd.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, tcpKeepalive);

				
				if (tcpKeepaliveIdle != -1 && tcpKeepaliveIntvl != -1)
				{
					ByteArraySegment bytes = new ByteArraySegment(new byte[12]);

				  Endianness endian = BitConverter.IsLittleEndian ? Endianness.Little : Endianness.Big;

          bytes.PutInteger(endian, tcpKeepalive, 0);
          bytes.PutInteger(endian, tcpKeepaliveIdle, 4);
          bytes.PutInteger(endian, tcpKeepaliveIntvl, 8);

					fd.IOControl(IOControlCode.KeepAliveValues, (byte[]) bytes, null);
				}
			}
		}

		public static void UnblockSocket(Socket s)
		{
			s.Blocking = false;
		}

		//@SuppressWarnings("unchecked")
		public static T[] Realloc<T>(T[] src, int size, bool ended)
		{
			T[] dest;

			if (size > src.Length)
			{
				dest = new T[size];
				if (ended)
					Array.Copy(src, 0, dest, 0, src.Length);
				else
					Array.Copy(src, 0, dest, size - src.Length, src.Length);
			}
			else if (size < src.Length)
			{
				dest = new T[size];
				if (ended)
					Array.Copy(src, 0, dest, 0, size);
				else
					Array.Copy(src, src.Length - size, dest, 0, size);

			}
			else
			{
				dest = src;
			}
			return dest;
		}

		public static void Swap<T>(List<T> items, int index1, int index2) where T : class
		{
			if (index1 == index2)
				return;

			T item1 = items[index1];
			T item2 = items[index2];
			if (item1 != null)
				items[index2] = item1;
			if (item2 != null)
				items[index1] = item2;
		}

		public static byte[] Realloc(byte[] src, int size)
		{

			byte[] dest = new byte[size];
			if (src != null)
				Buffer.BlockCopy(src, 0, dest, 0, src.Length);

			return dest;
		}
	}
}
