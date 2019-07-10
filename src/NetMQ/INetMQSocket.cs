using System;
using JetBrains.Annotations;
using NetMQ.Core.Utils;

namespace NetMQ
{
	public interface INetMQSocket : IOutgoingSocket, IReceivingSocket, ISocketPollable, IDisposable
	{
		/// <summary>
		/// This event occurs when at least one message may be received from the socket without blocking.
		/// </summary>
		/// <remarks>
		/// This event is raised when a <see cref="NetMQSocket"/> is added to a running <see cref="Poller"/>.
		/// </remarks>
		event EventHandler<NetMQSocketEventArgs> ReceiveReady;

		/// <summary>
		/// This event occurs when at least one message may be sent via the socket without blocking.
		/// </summary>
		/// <remarks>
		/// This event is raised when a <see cref="NetMQSocket"/> is added to a running <see cref="Poller"/>.
		/// </remarks>
		event EventHandler<NetMQSocketEventArgs> SendReady;

		/// <summary>
		/// Get the <see cref="SocketOptions"/> of this socket.
		/// </summary>
		SocketOptions Options { get; }

		/// <summary>
		/// Get whether a message is waiting to be picked up (<c>true</c> if there is, <c>false</c> if there is none).
		/// </summary>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		bool HasIn { get; }

		/// <summary>
		/// Get whether a message is waiting to be sent.
		/// </summary>
		/// <remarks>
		/// This is <c>true</c> if at least one message is waiting to be sent, <c>false</c> if there is none.
		/// </remarks>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		bool HasOut { get; }

		/// <summary>
		/// Bind the socket to <paramref name="address"/>.
		/// </summary>
		/// <param name="address">a string representing the address to bind this socket to</param>
		/// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		/// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
		/// <exception cref="NetMQException">No IO thread was found, or the protocol's listener encountered an
		/// error during initialisation.</exception>
		void Bind([NotNull] string address);

		/// <summary>Binds the specified TCP <paramref name="address"/> to an available port, assigned by the operating system.</summary>
		/// <returns>the chosen port-number</returns>
		/// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
		/// <exception cref="ProtocolNotSupportedException"><paramref name="address"/> uses a protocol other than TCP.</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		/// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
		/// <exception cref="NetMQException">No IO thread was found, or the protocol's listener errored during
		/// initialisation.</exception>
		int BindRandomPort([NotNull] string address);

		/// <summary>
		/// Connect the socket to <paramref name="address"/>.
		/// </summary>
		/// <param name="address">a string denoting the address to connect this socket to</param>
		/// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		/// <exception cref="NetMQException">No IO thread was found.</exception>
		/// <exception cref="AddressAlreadyInUseException">The specified address is already in use.</exception>
		void Connect([NotNull] string address);

		/// <summary>
		/// Disconnect this socket from <paramref name="address"/>.
		/// </summary>
		/// <param name="address">a string denoting the address to disconnect from</param>
		/// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		/// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
		void Disconnect([NotNull] string address);

		/// <summary>
		/// Unbind this socket from <paramref name="address"/>.
		/// </summary>
		/// <param name="address">a string denoting the address to unbind from</param>
		/// <exception cref="ObjectDisposedException">thrown if the socket was already disposed</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		/// <exception cref="EndpointNotFoundException">Endpoint was not found and cannot be disconnected.</exception>
		void Unbind([NotNull] string address);

		/// <summary>Closes this socket, rendering it unusable. Equivalent to calling <see cref="NetMQSocket.Dispose"/>.</summary>
		void Close();

		/// <summary>
		/// Wait until a message is ready to be received from the socket.
		/// </summary>
		void Poll();

		/// <summary>
		/// Wait until a message is ready to be received/sent from this socket or until timeout is reached.
		/// If a message is available, the ReceiveReady/SendReady event is fired.
		/// </summary>
		/// <param name="timeout">a TimeSpan that represents the timeout-period</param>
		/// <returns>true if a message was available within the timeout, false otherwise</returns>
		bool Poll(TimeSpan timeout);

		/// <summary>
		/// Poll this socket, which means wait for an event to happen within the given timeout period.
		/// </summary>
		/// <param name="pollEvents">the poll event(s) to listen for</param>
		/// <param name="timeout">the timeout period</param>
		/// <returns>
		/// PollEvents.None     -> no message available
		/// PollEvents.PollIn   -> at least one message has arrived
		/// PollEvents.PollOut  -> at least one message is ready to send
		/// PollEvents.Error    -> an error has occurred
		/// or any combination thereof
		/// </returns>
		/// <exception cref="FaultException">The internal select operation failed.</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		PollEvents Poll(PollEvents pollEvents, TimeSpan timeout);

		/// <summary>
		/// Listen to the given endpoint for SocketEvent events.
		/// </summary>
		/// <param name="endpoint">A string denoting the endpoint to monitor</param>
		/// <param name="events">The specific <see cref="SocketEvents"/> events to report on. Defaults to <see cref="SocketEvents.All"/> if omitted.</param>
		/// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException"><paramref name="endpoint"/> cannot be empty or whitespace.</exception>
		/// <exception cref="ObjectDisposedException">This object is already disposed.</exception>
		/// <exception cref="ProtocolNotSupportedException">The protocol of <paramref name="endpoint"/> is not supported.</exception>
		/// <exception cref="TerminatingException">The socket has been stopped.</exception>
		/// <exception cref="NetMQException">Maximum number of sockets reached.</exception>
		void Monitor([NotNull] string endpoint, SocketEvents events = SocketEvents.All);
	}
}