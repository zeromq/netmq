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
using System.Net;

public class TcpAddress : Address.IZAddress {

    public class TcpAddressMask : TcpAddress {
        public bool match_address(IPEndPoint addr_) {
            return address.Equals(addr_); 
        }
    }

    //protected IPEndPoint address;
    
    public TcpAddress(String addr_) {
        resolve(addr_, false);
    }
    public TcpAddress() {
    }
    
    public override String ToString() {
        if (address == null) {
            return null;
        }

                IPEndPoint endpoint = (IPEndPoint)address;
        
        if (endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
            return "tcp://[" + endpoint .AddressFamily.ToString() + "]:" + endpoint.Port;
        } else {
            return "tcp://" + endpoint.Address.ToString() + ":" + endpoint.Port;
        }
    }
    

    public void resolve(String name_, bool ipv4only_) {
        //  Find the ':' at end that separates address from the port number.
        int delimiter = name_.LastIndexOf(':');
        if (delimiter < 0) {
            throw new ArgumentException(name_);
        }

        //  Separate the address/port.
        String addr_str = name_.Substring(0, delimiter); 
        String port_str = name_.Substring(delimiter+1);
        
        //  Remove square brackets around the address, if any.
        if (addr_str.Length >= 2 && addr_str[0] == '[' &&
              addr_str[addr_str.Length - 1] == ']')
            addr_str = addr_str.Substring (1, addr_str.Length - 2);

        int port;
        //  Allow 0 specifically, to detect invalid port error in atoi if not
        if (port_str.Equals("*") || port_str.Equals("0"))
            //  Resolve wildcard to 0 to allow autoselection of port
            port = 0;
        else {
            //  Parse the port number (0 is not a valid port).
            port = Convert.ToInt32(port_str);
            if (port == 0) {
                throw new ArgumentException(name_);
            }
        }

        IPEndPoint addr_net = null;

        if (addr_str.Equals("*")) {
            addr_str = "0.0.0.0";
        }
        //try {
        //    for(InetAddress ia: IPEndPoint .getAllByName(addr_str)) {
        //        if (ipv4only_ && (ia is Inet6Address)) {
        //            continue;
        //        }
        //        addr_net = ia;
        //        break;
        //    }
        //} catch (UnknownHostException e) {
        //    throw new ArgumentException(e);
        //}

        IPAddress ipAddress;

        if (!IPAddress.TryParse(addr_str, out ipAddress))
        {
            throw new ArgumentException();
        }

        addr_net  = new IPEndPoint(ipAddress, port);
        
        //if (addr_net == null) {
        //    throw new ArgumentException(name_);
        //}
            
        address = addr_net;

    }

    public IPEndPoint address
    {
        get;
        private set;
    }

}
