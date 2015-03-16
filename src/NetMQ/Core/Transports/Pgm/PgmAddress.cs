using System;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace NetMQ.Core.Transports.Pgm
{
    internal sealed class PgmAddress : Address.IZAddress
    {
        /// <exception cref="InvalidException">Unable to parse the address's port number, or the IP address could not be parsed.</exception>
        public PgmAddress([NotNull] string network)
        {
            Resolve(network, true);
        }

        public PgmAddress()
        {
        }

        /// <exception cref="InvalidException">Unable to parse the address's port number, or the IP address could not be parsed.</exception>
        public void Resolve(string name, bool ip4Only)
        {
            int delimiter = name.LastIndexOf(':');
            if (delimiter < 0)
            {
                throw new InvalidException(string.Format("In PgmAddress.Resolve({0},{1}), delimiter ({2}) must be non-negative.", name, ip4Only, delimiter));
            }

            // Separate the address/port.
            string addrStr = name.Substring(0, delimiter);
            string portStr = name.Substring(delimiter + 1);

            if (addrStr.Contains(";"))
            {
                int semiColonDelimiter = addrStr.IndexOf(";");
                string interfaceIP = addrStr.Substring(0, semiColonDelimiter);
                addrStr = addrStr.Substring(semiColonDelimiter + 1);

                InterfaceAddress = IPAddress.Parse(interfaceIP);
            }
            else
            {
                InterfaceAddress = null;
            }

            // Remove square brackets around the address, if any.
            if (addrStr.Length >= 2 && addrStr[0] == '[' && addrStr[addrStr.Length - 1] == ']')
                addrStr = addrStr.Substring(1, addrStr.Length - 2);

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
                    throw new InvalidException(string.Format("In PgmAddress.Resolve({0},{1}), portStr ({2}) must denote a valid nonzero integer.", name, ip4Only, portStr));
            }

            if (addrStr == "*")
                addrStr = "0.0.0.0";

            IPAddress ipAddress;
            if (!IPAddress.TryParse(addrStr, out ipAddress))
                throw new InvalidException(string.Format("In PgmAddress.Resolve({0},{1}), addrStr ({2}) must be a valid IPAddress.", name, ip4Only, addrStr));

            Address = new IPEndPoint(ipAddress, port);
        }

        [CanBeNull]
        public IPAddress InterfaceAddress { get; private set; }

        public IPEndPoint Address { get; private set; }

        public override string ToString()
        {
            if (Address == null)
                return string.Empty;

            IPEndPoint endpoint = Address;

            return endpoint.AddressFamily == AddressFamily.InterNetworkV6 
                ? Protocol + "://[" + endpoint.AddressFamily + "]:" + endpoint.Port 
                : Protocol + "://" + endpoint.Address + ":" + endpoint.Port;
        }

        public string Protocol
        {
            get { return Core.Address.PgmProtocol; }
        }
    }
}
