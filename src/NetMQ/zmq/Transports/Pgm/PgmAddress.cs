using System;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;

namespace NetMQ.zmq.Transports.PGM
{
    internal sealed class PgmAddress : Address.IZAddress
    {
        public PgmAddress([NotNull] string network)
        {
            Resolve(network, true);
        }

        public PgmAddress()
        {
        }

        public void Resolve(string name, bool ip4Only)
        {
            int delimiter = name.LastIndexOf(':');
            if (delimiter < 0)
            {
                throw new InvalidException(String.Format("In PgmAddress.Resolve({0},{1}), delimiter ({2}) must be non-negative.", name, ip4Only, delimiter));
            }

            //  Separate the address/port.
            String addrStr = name.Substring(0, delimiter);
            String portStr = name.Substring(delimiter + 1);

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

            //  Remove square brackets around the address, if any.
            if (addrStr.Length >= 2 && addrStr[0] == '[' && addrStr[addrStr.Length - 1] == ']')
                addrStr = addrStr.Substring(1, addrStr.Length - 2);

            int port;
            //  Allow 0 specifically, to detect invalid port error in atoi if not
            if (portStr.Equals("*") || portStr.Equals("0"))
            {
                //  Resolve wildcard to 0 to allow autoselection of port
                port = 0;
            }
            else
            {
                //  Parse the port number (0 is not a valid port).
                port = Convert.ToInt32(portStr);
                
                if (port == 0)
                    throw new InvalidException(String.Format("In PgmAddress.Resolve({0},{1}), portStr ({2}) must denote a valid nonzero integer.", name, ip4Only, portStr));
            }

            if (addrStr.Equals("*"))
                addrStr = "0.0.0.0";

            IPAddress ipAddress;
            if (!IPAddress.TryParse(addrStr, out ipAddress))
                throw new InvalidException(String.Format("In PgmAddress.Resolve({0},{1}), addrStr ({2}) must be a valid IPAddress.", name, ip4Only, addrStr));

            Address = new IPEndPoint(ipAddress, port);
        }

        [CanBeNull]
        public IPAddress InterfaceAddress { get; private set; }

        public IPEndPoint Address { get; private set; }

        public override String ToString()
        {
            if (Address == null)
                return string.Empty;

            IPEndPoint endpoint = Address;

            if (endpoint.AddressFamily == AddressFamily.InterNetworkV6)
                return Protocol + "://[" + endpoint.AddressFamily + "]:" + endpoint.Port;
            else
                return Protocol + "://" + endpoint.Address + ":" + endpoint.Port;
        }

        public String Protocol
        {
            get { return zmq.Address.PgmProtocol; }
        }
    }
}
