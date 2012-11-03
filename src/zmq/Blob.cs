/*
    Copyright (c) 2010 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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
using System.Runtime.InteropServices;

public class Blob {

    private byte[] buf;
    private int hash = 0;
    
    public Blob(byte[] data_) {
        buf = new byte[data_.Length];
        data_.CopyTo(buf, 0);
    }
    
    public Blob(int size) {
       buf = new byte[size];
    }
    
    //public Blob(ByteBuffer buf_) {
    //   buf = Utils.bytes(buf_);
    //}

    public Blob put(int pos, byte b) {
        buf[pos] = b;
        hash = 0;
        return this;
    }
    
    public Blob put(int pos, byte[] data) {
        //System.arraycopy(data, 0, buf, pos, data.Length);
        
        for (int i = 0; i < data.Length; i++)
			{
                buf[i+pos]= data[i];
			}
        
        hash = 0;
        return this;
    }

    public int size() {
        return buf.Length;
    }

    public byte[] data() {
        return buf;
    }

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int memcmp(byte[] b1, byte[] b2, long count);
    
    public override bool Equals(Object t) {

        if (t is Blob)
        {
            Blob b = (Blob)t;
            if (b.buf.Length != buf.Length)
            {
                return false;
            }

            return memcmp(buf, b.buf, buf.Length) == 0;
        }
            return false;
    }
    
    public override int GetHashCode() {
        if (hash == 0) {
            foreach (byte b in buf) {
                hash = 31 * hash + b;
            }
        }
        return hash;
    }
}
