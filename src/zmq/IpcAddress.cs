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
using System.Net;

public class IpcAddress : Address.IZAddress {

    private String name;
    
    public override String ToString() {
        if (name == null) {
            return null;
        }
        
        return "ipc://" + name;
    }

    public void resolve(String name_, bool ip4only_) {
        
        name = name_;
        
        int hash = name_.GetHashCode();
        if (hash < 0)
            hash = -hash;
        hash = hash % 55536;
        hash += 10000;
        
        
        try {
            address = new IPEndPoint(IPAddress.Loopback, hash);
        } catch (Exception ex) {
            throw new ArgumentException("",ex);
        }
    }

    public IPEndPoint address
    {
        get;
        private set;
    }

}
