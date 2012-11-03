/*
    Copyright other contributors as noted in the AUTHORS file.
    
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
using System.Threading;


public class ZError  {
    
    private static ThreadLocal<int> s_errno = new ThreadLocal<int>(() => 0);
    
    //private static ThreadLocal<Throwable> s_exc = new ThreadLocal<Throwable>();


    public const int EINTR = 4;
    public const  int EACCESS = 13;
    public const  int EFAULT = 14;
    public const  int EINVAL = 22;
    public const  int EAGAIN = 35;
    public const  int EINPROGRESS = 36;
    public const  int EPROTONOSUPPORT = 43;
    public const  int ENOTSUP = 45;
    public const  int EADDRINUSE = 48;
    public const  int EADDRNOTAVAIL = 49;
    public const  int ENETDOWN = 50;
    public const  int ENOBUFS = 55;
    public const  int EISCONN = 56;
    public const  int ENOTCONN = 57;
    public const  int ECONNREFUSED = 61;
    public const  int EHOSTUNREACH = 65;
    
    private const  int ZMQ_HAUSNUMERO = 156384712;

    public const  int EFSM = ZMQ_HAUSNUMERO + 51;
    public const  int ENOCOMPATPROTO = ZMQ_HAUSNUMERO + 52;
    public const  int ETERM = ZMQ_HAUSNUMERO + 53;
    public const  int EMTHREAD = ZMQ_HAUSNUMERO + 54;

    public const  int EIOEXC = ZMQ_HAUSNUMERO + 105;
    public const  int ESOCKET = ZMQ_HAUSNUMERO + 106;
    public const  int EMFILE = ZMQ_HAUSNUMERO + 107;
        
    public static int errno {
        get
        {
            return s_errno.Value;
        }
        set
        {
            s_errno.Value = value;
        }
    }
    
    //public static Throwable exc () {
    //    return exc.get();
    //}
    
    //public static void exc (java.io.IOException e) {
    //    if (e is SocketException) {
    //        errno.set(ESOCKET);
    //    } else if (e is ClosedChannelException) {
    //        errno.set(ENOTCONN);
    //    } else {
    //        errno.set(EIOEXC);
    //    }
    //    exc.set(e);
    //}
    
    public static bool IsError (int code) {
        switch(code) {
        case EINTR:
            return false;
        default:
            return errno == code;
        }
        
    }

    public static void clear () {
        errno=0;
    }
    

}
