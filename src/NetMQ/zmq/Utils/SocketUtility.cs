using System.Collections;
using System.Net.Sockets;

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
