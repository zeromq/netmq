/*
    Copyright (c) 2011 250bpm s.r.o.
    Copyright (c) 2011 Other contributors as noted in the AUTHORS file

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

// fake Unix domain socket
namespace NetMQ.zmq
{
	public class IpcListener : TcpListener {

		private readonly IpcAddress m_address;
    
		public IpcListener(IOThread ioThread, SocketBase socket, Options options) :base(ioThread, socket, options)
		{
			m_address = new IpcAddress();
		}

		// Get the bound address for use with wildcards
		public override String Address
		{get
			{
				return m_address.ToString();
			}
		}
    

		//  Set address to listen on.
		public override void SetAddress(String addr)
		{        
			m_address.Resolve (addr, false);
        
			String fake = m_address.Address.Address + ":" + m_address.Address.Port;
			base.SetAddress(fake);
		}


    
	}
}
