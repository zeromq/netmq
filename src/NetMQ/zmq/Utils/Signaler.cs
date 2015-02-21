/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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

namespace NetMQ.zmq.Utils
{
    class Signaler
    {
        //  Underlying write & read file descriptor.
        private Socket m_writeSocket;
        private Socket m_readSocket;
        private readonly byte[] m_dummy;
        private readonly byte[] m_receiveDummy;

        public Signaler()
        {
            m_dummy = new byte[1]{0};
            m_receiveDummy = new byte[1];

            //  Create the socketpair for signaling.
            MakeSocketsPair();
            
            m_writeSocket.Blocking = false;
            m_readSocket.Blocking = false;
        }

        public void Close()
        {
            try
            {
                m_writeSocket.LingerState = new LingerOption(true, 0);
            }
            catch (SocketException)
            {                                
            }

            try
            {
                m_writeSocket.Close();
            }
            catch (SocketException)
            {
            }

            try
            {
                m_readSocket.Close();
            }
            catch (SocketException)
            {
            }
        }

        //  Creates a pair of filedescriptors that will be used
        //  to pass the signals.
        private void MakeSocketsPair()
        {
            using (Socket listner = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified))
            {
                listner.NoDelay = true;
                listner.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // using ephemeral port            
                listner.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listner.Listen(1);

                m_writeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Unspecified);
                m_writeSocket.NoDelay = true;

                m_writeSocket.Connect(listner.LocalEndPoint);
                m_readSocket = listner.Accept();
            }            
        }

        public Socket Handle
        {
            get
            {
                return m_readSocket;
            }
        }

        public void Send()
        {            
            int sent = m_writeSocket.Send(m_dummy);

            Debug.Assert(sent == 1);
        }

        public bool WaitEvent(int timeout)
        {
            if(m_readSocket.Connected)
                return m_readSocket.Poll(timeout * 1000, SelectMode.SelectRead);

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
