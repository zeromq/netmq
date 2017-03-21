/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
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
using System.Diagnostics;
using System.Net.Sockets;
using AsyncIO;
using JetBrains.Annotations;

namespace NetMQ.Core.Transports.Tcp
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
        private readonly Address m_addr;

        /// <summary>
        /// The underlying AsyncSocket.
        /// </summary>
        [CanBeNull]
        private AsyncSocket m_s;

        /// <summary>
        /// If true file descriptor is registered with the poller and 'handle'
        /// contains valid value.
        /// </summary>
        private bool m_handleValid;

        /// <summary>
        /// If true, connector is waiting a while before trying to connect.
        /// </summary>
        private readonly bool m_delayedStart;

        /// <summary>
        /// True if a timer has been started.
        /// </summary>
        private bool m_timerStarted;

        /// <summary>
        /// Reference to the session we belong to.
        /// </summary>
        private readonly SessionBase m_session;

        /// <summary>
        /// Current reconnect-interval. This gets updated for back-off strategy.
        /// </summary>
        private int m_currentReconnectIvl;

        /// <summary>
        /// String representation of endpoint to connect to
        /// </summary>
        private readonly string m_endpoint;

        /// <summary>
        /// Socket
        /// </summary>
        private readonly SocketBase m_socket;

        /// <summary>
        /// Create a new TcpConnector object.
        /// </summary>
        /// <param name="ioThread">the I/O-thread for this TcpConnector to live on.</param>
        /// <param name="session">the session that will contain this</param>
        /// <param name="options">Options that define this new TcpC</param>
        /// <param name="addr">the Address for this Tcp to connect to</param>
        /// <param name="delayedStart">this boolean flag dictates whether to wait before trying to connect</param>
        public TcpConnector([NotNull] IOThread ioThread, [NotNull] SessionBase session, [NotNull] Options options, [NotNull] Address addr, bool delayedStart)
            : base(ioThread, options)
        {
            m_ioObject = new IOObject(ioThread);
            m_addr = addr;
            m_s = null;
            m_handleValid = false;
            m_delayedStart = delayedStart;
            m_timerStarted = false;
            m_session = session;
            m_currentReconnectIvl = m_options.ReconnectIvl;

            Debug.Assert(m_addr != null);
            m_endpoint = m_addr.ToString();
            m_socket = session.Socket;
        }

        /// <summary>
        /// This does nothing.
        /// </summary>
        public override void Destroy()
        {
            Debug.Assert(!m_timerStarted);
            Debug.Assert(!m_handleValid);
            Debug.Assert(m_s == null);
        }

        /// <summary>
        /// Begin connecting.  If a delayed-start was specified - then the reconnect-timer is set, otherwise this starts immediately.
        /// </summary>
        protected override void ProcessPlug()
        {
            m_ioObject.SetHandler(this);
            if (m_delayedStart)
                AddReconnectTimer();
            else
            {
                StartConnecting();
            }
        }

        /// <summary>
        /// Process a termination request.
        /// This cancels the reconnect-timer, closes the AsyncSocket, and marks the socket-handle as invalid.
        /// </summary>
        /// <param name="linger">a time (in milliseconds) for this to linger before actually going away. -1 means infinite.</param>
        protected override void ProcessTerm(int linger)
        {
            if (m_timerStarted)
            {
                m_ioObject.CancelTimer(ReconnectTimerId);
                m_timerStarted = false;
            }

            if (m_handleValid)
            {
                m_ioObject.RemoveSocket(m_s);
                m_handleValid = false;
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
        /// <exception cref="NotImplementedException">InCompleted must not be called on a TcpConnector.</exception>
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

            // Create the socket.
            try
            {
                m_s = AsyncSocket.Create(m_addr.Resolved.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException)
            {
                AddReconnectTimer();
                return;
            }

            m_ioObject.AddSocket(m_s);
            m_handleValid = true;

            // Connect to the remote peer.
            try
            {
                m_s.Connect(m_addr.Resolved.Address.Address, m_addr.Resolved.Address.Port);
                m_socket.EventConnectDelayed(m_endpoint, ErrorCode.InProgress);
            }
            catch (SocketException ex)
            {
                OutCompleted(ex.SocketErrorCode, 0);
            }
            // TerminatingException can occur in above call to EventConnectDelayed via
            // MonitorEvent.Write if corresponding PairSocket has been sent Term command
            catch (TerminatingException)
            {}
        }

        /// <summary>
        /// This method is called when a message Send operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        /// <exception cref="NetMQException">A non-recoverable socket error occurred.</exception>
        /// <exception cref="NetMQException">If the socketError is not Success then it must be a valid recoverable error.</exception>
        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success)
            {
                m_ioObject.RemoveSocket(m_s);
                m_handleValid = false;

                Close();

                // Try again to connect after a time,
                // as long as the error is one of these..
                if (socketError == SocketError.ConnectionRefused || socketError == SocketError.TimedOut ||
                    socketError == SocketError.ConnectionAborted ||
                    socketError == SocketError.HostUnreachable || socketError == SocketError.NetworkUnreachable ||
                    socketError == SocketError.NetworkDown || socketError == SocketError.AccessDenied ||
                    socketError == SocketError.OperationAborted)
                {
                    if (m_options.ReconnectIvl >= 0)
                        AddReconnectTimer();
                }
                else
                {
                    throw NetMQException.Create(socketError);
                }
            }
            else
            {
                m_ioObject.RemoveSocket(m_s);
                m_handleValid = false;

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

                // Create the engine object for this connection.
                var engine = new StreamEngine(m_s, m_options, m_endpoint);

                m_socket.EventConnected(m_endpoint, m_s);

                m_s = null;

                // Attach the engine to the corresponding session object.
                SendAttach(m_session, engine);

                // Shut the connector down.
                Terminate();
            }
        }

        /// <summary>
        /// This is called when the timer expires - to start trying to connect.
        /// </summary>
        /// <param name="id">The timer-id. This is not used.</param>
        public void TimerEvent(int id)
        {
            m_timerStarted = false;
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
            m_timerStarted = true;
        }

        /// <summary>
        /// Internal function to return a reconnect back-off delay.
        /// Will modify the current_reconnect_ivl used for next call
        /// Returns the currently used interval
        /// </summary>
        private int GetNewReconnectIvl()
        {
            // The new interval is the current interval + random value.
            int thisInterval = m_currentReconnectIvl + new Random().Next(0, m_options.ReconnectIvl);

            // Only change the current reconnect interval  if the maximum reconnect
            // interval was set and if it's larger than the reconnect interval.
            if (m_options.ReconnectIvlMax > 0 &&
                m_options.ReconnectIvlMax > m_options.ReconnectIvl)
            {
                // Calculate the next interval
                m_currentReconnectIvl = m_currentReconnectIvl * 2;
                if (m_currentReconnectIvl >= m_options.ReconnectIvlMax)
                {
                    m_currentReconnectIvl = m_options.ReconnectIvlMax;
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
                m_socket.EventCloseFailed(m_endpoint, ex.SocketErrorCode.ToErrorCode());
            }
        }
    }
}
