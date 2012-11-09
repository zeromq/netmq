using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public class RequestSocket : DuplexSocket<RequestSocket>
	{
		public RequestSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}


        protected override RequestSocket This
        {
            get { return this; }
        }
    }
}
