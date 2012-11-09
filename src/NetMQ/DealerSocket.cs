using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class DealerSocket : DuplexSocket<DealerSocket>
    {
        public DealerSocket(SocketBase socketHandle) : base(socketHandle)
        {
        }

        protected override DealerSocket This
        {
            get { return this; }
        }
    }
}
