using System.Collections;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace NetMQ.Core.Utils
{
    /// <summary>
    /// This class exists only to provide a wrapper for the Socket.Select method,
    /// such that the call may be handled slightly differently on .NET 3.5 as opposed to later versions.
    /// </summary>
    internal static class SocketUtility
    {
        /// <summary>
        /// Determine the status of one or more sockets.
        /// After returning, the lists will be filled with only those sockets that satisfy the conditions.
        /// </summary>
        /// <param name="checkRead">a list of Sockets to check for readability</param>
        /// <param name="checkWrite">a list of Sockets to check for writability</param>
        /// <param name="checkError">a list of Sockets to check for errors</param>
        /// <param name="microSeconds">a timeout value, in microseconds. A value of -1 indicates an infinite timeout.</param>
        /// <remarks>
        /// If you are in a listening state, readability means that a call to Accept will succeed without blocking.
        /// If you have already accepted the connection, readability means that data is available for reading. In these cases,
        /// all receive operations will succeed without blocking. Readability can also indicate whether the remote Socket
        /// has shut down the connection - in which case a call to Receive will return immediately, with zero bytes returned.
        ///
        /// Select returns when at least one of the sockets of interest (ie any of the sockets in the checkRead, checkWrite, or checkError
        /// lists) meets its specified criteria, or the microSeconds parameter is exceeded - whichever comes first.
        /// Setting microSeconds to -1 specifies an infinite timeout.
        ///
        /// If you make a non-blocking call to Connect, writability means that you have connected successfully. If you already
        /// have a connection established, writability means that all send operations will succeed without blocking.
        /// If you have made a non-blocking call to Connect, the checkError parameter identifies sockets that have not connected successfully.
        ///
        /// See this reference for further details of the operation of the Socket.Select method:
        /// https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.select(v=vs.110).aspx
        ///
        /// This may possibly throw an ArgumentNullException, if all three lists are null or empty,
        /// and a SocketException if an error occurred when attempting to access a socket.
        ///
        /// Use the Poll method if you only want to determine the status of a single Socket.
        ///
        /// This method cannot detect certain kinds of connection problems,
        /// such as a broken network cable, or that the remote host was shut down ungracefully.
        /// You must attempt to send or receive data to detect these kinds of errors.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">none of the three lists of sockets may be null.</exception>
        /// <exception cref="SocketException">an error occurred when attempting to access the socket.</exception>
        public static void Select([CanBeNull] IList checkRead, [CanBeNull] IList checkWrite, [CanBeNull] IList checkError, int microSeconds)
        {
#if NET35
            // .NET 3.5 has a bug, such that -1 is not blocking the select call - therefore we use here instead the maximum integer value.
            if (microSeconds == -1)
                microSeconds = int.MaxValue;
#endif

            Socket.Select(checkRead, checkWrite, checkError, microSeconds);
        }
    }
}
