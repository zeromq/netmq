/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2015 Other contributors as noted in the AUTHORS file

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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;

namespace NetMQ.Core.Utils
{
    internal sealed class Signaler
    {
        // Underlying write & read file descriptor.
        [NotNull] private readonly Socket m_writeSocket;
        [NotNull] private readonly Socket m_readSocket;
        [NotNull] private readonly byte[] m_dummy;
        [NotNull] private readonly byte[] m_receiveDummy;

        public Signaler()
        {
            m_dummy = new byte[] { 0 };
            m_receiveDummy = new byte[1];

            // Create the socketpair for signaling.
            using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified))
            {
                listener.NoDelay = true;
                listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // using ephemeral port
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listener.Listen(1);

                m_writeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified) { NoDelay = true };

                m_writeSocket.Connect(listener.LocalEndPoint);
                m_readSocket = listener.Accept();
            }

            m_writeSocket.Blocking = false;
            m_readSocket.Blocking = false;
        }

        /// <summary>
        /// Close the read and write sockets.
        /// </summary>
        public void Close()
        {
            try
            {
                m_writeSocket.LingerState = new LingerOption(true, 0);
            }
            catch (SocketException)
            {}

            try
            {
#if NET35
                m_writeSocket.Close();
#else
                m_writeSocket.Dispose();
#endif
            }
            catch (SocketException)
            {}

            try
            {
#if NET35
                m_readSocket.Close();
#else
                m_readSocket.Dispose();
#endif
            }
            catch (SocketException)
            {}
        }

        // Creates a pair of file descriptors that will be used
        // to pass the signals.

        [NotNull]
        public Socket Handle => m_readSocket;

        public void Send()
        {
            int sent = m_writeSocket.Send(m_dummy);

            Debug.Assert(sent == 1);
        }

        public bool WaitEvent(int timeout)
        {
            int timeoutInMicroSeconds = (timeout >= 0)
                ? timeout * 1000
                : Timeout.Infinite;

            if (m_readSocket.Connected)
                return m_readSocket.Poll(timeoutInMicroSeconds, SelectMode.SelectRead);

            return false;
        }

        public void Recv()
        {
            int received = m_readSocket.Receive(m_receiveDummy);

            Debug.Assert(received == 1);
            Debug.Assert(m_receiveDummy[0] == 0);
        }
    }
}