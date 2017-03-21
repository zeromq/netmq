/*
    Copyright (c) 2012 Spotify AB
    Copyright (c) 2012-2015 Other contributors as noted in the AUTHORS file

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

using System.Net;
using JetBrains.Annotations;

namespace NetMQ.Core
{
    /// <summary>
    /// Class Address contains a specification of a protocol and an MqEndPoint.
    /// </summary>
    internal sealed class Address
    {
        /// <summary>
        /// The string-literal "inproc"
        /// - this denotes in-process, or inter-thread, communication.
        /// </summary>
        public const string InProcProtocol = "inproc";

        /// <summary>
        /// The string-literal "tcp"
        /// - this denotes TCP communication over the network.
        /// </summary>
        public const string TcpProtocol = "tcp";

        /// <summary>
        /// The string-literal "ipc"
        /// - this denotes inter-process communication, which on NetMQ is exactly the same as TCP.
        /// </summary>
        public const string IpcProtocol = "ipc";

        /// <summary>
        /// The string-literal "pgm"
        /// - this denotes the Pragmatic General Multicast (PGM) reliable multicast protocol.
        /// </summary>
        public const string PgmProtocol = "pgm";

        /// <summary>
        /// The string-literal "epgm"
        /// - this denotes the Encapsulated PGM protocol.
        /// </summary>
        public const string EpgmProtocol = "epgm";

        /// <summary>
        /// Interface IZAddress specifies that Resolve and property Address must be implemented.
        /// </summary>
        public interface IZAddress
        {
            void Resolve([NotNull] string name, bool ip4Only);

            [CanBeNull] IPEndPoint Address { get; }
            [NotNull] string Protocol { get; }
        }

        /// <summary>
        /// Create a new Address instance with the given protocol and text expression of an address.
        /// </summary>
        /// <param name="protocol">the protocol of this Address - as in tcp, ipc, pgm</param>
        /// <param name="address">a text representation of the address</param>
        public Address([NotNull] string protocol, [NotNull] string address)
        {
            Protocol = protocol;
            AddressString = address;
            Resolved = null;
        }

        /// <summary>
        /// Create a new Address instance based upon the given endpoint, assuming a protocol of tcp.
        /// </summary>
        /// <param name="endpoint">the subclass of EndPoint to base this Address upon</param>
        public Address([NotNull] EndPoint endpoint)
        {
            Protocol = TcpProtocol;

            var dnsEndPoint = endpoint as DnsEndPoint;
            if (dnsEndPoint != null)
            {
                AddressString = dnsEndPoint.Host + ":" + dnsEndPoint.Port;
                return;
            }

            var ipEndPoint = endpoint as IPEndPoint;
            if (ipEndPoint != null)
            {
                AddressString = ipEndPoint.Address + ":" + ipEndPoint.Port;
                return;
            }

            AddressString = endpoint.ToString();
        }


        public override string ToString()
        {
            if (Resolved != null)
            {
                switch (Protocol)
                {
                    case TcpProtocol: return Resolved.ToString();
                    case IpcProtocol: return Resolved.ToString();
                    case PgmProtocol: return Resolved.ToString();
                }
            }

            if (!string.IsNullOrEmpty(Protocol) && !string.IsNullOrEmpty(AddressString))
            {
                return Protocol + "://" + AddressString;
            }

            return null; //TODO: REVIEW - Although not explicitly prohibited, returning null from ToString seems sketchy; return string.Empty?
        }

        [NotNull]
        public string Protocol { get; }

        [NotNull]
        public string AddressString { get; }

        [CanBeNull]
        public IZAddress Resolved { get; set; }
    }
}
