using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ.Devices
{
    /// <summary>
    /// This is a configuration for a socket
    /// that holds a list of endpoints to bind or connect to, of Actions to perform initialization on it,
    /// all to happen when the Configure method is called.
    /// </summary>
    public class DeviceSocketSetup
    {
        /// <summary>
        /// The socket associated with this DeviceSocketSetup.
        /// </summary>
        private readonly NetMQSocket m_socket;

        /// <summary>
        /// The list of initialization Actions to perform upon the socket when Configure is called.
        /// </summary>
        private readonly List<Action<NetMQSocket>> m_socketInitializers;

        /// <summary>
        /// The list of endpoints to bind to when Configure is called.
        /// </summary>
        private readonly List<string> m_bindings;

        /// <summary>
        /// The list of endpoints to connect to when Configure is called.
        /// </summary>
        private readonly List<string> m_connections;

        /// <summary>
        /// This indicates whether the Configure method has been called yet.
        /// </summary>
        private bool m_isConfigured;

        /// <summary>
        /// The constructor: create a new DeviceSocketSetup object associated with the given NetMQSocket.
        /// </summary>
        /// <param name="socket">the NetMQSocket to associate with this DeviceSocketSetup</param>
        /// <exception cref="ArgumentNullException">socket must not be null.</exception>
        internal DeviceSocketSetup([NotNull] NetMQSocket socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            m_socket = socket;
            m_socketInitializers = new List<Action<NetMQSocket>>();
            m_bindings = new List<string>();
            m_connections = new List<string>();
        }

        /// <summary>
        /// Configure the socket to bind to a given endpoint.
        /// This simply adds this endpoint to the list to bind to when the Configure method is called.
        /// </summary>
        /// <param name="endpoint">a string representing the endpoint to which the socket will bind (must not be null)</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        /// <exception cref="ArgumentNullException">endpoint must not be null.</exception>
        public DeviceSocketSetup Bind([NotNull] string endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            m_bindings.Add(endpoint);
            return this;
        }

        /// <summary>
        /// Configure the socket to connect to a given endpoint.
        /// This simply adds this endpoint to the list to connect to when the Configure method is called.
        /// </summary>
        /// <param name="endpoint">a string representing the endpoint to which the socket will connect (must not be null)</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        /// <exception cref="ArgumentNullException">endpoint must not be null.</exception>
        public DeviceSocketSetup Connect([NotNull] string endpoint)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            m_connections.Add(endpoint);
            return this;
        }

        /// <summary>
        /// Set an integer-based socket option.
        /// </summary>
        /// <param name="property">an <see cref="Expression"/> that denotes the property to set</param>
        /// <param name="value">the integer value to assign</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, int>> property, int value)
        {
            return SetSocketOption<int>(property, value);
        }

        /// <summary>
        /// Set an timespan-based socket option.
        /// </summary>
        /// <param name="property">an <see cref="Expression"/> that denotes the property to set</param>
        /// <param name="value">the timespan value to assign</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, TimeSpan>> property, TimeSpan value)
        {
            return SetSocketOption<TimeSpan>(property, value);
        }

        /// <summary>
        /// Set a socket option that is other than an integer or timespan.
        /// </summary>
        /// <param name="property">an <see cref="Expression"/> that denotes the property to set</param>
        /// <param name="value">the object value to assign</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, object>> property, object value)
        {
            return SetSocketOption<object>(property, value);
        }

        /// <summary>
        /// Add the given byte-array prefix to the list that the socket is to subscribe to when Configure is called.
        /// Note: This method should ONLY be called on a <see cref="SubscriberSocket"/>.
        /// </summary>
        /// <param name="prefix">a byte-array containing the prefix to which the socket will subscribe</param>
        /// <returns>the current <see cref="DeviceSocketSetup"/> object</returns>
        /// <exception cref="InvalidException">The socket type must support Subscription.</exception>
        public DeviceSocketSetup Subscribe(byte[] prefix)
        {
            return AddSocketInitializer(s =>
            {
                var sck = s as SubscriberSocket;

                if (sck == null)
                {
                    string xMsg = $"DeviceSocketSetup.Subscribe, this socket type {s.GetType()} does not support Subscription.";
                    throw new InvalidException(xMsg);
                }

                sck.Subscribe(prefix);
            });
        }

        /// <summary>
        /// Add the given string prefix to the list that the socket is to subscribe to when Configure is called.
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
                    string xMsg = $"DeviceSocketSetup.Subscribe, this socket type {s.GetType()} does not support Subscription.";
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
        /// Perform the initializations, bindings, and connections on the socket.
        /// </summary>
        /// <exception cref="InvalidOperationException">Device socket must bind or connect to at least one endpoint.</exception>
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
        /// <param name="value">the "T" value to assign</param>
        /// <returns>the current DeviceSocketSetup</returns>
        /// <exception cref="InvalidOperationException">The specified property must be a valid member of the type.</exception>
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