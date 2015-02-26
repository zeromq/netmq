using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ.Devices
{
    /// <summary>
    /// Configures the given socket
    /// </summary>
    public class DeviceSocketSetup
    {
        private readonly NetMQSocket m_socket;
        private readonly List<Action<NetMQSocket>> m_socketInitializers;
        private readonly List<string> m_bindings;
        private readonly List<string> m_connections;

        private bool m_isConfigured;

        internal DeviceSocketSetup([NotNull] NetMQSocket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            m_socket = socket;
            m_socketInitializers = new List<Action<NetMQSocket>>();
            m_bindings = new List<string>();
            m_connections = new List<string>();
        }

        /// <summary>
        /// Configure the contained socket to bind to a given endpoint.
        /// Essentially this simply adds this endpoint to the list of bindings.
        /// </summary>
        /// <param name="endpoint">a string representing the endpoint to which the socket will bind (must not be null)</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup Bind([NotNull] string endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            m_bindings.Add(endpoint);
            return this;
        }

        /// <summary>
        /// Configure the contained socket to connect to a given endpoint.
        /// Essentially this simply adds this endpoint to the list of connections.
        /// </summary>
        /// <param name="endpoint">a string representing the endpoint to which the socket will connect (must not be null)</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup Connect([NotNull] string endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            m_connections.Add(endpoint);
            return this;
        }

        /// <summary>
        /// Set an integer-based socket option.
        /// </summary>
        /// <param name="property">the <see cref="TSocket"/> property to set</param>
        /// <param name="value">the integer value to assignt</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, int>> property, int value)
        {
            return SetSocketOption<int>(property, value);
        }

        /// <summary>
        /// Set an timespan-based socket option.
        /// </summary>
        /// <param name="property">the <see cref="TSocket"/> property to set</param>
        /// <param name="value">the timespan value to assign</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, TimeSpan>> property, TimeSpan value)
        {
            return SetSocketOption<TimeSpan>(property, value);
        }

        /// <summary>
        /// Set a socket option that is other than an integer or timespan.
        /// </summary>
        /// <param name="property">the <see cref="TSocket"/> property to set</param>
        /// <param name="value">the object value to assign</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, object>> property, object value)
        {
            return SetSocketOption<object>(property, value);
        }

        /// <summary>
        /// Configure the contained socket to subscribe to a specific prefix.
        /// Note: This method should ONLY be called on a <see cref="SubscriberSocket"/>.
        /// </summary>
        /// <param name="prefix">a byte-array containing the prefix to which the socket will subscribe</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup Subscribe(byte[] prefix)
        {
            return AddSocketInitializer(s =>
            {
                var sck = s as SubscriberSocket;

                if (sck == null)
                {
                    string xMsg = String.Format("DeviceSocketSetup.Subscribe, this socket type {0} does not support Subscription.", s.GetType());
                    throw new InvalidException(xMsg);
                }

                sck.Subscribe(prefix);
            });
        }

        /// <summary>
        /// Configure the socket to subscribe to a specific prefix.
        /// Note: This method should ONLY be called on a <see cref="SubscriberSocket"/>.
        /// </summary>
        /// <param name="prefix">a string that denotes the prefix to which the socket will subscribe</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup Subscribe(string prefix)
        {
            return AddSocketInitializer(s =>
            {
                var sck = s as SubscriberSocket;

                if (sck == null)
                {
                    string xMsg = String.Format("DeviceSocketSetup.Subscribe, this socket type {0} does not support Subscription.", s.GetType());
                    throw new ArgumentException(xMsg);
                }

                sck.Subscribe(prefix);
            });
        }

        /// <summary>
        /// Add an action which will be performed on the socket when it's initialized.
        /// </summary>
        /// <param name="setupMethod">an Action to add to the list of initializers</param>
        /// <returns>this DeviceSocketSetup</returns>
        internal DeviceSocketSetup AddSocketInitializer(Action<NetMQSocket> setupMethod)
        {
            m_socketInitializers.Add(setupMethod);
            return this;
        }

        /// <summary>
        /// Performs initialization, binding, and connection actions on the socket.
        /// </summary>
        internal void Configure()
        {
            if (m_isConfigured)
                return;

            if (m_bindings.Count == 0 && m_connections.Count == 0)
                throw new InvalidOperationException("DeviceSocketSetup.Configure - device sockets must bind or connect to at least one endpoint.");

            foreach (var initializer in m_socketInitializers)
            {
                initializer.Invoke(m_socket);
            }

            foreach (var endpoint in m_bindings)
            {
                m_socket.Bind(endpoint);
            }

            foreach (string endpoint in m_connections)
            {
                m_socket.Connect(endpoint);
            }

            m_isConfigured = true;
        }

        /// <summary>
        /// Set a socket option, to the given generic type.
        /// </summary>
        /// <param name="property">the "T" property to set</param>
        /// <param name="value">the "T" value to assignt</param>
        /// <returns>the current DeviceSocketSetup</returns>
        private DeviceSocketSetup SetSocketOption<T>(Expression<Func<NetMQSocket, T>> property, T value)
        {
            var propertyInfo = property.Body is MemberExpression
                               ? ((MemberExpression)property.Body).Member as PropertyInfo
                               : ((MemberExpression)((UnaryExpression)property.Body).Operand).Member as PropertyInfo;

            if (propertyInfo == null)
                throw new InvalidOperationException("DeviceSocketSetup.SetSocketOption - the specified member is not a property: " + property.Body);

            m_socketInitializers.Add(s => propertyInfo.SetValue(s, value, null));

            return this;
        }
    }
}