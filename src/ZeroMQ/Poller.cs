using zmq;

namespace ZeroMQ
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
  

    /// <summary>
    /// Multiplexes input/output events in a level-triggered fashion over a set of sockets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sockets will be polled according to their capabilities. For example, sockets that are
    /// receive-only (e.g., PULL and SUB sockets) will only poll for Input events. Sockets that
    /// can both send and receive (e.g., REP, REQ, etc.) will poll for both Input and Output events.
    /// </para>
    /// <para>
    /// To actually send or receive data, the socket's <see cref="ZmqSocket.ReceiveReady"/> and/or
    /// <see cref="ZmqSocket.SendReady"/> event handlers must be attached to. If attached, these will
    /// be invoked when data is ready to be received or sent.
    /// </para>
    /// </remarks>
    public class Poller : IDisposable
    {
        private readonly Dictionary<PollItem, ZmqSocket> _pollableSockets;
        

        private PollItem[] _pollItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="Poller"/> class.
        /// </summary>
        public Poller()        
        {
					_pollableSockets = new Dictionary<PollItem, ZmqSocket>();
					Pulse = new AutoResetEvent(false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Poller"/> class with a collection of sockets to poll over.
        /// </summary>
        /// <param name="socketsToPoll">The collection of <see cref="ZmqSocket"/>s to poll.</param>
        public Poller(IEnumerable<ZmqSocket> socketsToPoll)
            : this()
        {
            AddSockets(socketsToPoll);
        }     

        /// <summary>
        /// Gets an <see cref="AutoResetEvent"/> that is pulsed after every Poll call.
        /// </summary>
        public AutoResetEvent Pulse { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Add a socket that will be polled for input/output events, depending on its capabilities.
        /// </summary>
        /// <param name="socket">The <see cref="ZmqSocket"/> to poll.</param>
        /// <exception cref="ArgumentNullException"><paramref name="socket"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="socket"/> has no event handlers.</exception>
        public void AddSocket(ZmqSocket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            var pollEvents = socket.GetPollEvents();

            if (pollEvents == PollEvents.None)
            {
                throw new ArgumentOutOfRangeException("socket", "Unable to add socket without at least one handler.");
            }

            _pollableSockets.Add(new PollItem(socket.SocketHandle, pollEvents), socket);
        }

        /// <summary>
        /// Add a collection of sockets that will be polled for input/output events, depending on their capabilities.
        /// </summary>
        /// <param name="sockets">The collection of <see cref="ZmqSocket"/>s to poll.</param>
        public void AddSockets(IEnumerable<ZmqSocket> sockets)
        {
            if (sockets == null)
            {
                throw new ArgumentNullException("sockets");
            }

            foreach (var socket in sockets)
            {
                AddSocket(socket);
            }
        }

        /// <summary>
        /// Removes all sockets from the current collection.
        /// </summary>
        public void ClearSockets()
        {
            _pollableSockets.Clear();
        }

        /// <summary>
        /// Multiplex input/output events over the contained set of sockets in blocking mode, firing
        /// <see cref="ZmqSocket.ReceiveReady" /> or <see cref="ZmqSocket.SendReady" /> as appropriate.
        /// </summary>
        /// <returns>The number of ready events that were signaled.</returns>
        /// <exception cref="ZmqSocketException">An error occurred polling for socket events.</exception>
        public int Poll()
        {
            return PollBlocking();
        }

        /// <summary>
        /// Multiplex input/output events over the contained set of sockets in non-blocking mode, firing
        /// <see cref="ZmqSocket.ReceiveReady" /> or <see cref="ZmqSocket.SendReady" /> as appropriate.
        /// Returns when one or more events are ready to fire or when the specified timeout elapses, whichever
        /// comes first.
        /// </summary>
        /// <returns>The number of ready events that were signaled.</returns>
        /// <param name="timeout">A <see cref="TimeSpan"/> indicating the timeout value.</param>
        /// <exception cref="ZmqSocketException">An error occurred polling for socket events.</exception>
        public int Poll(TimeSpan timeout)
        {
            return (int)timeout.TotalMilliseconds == Timeout.Infinite
                ? PollBlocking()
                : PollNonBlocking(timeout);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if the object is being disposed, false otherwise.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Pulse.Dispose();
            }
        }

        private static void ContinueIfInterrupted()
        {
            // An error value of EINTR indicates that the operation was interrupted
            // by delivery of a signal before any events were available. This is a recoverable
            // error, so try polling again for the remaining amount of time in the timeout.
            if (!ErrorProxy.ThreadWasInterrupted)
            {
                throw new ZmqSocketException(ErrorProxy.GetLastError());
            }
        }

        private int PollBlocking()
        {
            CreatePollItems();

            int readyCount;

            while ((readyCount = Poll(Timeout.Infinite)) == -1 && !ErrorProxy.ContextWasTerminated)
            {
                ContinueIfInterrupted();
            }

            return readyCount;
        }

        private int PollNonBlocking(TimeSpan timeout)
        {
            CreatePollItems();

            var remainingTimeout = (int)timeout.TotalMilliseconds;
            var elapsed = Stopwatch.StartNew();
            int readyCount;

            do
            {
                readyCount = Poll(remainingTimeout);

                if (readyCount >= 0 || ErrorProxy.ContextWasTerminated)
                {
                    break;
                }

                ContinueIfInterrupted();
                remainingTimeout -= (int)elapsed.ElapsedMilliseconds;
            }
            while (remainingTimeout >= 0);

            return readyCount;
        }

        private void CreatePollItems()
        {
            if (_pollItems == null || _pollItems.Length != _pollableSockets.Count)
            {
                _pollItems = _pollableSockets.Keys.ToArray();
            }
        }

        private int Poll(int timeoutMilliseconds)
        {
            if (_pollableSockets.Count == 0)
            {
                throw new InvalidOperationException("At least one socket is required for polling.");
            }

            int readyCount = _pollerProxy.Poll(_pollItems, timeoutMilliseconds);

            Pulse.Set();

            if (readyCount > 0)
            {
                foreach (PollItem pollItem in _pollItems.Where(item => item.ReadyEvents != (short)PollEvents.None))
                {
                    ZmqSocket socket = _pollableSockets[pollItem];

                    socket.InvokePollEvents((PollEvents)pollItem.ReadyEvents);
                }
            }

            return readyCount;
        }
    }
}
