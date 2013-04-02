/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

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

namespace NetMQ.zmq
{
	public class TcpAddress : Address.IZAddress {

		public class TcpAddressMask : TcpAddress {
			public bool MatchAddress(IPEndPoint addr) {
				return Address.Equals(addr); 
			}
		}

		//protected IPEndPoint address;
    
		public TcpAddress(String addr) {
			Resolve(addr, false);
		}
		public TcpAddress() {
		}
    
		public override String ToString() {
			if (Address == null) {
				return string.Empty;
			}

			IPEndPoint endpoint = Address;
        
			if (endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
				return "tcp://[" + endpoint .AddressFamily.ToString() + "]:" + endpoint.Port;
			} else {
				return "tcp://" + endpoint.Address.ToString() + ":" + endpoint.Port;
			}
		}
    

		public void Resolve(String name, bool ip4Only)
		{
			//  Find the ':' at end that separates address from the port number.
			int delimiter = name.LastIndexOf(':');
			if (delimiter < 0) {
				throw InvalidException.Create();
			}

			//  Separate the address/port.
			String addrStr = name.Substring(0, delimiter); 
			String portStr = name.Substring(delimiter+1);
        
			//  Remove square brackets around the address, if any.
			if (addrStr.Length >= 2 && addrStr[0] == '[' &&
			    addrStr[addrStr.Length - 1] == ']')
				addrStr = addrStr.Substring (1, addrStr.Length - 2);

			int port;
			//  Allow 0 specifically, to detect invalid port error in atoi if not
			if (portStr.Equals("*") || portStr.Equals("0"))
				//  Resolve wildcard to 0 to allow autoselection of port
				port = 0;
			else {
				//  Parse the port number (0 is not a valid port).
				port = Convert.ToInt32(portStr);
				if (port == 0) {
					throw InvalidException.Create();
				}
			}

			IPEndPoint addrNet = null;

			if (addrStr.Equals("*")) {
				addrStr = "0.0.0.0";
			}

			IPAddress ipAddress;
 
			if (!IPAddress.TryParse(addrStr, out ipAddress))
			{
				ipAddress = Dns.GetHostEntry(addrStr).AddressList.FirstOrDefault();

				if (ipAddress == null)
				{
					throw InvalidException.Create(string.Format("Unable to find an IP address for {0}", name));
				}
			}

			addrNet  = new IPEndPoint(ipAddress, port);
        
			//if (addr_net == null) {
			//    throw new ArgumentException(name_);
			//}
            
			Address = addrNet;

		}

		public IPEndPoint Address
		{
			get;
			private set;
		}

	}
}
