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
            // Dot3.5 has a bug that -1 is not blocking the select call, therefore we are using the maximum int value
            if (microSeconds == -1)
                microSeconds = int.MaxValue;

            Socket.Select(checkRead, checkWrite, checkError, microSeconds);
        }
    }
}
