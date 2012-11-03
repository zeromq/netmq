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

public class Address
{

    public interface IZAddress
    {
        //String ToString();
        void resolve(String name_, bool ip4only_);
        IPEndPoint address{get;}        
    };


    public Address(String protocol_, String address_)
    {
        protocol = protocol_;
        address = address_;
        resolved = null;
    }

    public Address(EndPoint endpoint)
    {
        protocol = "tcp";

        if (endpoint is DnsEndPoint)
        {
            DnsEndPoint dnsEndpoint = endpoint as DnsEndPoint;
            address = dnsEndpoint.Host + ":" + dnsEndpoint.Port.ToString();
        }
        else if (endpoint is IPEndPoint)
        {
            IPEndPoint ipEndpoint = endpoint as IPEndPoint;
            address = ipEndpoint.Address.ToString() + ":" + ipEndpoint.Port.ToString();
        }
        else
        {
            address = endpoint.ToString();
        }
    }


    public override String ToString()
    {
        if (protocol.Equals("tcp"))
        {
            if (resolved != null)
            {
                return resolved.ToString();
            }
        }
        else if (protocol.Equals("ipc"))
        {
            if (resolved != null)
            {
                return resolved.ToString();
            }
        }

        if (!string.IsNullOrEmpty(protocol) && !string.IsNullOrEmpty(address))
        {
            return protocol + "://" + address;
        }

        return null;
    }

    public String protocol
    {
        get;
        private set;
    }

    public String address
    {
        get;
        private set;
    }

    public IZAddress resolved
    {
        get;
        set;
    }
}
