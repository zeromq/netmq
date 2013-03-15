using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NetMQ.Sockets;

namespace NetMQ.Devices
{

	/// <summary>
	/// Configures the given socket
	/// </summary>
	/// <typeparam name="TSocket"></typeparam>
	public class DeviceSocketSetup
	{

		private readonly NetMQSocket m_socket;
		private readonly List<Action<NetMQSocket>> m_socketInitializers;
		private readonly List<string> m_bindings;
		private readonly List<string> m_connections;

		private bool m_isConfigured;

		internal DeviceSocketSetup(NetMQSocket socket)
		{
			if (socket == null)
				throw new ArgumentNullException("socket");

			m_socket = socket;
			m_socketInitializers = new List<Action<NetMQSocket>>();
			m_bindings = new List<string>();
			m_connections = new List<string>();
		}

		/// <summary>
		/// Configure the socket to bind to a given endpoint. 
		/// </summary>
		/// <param name="endpoint">A string representing the endpoint to which the socket will bind.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup Bind(string endpoint)
		{
			if (endpoint == null)
				throw new ArgumentNullException("endpoint");

			m_bindings.Add(endpoint);
			return this;
		}

		/// <summary>
		/// Configure the socket to connect to a given endpoint.
		/// </summary>
		/// <param name="endpoint">A string representing the endpoint to which the socket will connect.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup Connect(string endpoint) {
			if (endpoint == null)
				throw new ArgumentNullException("endpoint");

			m_connections.Add(endpoint);
			return this;
		}

		/// <summary>
		/// Set an int-based socket option.
		/// </summary>
		/// <param name="property">The <see cref="TSocket"/> property to set.</param>
		/// <param name="value">The int value to assign.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, int>> property, int value) {
			return SetSocketOption<int>(property, value);
		}

		/// <summary>
		/// Set an timespan-based socket option.
		/// </summary>
		/// <param name="property">The <see cref="TSocket"/> property to set.</param>
		/// <param name="value">The timespan value to assign.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, TimeSpan>> property, TimeSpan value)
		{
			return SetSocketOption<TimeSpan>(property, value);
		}

		/// <summary>
		/// Set other socket option.
		/// </summary>
		/// <param name="property">The <see cref="TSocket"/> property to set.</param>
		/// <param name="value">The object value to assign.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup SetSocketOption(Expression<Func<NetMQSocket, object>> property, object value)
		{
			return SetSocketOption<object>(property, value);
		}

		/// <summary>
		/// Configure the socket to subscribe to a specific prefix. 
		/// Note: This method should ONLY be called on <typeparam name="TSocket" />
		/// of type <see cref="SubscriberSocket"/>.
		/// </summary>
		/// <param name="prefix">A byte array containing the prefix to which the socket will subscribe.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup Subscribe(byte[] prefix) {
			return AddSocketInitializer(s => {
				var sck = s as SubscriberSocket;

				if (sck == null)
					throw new ArgumentException("This socket type does not support Subscription");

				sck.Subscribe(prefix);
			});
		}

		/// <summary>
		/// Configure the socket to subscribe to a specific prefix.
		/// Note: This method should ONLY be called on <typeparam name="TSocket" />
		/// of type <see cref="SubscriberSocket"/>.
		/// </summary>
		/// <param name="prefix">A byte array containing the prefix to which the socket will subscribe.</param>
		/// <returns>The current <see cref="DeviceSocketSetup"/> object.</returns>
		public DeviceSocketSetup Subscribe(string prefix) {
			return AddSocketInitializer(s => {
				var sck = s as SubscriberSocket;

				if (sck == null)
					throw new ArgumentException("This socket type does not support Subscription");

				sck.Subscribe(prefix);
			});
		}

		/// <summary>
		/// Adds an action which will be performed on the socket when its initialized.
		/// </summary>
		/// <param name="setupMethod"></param>
		/// <returns></returns>
		internal DeviceSocketSetup AddSocketInitializer(Action<NetMQSocket> setupMethod)
		{
			m_socketInitializers.Add(setupMethod);
			return this;
		}

		/// <summary>
		/// Performs initialization actions, bindings and connections on the socket.
		/// </summary>
		internal void Configure() {
			if(m_isConfigured)
				return;

			if (m_bindings.Count == 0 && m_connections.Count == 0)
				throw new InvalidOperationException("Device sockets must bind or connect to at least one endpoint.");

			foreach (var initializer in m_socketInitializers)
				initializer.Invoke(m_socket);

			foreach (var endpoint in m_bindings) {
				m_socket.Bind(endpoint);
			}

			foreach (string endpoint in m_connections) {
				m_socket.Connect(endpoint);
			}

			m_isConfigured = true;
		}

		private DeviceSocketSetup SetSocketOption<T>(Expression<Func<NetMQSocket, T>> property, T value)
		{
			var propertyInfo = property.Body is MemberExpression
							   ? ((MemberExpression)property.Body).Member as PropertyInfo
							   : ((MemberExpression)((UnaryExpression)property.Body).Operand).Member as PropertyInfo;

			if (propertyInfo == null)
				throw new InvalidOperationException("The specified member is not a property: " + property.Body);

			m_socketInitializers.Add(s => propertyInfo.SetValue(s, value, null));

			return this;
		}		
	}
}