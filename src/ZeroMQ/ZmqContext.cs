using zmq;

namespace ZeroMQ
{
    using System;
    using System.Text;    

    /// <summary>
    /// Creates <see cref="ZmqSocket"/> instances within a process boundary.
    /// </summary>
    /// <remarks>
    /// The <see cref="ZmqContext"/> object is a container for all sockets in a single process,
    /// and acts as the transport for inproc sockets. <see cref="ZmqContext"/> is thread safe.
    /// A <see cref="ZmqContext"/> must not be terminated until all spawned sockets have been
    /// successfully closed.
    /// </remarks>
    public class ZmqContext : IDisposable
    {
        private readonly zmq.Ctx _contextProxy;

        private bool _disposed;

        static ZmqContext()
        {
            DefaultEncoding = Encoding.UTF8;
        }

				internal ZmqContext(zmq.Ctx contextProxy)
        {
            if (contextProxy == null)
            {
                throw new ArgumentNullException("contextProxy");
            }

            _contextProxy = contextProxy;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ZmqContext"/> class.
        /// </summary>
        ~ZmqContext()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets or sets the default encoding for all sockets in the current process.
        /// </summary>
        public static Encoding DefaultEncoding { get; set; }
        
        /// <summary>
        /// Gets or sets the size of the thread pool for the current context (default = 1).
        /// </summary>
        public int ThreadPoolSize
        {
            get { return ZMQ.CtxGet(_contextProxy, zmq.ContextOption.IOThreads);  }
            set { ZMQ.CtxSet(_contextProxy, ContextOption.IOThreads, value); }
        }

        /// <summary>
        /// Gets or sets the maximum number of sockets for the current context (default = 1024).
        /// </summary>
        public int MaxSockets
        {
            get { return ZMQ.CtxGet(_contextProxy, zmq.ContextOption.MaxSockets); }
            set { ZMQ.CtxSet(_contextProxy, ContextOption.MaxSockets, value); }
        }

        /// <summary>
        /// Create a <see cref="ZmqContext"/> instance.
        /// </summary>
        /// <returns>A <see cref="ZmqContext"/> instance with the default thread pool size (1).</returns>
        public static ZmqContext Create()
        {
            var contextProxy = ZMQ.CtxNew();

            //if (contextProxy.Initialize() == -1)
            //{
            //    throw new ZmqException(ErrorProxy.GetLastError());
            //}

            return new ZmqContext(contextProxy);
        }

        /// <summary>
        /// Create a socket with the current context and the specified socket type.
        /// </summary>
        /// <param name="socketType">A <see cref="SocketType"/> value for the socket.</param>
        /// <returns>A <see cref="ZmqSocket"/> instance with the current context and the specified socket type.</returns>
        public ZmqSocket CreateSocket(ZmqSocketType socketType)
        {
            switch (socketType)
            {
                case ZmqSocketType.Req:
                case ZmqSocketType.Rep:
                case ZmqSocketType.Dealer:
                case ZmqSocketType.Router:
                case ZmqSocketType.Xpub:
                case ZmqSocketType.Pair:
                    return CreateSocket(sp => new DuplexSocket(sp, socketType), socketType);

                case ZmqSocketType.Push:
                case ZmqSocketType.Pub:
                    return CreateSocket(sp => new SendSocket(sp, socketType), socketType);

                case ZmqSocketType.Pull:
                    return CreateSocket(sp => new ReceiveSocket(sp, socketType), socketType);

                case ZmqSocketType.Sub:
                    return CreateSocket(sp => new SubscribeSocket(sp, socketType), socketType);

                case ZmqSocketType.Xsub:
                    return CreateSocket(sp => new SubscribeExtSocket(sp, socketType), socketType);
            }

            throw new InvalidOperationException("Invalid socket type specified: " + socketType);
        }

        /// <summary>
        /// Terminate the ZeroMQ context.
        /// </summary>
        /// <remarks>
        /// Context termination is performed in the following steps:
        /// <ul>
        ///   <li>
        ///     Any blocking operations currently in progress on sockets open within context shall return immediately
        ///     with an error code of ETERM. With the exception of <see cref="ZmqSocket.Close"/>, any further operations
        ///     on sockets open within the context shall fail with a <see cref="ZmqSocketException"/>.
        ///   </li>
        ///   <li>
        ///     After interrupting all blocking calls, <see cref="Terminate"/> shall block until the following conditions
        ///     are met:
        ///     <ul>
        ///       <li>
        ///         All sockets open within the context have been closed with <see cref="ZmqSocket.Close"/>.
        ///       </li>
        ///       <li>
        ///         For each socket within the context, all messages sent by the application  have either been
        ///         physically transferred to a network peer, or the socket's linger period set with the
        ///         <see cref="ZmqSocket.Linger"/> socket option has expired.
        ///       </li>
        ///     </ul>
        ///   </li>
        /// </ul>
        /// </remarks>
        /// <exception cref="System.ObjectDisposedException">The <see cref="ZmqContext"/> has already been disposed.</exception>
        /// <exception cref="ZmqException">An error occurred creating the socket.</exception>
        public void Terminate()
        {
            EnsureNotDisposed();

            _contextProxy.Terminate();
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="ZmqContext"/> class.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ZmqContext"/>, and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                	ZMQ.Term(_contextProxy);                    
                }
            }

            _disposed = true;
        }

        private TSocket CreateSocket<TSocket>(Func<SocketBase, TSocket> constructor, ZmqSocketType socketType) where TSocket : ZmqSocket
        {
            EnsureNotDisposed();

            SocketBase socketHandle = _contextProxy.CreateSocket(socketType);

            if (socketHandle == null)
            {
                throw new ZmqException(ZError.ErrorNumber, ZMQ.ErrorText(ZError.ErrorNumber));
            }

            return constructor(socketHandle);
        }

        private void SetContextOption(ContextOption option, int value)
        {
            if (_contextProxy.SetContextOption((int)option, value) == -1)
            {
                throw new ZmqException(ErrorProxy.GetLastError());
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
