/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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
using System.Diagnostics;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.zmq.Transports.Tcp
{
    /// <summary>
    /// If 'delay' is true connector first waits for a while, then starts connection process.
    /// </summary>
    internal class TcpConnector : Own, IProactorEvents
    {
        /// <summary>
        /// ID of the timer used to delay the reconnection. Value is 1.
        /// </summary>
        private const int ReconnectTimerId = 1;

        private readonly IOObject m_ioObject;

        /// <summary>
        /// Address to connect to. Owned by session_base_t.
        /// </summary>
        private readonly Address m_address;

        /// <summary>
        /// Underlying socket.
        /// </summary>
        [CanBeNull]
        private AsyncSocket m_s;

        /// <summary>
        /// If true file descriptor is registered with the poller and 'handle'
        /// contains valid value.
        /// </summary>
        private bool m_isHandleValid;

        /// <summary>
        /// If true, connector is waiting a while before trying to connect.
        /// </summary>
        private readonly bool m_isDelayedStart;

        /// <summary>
        /// True if a timer has been started.
        /// </summary>
        private bool m_isTimerStarted;

        /// <summary>
        /// Reference to the session we belong to.
        /// </summary>
        private readonly SessionBase m_session;

        /// <summary>
        /// Current reconnect-interval, updated for back-off strategy
        /// </summary>
        private int m_currentReconnectInterval;

        /// <summary>
        /// String representation of endpoint to connect to
        /// </summary>
        private readonly string m_endpoint;

        /// <summary>
        /// Socket
        /// </summary>
        private readonly SocketBase m_socket;

        public TcpConnector([NotNull] IOThread ioThread, [NotNull] SessionBase session, [NotNull] Options options, [NotNull] Address addr, bool delayedStart)
            : base(ioThread, options)
        {
            m_ioObject = new IOObject(ioThread);
            m_address = addr;
            m_s = null;
            m_isHandleValid = false;
            m_isDelayedStart = delayedStart;
            m_isTimerStarted = false;
            m_session = session;
            m_currentReconnectInterval = m_options.ReconnectIvl;

            Debug.Assert(m_address != null);
            m_endpoint = m_address.ToString();
            m_socket = session.Socket;
        }

        /// <summary>
        /// This does nothing.
        /// </summary>
        public override void Destroy()
        {
            Debug.Assert(!m_isTimerStarted);
            Debug.Assert(!m_isHandleValid);
            Debug.Assert(m_s == null);
        }

        protected override void ProcessPlug()
        {
            m_ioObject.SetHandler(this);
            if (m_isDelayedStart)
                AddReconnectTimer();
            else
            {
                StartConnecting();
            }
        }

        protected override void ProcessTerm(int linger)
        {
            if (m_isTimerStarted)
            {
                m_ioObject.CancelTimer(ReconnectTimerId);
                m_isTimerStarted = false;
            }

            if (m_isHandleValid)
            {
                m_ioObject.RemoveSocket(m_s);
                m_isHandleValid = false;
            }

            if (m_s != null)
                Close();

            base.ProcessTerm(linger);
        }

        /// <summary>
        /// This method would be called when a message receive operation has been completed, although here it only throws a NotImplementedException.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        /// <exception cref="NotImplementedException">this operation is not supported on the TcpConnector class.</exception>
        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Internal function to start the actual connection establishment.
        /// </summary>
        private void StartConnecting()
        {
            Debug.Assert(m_s == null);

            //  Create the socket.
            try
            {
                m_s = AsyncSocket.Create(m_address.Resolved.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException)
            {
                AddReconnectTimer();
                return;
            }

            m_ioObject.AddSocket(m_s);
            m_isHandleValid = true;

            //  Connect to the remote peer.
            try
            {
                m_s.Connect(m_address.Resolved.Address.Address, m_address.Resolved.Address.Port);
                m_socket.EventConnectDelayed(m_endpoint, ErrorCode.InProgress);
            }
            catch (SocketException ex)
            {
                OutCompleted(ex.SocketErrorCode, 0);
            }
        }

        /// <summary>
        /// This method is called when a message Send operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        /// <exception cref="NetMQException">A non-recoverable socket error occurred.</exception>
        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            m_ioObject.RemoveSocket(m_s);
            m_isHandleValid = false;

            if (socketError != SocketError.Success)
            {
                Close();

                // Try again to connect after a time,
                // as long as the error is one of these..
                if (socketError == SocketError.ConnectionRefused || socketError == SocketError.TimedOut ||
                    socketError == SocketError.ConnectionAborted ||
                    socketError == SocketError.HostUnreachable || socketError == SocketError.NetworkUnreachable ||
                    socketError == SocketError.NetworkDown)
                {
                    AddReconnectTimer();
                }
                else
                {
                    throw NetMQException.Create(socketError);
                }
            }
            else  // socketError is Success.
            {
                m_s.NoDelay = true;

                // As long as the TCP keep-alive option is not -1 (indicating no change),
                if (m_options.TcpKeepalive != -1)
                {
                    // Set the TCP keep-alive option values to the underlying socket.
                    m_s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, m_options.TcpKeepalive);

                    if (m_options.TcpKeepaliveIdle != -1 && m_options.TcpKeepaliveIntvl != -1)
                    {
                        // Write the TCP keep-alive options to a byte-array, to feed to the IOControl method..
                        var bytes = new ByteArraySegment(new byte[12]);

                        Endianness endian = BitConverter.IsLittleEndian ? Endianness.Little : Endianness.Big;

                        bytes.PutInteger(endian, m_options.TcpKeepalive, 0);
                        bytes.PutInteger(endian, m_options.TcpKeepaliveIdle, 4);
                        bytes.PutInteger(endian, m_options.TcpKeepaliveIntvl, 8);

                        m_s.IOControl(IOControlCode.KeepAliveValues, (byte[])bytes, null);
                    }
                }

                //  Create the engine object for this connection.
                var engine = new StreamEngine(m_s, m_options, m_endpoint);

                m_socket.EventConnected(m_endpoint, m_s);

                m_s = null;

                //  Attach the engine to the corresponding session object.
                SendAttach(m_session, engine);

                //  Shut the connector down.
                Terminate();
            }
        }

        /// <summary>
        /// This is called when the timer expires - to start trying to connect.
        /// </summary>
        /// <param name="id">The timer-id. This is not used.</param>
        public void TimerEvent(int id)
        {
            m_isTimerStarted = false;
            StartConnecting();
        }

        /// <summary>
        /// Internal function to add a reconnect timer
        /// </summary>
        private void AddReconnectTimer()
        {
            int rcIvl = GetNewReconnectIvl();
            m_ioObject.AddTimer(rcIvl, ReconnectTimerId);
            m_socket.EventConnectRetried(m_endpoint, rcIvl);
            m_isTimerStarted = true;
        }

        /// <summary>
        /// Internal function to return a reconnect back-off delay.
        /// Will modify the current_reconnect_ivl used for next call
        /// Returns the currently used interval
        /// </summary>
        private int GetNewReconnectIvl()
        {
            //  The new interval is the current interval + random value.
            int thisInterval = m_currentReconnectInterval + new Random().Next(0, m_options.ReconnectIvl);

            //  Only change the current reconnect interval  if the maximum reconnect
            //  interval was set and if it's larger than the reconnect interval.
            if (m_options.ReconnectIvlMax > 0 &&
                m_options.ReconnectIvlMax > m_options.ReconnectIvl)
            {
                //  Calculate the next interval
                m_currentReconnectInterval = m_currentReconnectInterval * 2;
                if (m_currentReconnectInterval >= m_options.ReconnectIvlMax)
                {
                    m_currentReconnectInterval = m_options.ReconnectIvlMax;
                }
            }
            return thisInterval;
        }

        /// <summary>
        /// Close the connecting socket.
        /// </summary>
        private void Close()
        {
            Debug.Assert(m_s != null);
            try
            {
                m_s.Dispose();
                m_socket.EventClosed(m_endpoint, m_s);
                m_s = null;
            }
            catch (SocketException ex)
            {
                m_socket.EventCloseFailed(m_endpoint, ErrorHelper.SocketErrorToErrorCode(ex.SocketErrorCode));
            }
        }
    }
}
