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

namespace NetMQ.zmq
{
    internal class Address
    {
        public const string InProcProtocol = "inproc";
        public const string TcpProtocol = "tcp";
        public const string IpcProtocol = "ipc";
        public const string PgmProtocol = "pgm";
        public const string EpgmProtocol = "epgm";

        public interface IZAddress
        {
            void Resolve(String name, bool ip4Only);
            IPEndPoint Address { get; }
            String Protocol { get; }
        }


        public Address(String protocol, String address)
        {
            Protocol = protocol;
            AddressString = address;
            Resolved = null;
        }

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
