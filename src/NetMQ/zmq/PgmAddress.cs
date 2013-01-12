using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace NetMQ.zmq
{
	public class PgmAddress : Address.IZAddress
	{
		private string m_network;
		
		public PgmAddress(string network)
		{
			Resolve(network, true);
		}

		public PgmAddress()
		{
		}

		public void Resolve(string name, bool ip4Only)
		{
			m_network = name;

			int delimiter = name.LastIndexOf(':');
			if (delimiter < 0)
			{
				throw InvalidException.Create();
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
			if (addrStr.Length >= 2 && addrStr[0] == '[' &&
					addrStr[addrStr.Length - 1] == ']')
				addrStr = addrStr.Substring(1, addrStr.Length - 2);

			int port;
			//  Allow 0 specifically, to detect invalid port error in atoi if not
			if (portStr.Equals("*") || portStr.Equals("0"))
				//  Resolve wildcard to 0 to allow autoselection of port
				port = 0;
			else
			{
				//  Parse the port number (0 is not a valid port).
				port = Convert.ToInt32(portStr);
				if (port == 0)
				{
					throw InvalidException.Create();
				}
			}

			IPEndPoint addrNet = null;

			if (addrStr.Equals("*"))
			{
				addrStr = "0.0.0.0";
			}
			
			IPAddress ipAddress;

			if (!IPAddress.TryParse(addrStr, out ipAddress))
			{
				throw InvalidException.Create();
			}

			addrNet = new IPEndPoint(ipAddress, port);
			
			Address = addrNet;
		}

		public IPAddress InterfaceAddress { get; private set; }

		public IPEndPoint Address { get; set; }

		public override String ToString()
		{
			if (Address == null)
			{
				return string.Empty;
			}

			IPEndPoint endpoint = Address;

			if (endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
			{
				return "pgm://[" + endpoint.AddressFamily.ToString() + "]:" + endpoint.Port;
			}
			else
			{
				return "pgm://" + endpoint.Address.ToString() + ":" + endpoint.Port;
			}
		}
	}
}
