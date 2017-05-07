/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace NetMQ.Core.Transports.Tcp
{
    /// <summary>
    /// A TcpAddress implements IZAddress, and contains an IPEndPoint (the Address property)
    /// and a Protocol property.
    /// </summary>
    internal sealed class TcpAddress : Address.IZAddress
    {
        /// <summary>
        /// Override ToString to provide a detailed description of this object's state
        /// in the form:  Protocol://[AddressFamily]:Port
        /// </summary>
        /// <returns>a string in the form Protocol://[AddressFamily]:Port</returns>
        public override string ToString()
        {
            if (Address == null)
                return string.Empty;

            var endpoint = Address;

            return endpoint.AddressFamily == AddressFamily.InterNetworkV6
                ? Protocol + "://[" + endpoint.AddressFamily + "]:" + endpoint.Port
                : Protocol + "://" + endpoint.Address + ":" + endpoint.Port;
        }

        /// <summary>
        /// Given a string that should identify an endpoint-address, resolve it to an actual IP address
        /// and set the Address property to a valid corresponding value.
        /// </summary>
        /// <param name="name">the endpoint-address to resolve</param>
        /// <param name="ip4Only">whether the address must be only-IPv4</param>
        /// <exception cref="InvalidException">The name must contain the colon delimiter.</exception>
        /// <exception cref="InvalidException">The specified port must be a valid nonzero integer.</exception>
        /// <exception cref="InvalidException">Must be able to find the IP-address.</exception>
        public void Resolve(string name, bool ip4Only)
        {
            // Find the ':' at end that separates address from the port number.
            int delimiter = name.LastIndexOf(':');
            if (delimiter < 0)
                throw new InvalidException($"TcpAddress.Resolve, delimiter ({delimiter}) must be non-negative.");

            // Separate the address/port.
            string addrStr = name.Substring(0, delimiter);
            string portStr = name.Substring(delimiter + 1);

            // Remove square brackets around the address, if any.
            if (addrStr.Length >= 2 && addrStr[0] == '[' && addrStr[addrStr.Length - 1] == ']')
                addrStr = addrStr.Substring(1, addrStr.Length - 2);

            // Get the port-number (or zero for auto-selection of a port).
            int port;
            // Allow 0 specifically, to detect invalid port error in atoi if not
            if (portStr == "*" || portStr == "0")
            {
                // Resolve wildcard to 0 to allow auto-selection of port
                port = 0;
            }
            else
            {
                // Parse the port number (0 is not a valid port).
                port = Convert.ToInt32(portStr);
                if (port == 0)
                {
                    throw new InvalidException($"TcpAddress.Resolve, port ({portStr}) must be a valid nonzero integer.");
                }
            }

            IPAddress ipAddress;

            // Interpret * as Any.
            if (addrStr == "*")
            {
                ipAddress = ip4Only
                    ? IPAddress.Any
                    : IPAddress.IPv6Any;
            }
            else if (!IPAddress.TryParse(addrStr, out ipAddress))
            {
#if NETSTANDARD1_3 || UAP
                var availableAddresses = Dns.GetHostEntryAsync(addrStr).Result.AddressList;
#else
                var availableAddresses = Dns.GetHostEntry(addrStr).AddressList;
#endif

                ipAddress = ip4Only
                    ? availableAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    : availableAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6);

                if (ipAddress == null)
                    throw new InvalidException($"TcpAddress.Resolve, unable to find an IP address for {name}");
            }

            Address = new IPEndPoint(ipAddress, port);
        }

        /// <summary>
        /// Get the Address implementation - which here is an IPEndPoint,
        /// which contains Address, AddressFamily, and Port properties.
        /// </summary>
        public IPEndPoint Address { get; private set; }

        /// <summary>
        /// Get the textual-representation of the communication protocol implied by this TcpAddress,
        /// which here is simply "tcp".
        /// </summary>
        public string Protocol => Core.Address.TcpProtocol;
    }
}
