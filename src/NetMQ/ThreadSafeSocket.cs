using System;
using System.Diagnostics;
using System.Threading;
using NetMQ.Core;
#if NET40
using NetMQ.Core.Utils;
#endif

namespace NetMQ
{
    /// <summary>
    /// Base interface for thread-safe sockets
    /// </summary>
    public interface IThreadSafeSocket
    {
        /// <summary>
        /// Send a message if one is available within <paramref name="timeout"/>.
        /// </summary>
        /// <param name="msg">An object with message's data to send.</param>
        /// <param name="timeout">The maximum length of time to try and send a message. If <see cref="TimeSpan.Zero"/>, no
        /// wait occurs.</param>
        /// <returns><c>true</c> if a message was sent, otherwise <c>false</c>.</returns>
        bool TrySend(ref Msg msg, TimeSpan timeout);

        /// <summary>Attempt to receive a message for the specified amount of time.</summary>
        /// <param name="msg">A reference to a <see cref="Msg"/> instance into which the received message
        /// data should be placed.</param>
        /// <param name="timeout">The maximum amount of time the call should wait for a message before returning.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was received before <paramref name="timeout"/> elapsed or cancellation was requested,
        /// otherwise <c>false</c>.</returns>
        bool TryReceive(ref Msg msg, TimeSpan timeout, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Tag interface to tag sockets that support sending
    /// </summary>
    public interface IThreadSafeOutSocket : IThreadSafeSocket
    {
    }

    /// <summary>
    /// Tag interface to tag sockets that support receiving
    /// </summary>
    public interface IThreadSafeInSocket : IThreadSafeSocket
    {
    }
    
    /// <summary>
    /// Tag interface for sockets that send and receive messages with <see cref="Msg.RoutingId" />.
    /// </summary>
    public interface IRoutingIdSocket : IThreadSafeSocket
    {
    }
    
    /// <summary>
    /// Tag interface to tag sockets that support sending a group message
    /// </summary>
    public interface IGroupOutSocket : IThreadSafeSocket
    {
    }

    /// <summary>
    /// Tag interface to tag sockets that support receiving a group message
    /// </summary>
    public interface IGroupInSocket : IThreadSafeSocket
    {
    }
    
    /// <summary>
    /// Abstract base class for NetMQ's different thread-safe socket types.
    /// </summary>
    /// <remarks>
    /// Various options are available in this base class, though their affect can vary by socket type.
    /// </remarks>
    public abstract class ThreadSafeSocket: IThreadSafeSocket, IDisposable  
    {
        internal readonly SocketBase m_socketHandle;
        private int m_isClosed;
        
        /// <summary>
        /// Creates a thread socket of type <paramref name="socketType"/>
        /// </summary>
        /// <param name="socketType"></param>
        protected internal ThreadSafeSocket(ZmqSocketType socketType)
        {
            m_socketHandle = NetMQConfig.Context.CreateSocket(socketType);
            Options = new ThreadSafeSocketOptions(m_socketHandle);
        }
        
        /// <summary>
        /// Get the Socket Options of this socket.
        /// </summary>
        public ThreadSafeSocketOptions Options { get; }

        #region Send and Receive

        /// <summary>
        /// Send a message if one is available within <paramref name="timeout"/>.
        /// </summary>
        /// <param name="msg">An object with message's data to send.</param>
        /// <param name="timeout">The maximum length of time to try and send a message. If <see cref="TimeSpan.Zero"/>, no
        /// wait occurs.</param>
        /// <returns><c>true</c> if a message was sent, otherwise <c>false</c>.</returns>
        public virtual bool TrySend(ref Msg msg, TimeSpan timeout)
        {
            return m_socketHandle.TrySend(ref msg, timeout, false);
        }
        
        /// <summary>Attempt to receive a message for the specified amount of time.</summary>
        /// <param name="msg">A reference to a <see cref="Msg"/> instance into which the received message
        /// data should be placed.</param>
        /// <param name="timeout">The maximum amount of time the call should wait for a message before returning.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns><c>true</c> if a message was received before <paramref name="timeout"/> elapsed,
        /// otherwise <c>false</c>.</returns>
        public virtual bool TryReceive(ref Msg msg, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return m_socketHandle.TryRecv(ref msg, timeout, cancellationToken);
        }

        #endregion

        #region Bind, Unbind, Connect, Disconnect, Close

        /// <summary>
        /// Bind the socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string representing the address to bind this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener encountered an
        /// error during initialisation.</exception>
        public void Bind(string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Bind(address);
        }

        /// <summary>Binds the specified TCP <paramref name="address"/> to an available port, assigned by the operating system.</summary>
        /// <returns>the chosen port-number</returns>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="ProtocolNotSupportedException"><paramref name="address"/> uses a protocol other than TCP.</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        /// <exception cref="NetMQException">No IO thread was found, or the protocol's listener errored during
        /// initialisation.</exception>
        public int BindRandomPort(string address)
        {
            m_socketHandle.CheckDisposed();

            return m_socketHandle.BindRandomPort(address);
        }

        /// <summary>
        /// Connect the socket to <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to connect this socket to</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="NetMQException">No IO thread was found.</exception>
        /// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
        public void Connect(string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.Connect(address);
        }

        /// <summary>
        /// Disconnect this socket from <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to disconnect from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        public void Disconnect(string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>
        /// Unbind this socket from <paramref name="address"/>.
        /// </summary>
        /// <param name="address">a string denoting the address to unbind from</param>
        /// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
        /// <exception cref="TerminatingException">The socket has been stopped.</exception>
        /// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
        public void Unbind(string address)
        {
            m_socketHandle.CheckDisposed();

            m_socketHandle.TermEndpoint(address);
        }

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Dispose()"/>.</summary>
        public void Close()
        {
            // #if NETSTANDARD2_0 || NETSTANDARD2_1 || NET47
            // if (m_runtime != null)
            // {
            //     m_runtime.Remove(this);
            //     m_runtime  = null;
            // }
            // #endif

            if (Interlocked.Exchange(ref m_isClosed, 1) != 0)
                return;

            m_socketHandle.CheckDisposed();

            m_socketHandle.Close();
        }

        #endregion
        
        #region IDisposable

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Close"/>.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="Close"/>.</summary>
        /// <param name="disposing">true if releasing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Close();
        }

        /// <summary>
        /// Gets whether the object has been disposed.
        /// </summary>
        public bool IsDisposed => m_isClosed != 0;

        #endregion
    }
}
