using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace NetMQ.zmq.Utils
{
    static class SocketUtility
    {
        public static void Select(IList checkRead, IList checkWrite, IList checkError, int microSeconds)
        {            
            Socket.Select(checkRead, checkWrite, checkError, microSeconds);
        }
    }
}
