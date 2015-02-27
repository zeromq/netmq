/*
    Copyright (c) 2012 Spotify AB
    Copyright (c) 2012 Other contributors as noted in the AUTHORS file

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

using System;
using System.Net;
using JetBrains.Annotations;

namespace NetMQ.zmq
{
    /// <summary>
    /// Class Address contains a specification of a protocol and an MqEndPoint.
    /// </summary>
    internal sealed class Address
    {
        /// <summary>
        /// This is the string-literal "inproc".
        /// </summary>
        public const string InProcProtocol = "inproc";

        /// <summary>
        /// This is the string-literal "tcp".
        /// </summary>
        public const string TcpProtocol = "tcp";

        /// <summary>
        /// This is the string-literal "ipc".
        /// </summary>
        public const string IpcProtocol = "ipc";

        /// <summary>
        /// This is the string-literal "pgm".
        /// </summary>
        public const string PgmProtocol = "pgm";

        /// <summary>
        /// This is the string-literal "epgm".
        /// </summary>
        public const string EpgmProtocol = "epgm";

        /// <summary>
        /// interface IZAddress specifies that Resolve and property Address must be implemented.
        /// </summary>
        public interface IZAddress
        {
            void Resolve([NotNull] String name, bool ip4Only);
            
            [CanBeNull] IPEndPoint Address { get; }
            [NotNull] String Protocol { get; }
        }

        /// <summary>
        /// Create a new Address instance with the given protocol and text expression of an address.
        /// </summary>
        /// <param name="protocol">the protocol of this Address - as in tcp, ipc, pgm</param>
        /// <param name="address">a text representation of the address</param>
        public Address(String protocol, String address)
        {
            Protocol = protocol;
            AddressString = address;
            Resolved = null;
        }

        /// <summary>
        /// Create a new Address instance based upon the given endpoint, assuming a protocol of tcp.
        /// </summary>
        /// <param name="endpoint">the subclass of EndPoint to base this Address upon</param>
        public Address(EndPoint endpoint)
        {
            Protocol = TcpProtocol;

            if (endpoint is DnsEndPoint)
            {
                DnsEndPoint dnsEndpoint = (DnsEndPoint)endpoint;
                AddressString = dnsEndpoint.Host + ":" + dnsEndpoint.Port;
            }
            else if (endpoint is IPEndPoint)
            {
                IPEndPoint ipEndpoint = (IPEndPoint)endpoint;
                AddressString = ipEndpoint.Address + ":" + ipEndpoint.Port;
            }
            else
            {
                AddressString = endpoint.ToString();
            }
        }


        public override String ToString()
        {
            if (Protocol.Equals(TcpProtocol))
            {
                if (Resolved != null)
                {
                    return Resolved.ToString();
                }
            }
            else if (Protocol.Equals(IpcProtocol))
            {
                if (Resolved != null)
                {
                    return Resolved.ToString();
                }
            }
            else if (Protocol.Equals(PgmProtocol))
            {
                if (Resolved != null)
                {
                    return Resolved.ToString();
                }
            }

            if (!string.IsNullOrEmpty(Protocol) && !string.IsNullOrEmpty(AddressString))
            {
                return Protocol + "://" + AddressString;
            }

            return null; //TODO: REVIEW - Although not explicitly prohibited, returning null from ToString seems sketchy; return string.Empty? 
        }

        public String Protocol { get; private set; }

        public String AddressString { get; private set; }

        public IZAddress Resolved { get; set; }
    }
}
