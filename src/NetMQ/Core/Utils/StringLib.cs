using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetMQ.Core.Utils
{
    /// <summary>
    /// StringLib is a static class whose purpose is to provide miscellaneous string-related utility methods
    /// and extension properties.
    /// </summary>
    internal static class StringLib
    {
        #region AsString (List<string>)
        /// <summary>
        /// Return a string that gives detailed information of the state of the given List of Sockets,
        /// for debugging purposes.
        /// </summary>
        /// <param name="list">the List of Sockets to describe</param>
        /// <returns>a string detailing the list contents, including the state of each Socket</returns>
        /// <remarks>
        /// This is intended just for debugging purposes, as when adding detailed information to the Message of an
        /// exception when an error occurs. In DEBUG mode more detail is provided.
        /// </remarks>
        public static string AsString(List<Socket> list)
        {
            var sb = new StringBuilder();
            if (list == null)
            {
                sb.Append("(null)");
            }
            else
            {
                int n = list.Count;
                if (n == 0)
                {
                    sb.Append("(empty list)");
                }
                else
                {
                    if (n == 1)
                    {
                        sb.Append("List<Socket> with 1 Socket: ");
                        sb.Append(list[0].AsString());
                    }
                    else
                    {
                        sb.Append("List with ").Append(n).Append(" Sockets: ");

                        for (int i = 0; i < n; i++)
                        {
                            Socket socket = list[i];
                            string s = socket.AsString();
                            sb.Append(s);
                            if (i < n - 1)
                            {
                                sb.Append(", ");
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }
        #endregion

        #region AsString (Socket)
        /// <summary>
        /// Return a detailed textual description of the state of this Socket (if DEBUG is defined).
        /// </summary>
        /// <param name="socket">the System.Net.Sockets.Socket to describe</param>
        /// <returns>a string containing a detailed listing of the properties of the Socket</returns>
        /// <remarks>
        /// This is intended just for debugging purposes, as when adding detailed information to the Message of an
        /// exception when an error occurs. In DEBUG mode more detail is provided.
        /// </remarks>
        public static string AsString(this Socket socket)
        {
            var sb = new StringBuilder();
            if (socket == null)
            {
                sb.Append("(null Socket)");
            }
            else
            {
#if DEBUG
                sb.Append("Socket(");
                sb.Append(socket.SocketType);
                sb.Append(",AddressFamily=");
                sb.Append(socket.AddressFamily);
                sb.Append(",");
                if (socket.Available != 0)
                {
                    sb.Append("Available=");
                    sb.Append(socket.Available);
                    sb.Append(",");
                }
                if (socket.Blocking)
                {
                    sb.Append("Blocking,");
                }
                if (socket.Connected)
                {
                    sb.Append("Connected,");
                }
                if (socket.ExclusiveAddressUse)
                {
                    sb.Append("ExclusiveAddressUse,");
                }
                if (socket.IsBound)
                {
                    sb.Append("IsBound,");
                }
                if (socket.LingerState.Enabled)
                {
                    sb.Append("LingerTime=");
                    sb.Append(socket.LingerState.LingerTime);
                    sb.Append(",");
                }
                if (socket.LocalEndPoint != null)
                {
                    sb.Append("LocalEndPoint=");
                    sb.Append(socket.LocalEndPoint);
                    sb.Append(",");
                }
                if (socket.NoDelay)
                {
                    sb.Append("NoDelay,");
                }

                sb.Append("ProtocolType=");
                sb.Append(socket.ProtocolType);
                sb.Append(",");

                if (socket.ReceiveBufferSize != 0)
                {
                    sb.Append("ReceiveBufferSize=");
                    sb.Append(socket.ReceiveBufferSize);
                    sb.Append(",");
                }
                if (socket.SendBufferSize != 0)
                {
                    sb.Append("SendBufferSize=");
                    sb.Append(socket.SendBufferSize);
                    sb.Append(",");
                }
                if (socket.ReceiveTimeout != 0)
                {
                    sb.Append("ReceiveTimeout=");
                    sb.Append(socket.ReceiveTimeout);
                    sb.Append(",");
                }
                if (socket.SendTimeout != 0)
                {
                    sb.Append("SendTimeout=");
                    sb.Append(socket.SendTimeout);
                    sb.Append(",");
                }
                if (socket.RemoteEndPoint != null)
                {
                    sb.Append("RemoteEndPoint=");
                    sb.Append(socket.RemoteEndPoint);
                    sb.Append(",");
                }
                if (socket.Ttl != 0)
                {
                    sb.Append("Ttl=");
                    sb.Append(socket.Ttl);
                }
                sb.Append(")");
#else
                sb.Append("Socket");
#endif
            }
            return sb.ToString();
        }
        #endregion
    }
}
