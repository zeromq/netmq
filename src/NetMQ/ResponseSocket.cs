using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using zmq;

namespace NetMQ
{
    public class ResponseSocket : DuplexSocket<ResponseSocket>
	{
		public ResponseSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}



        protected override ResponseSocket This
        {
            get { return this; }
        }
    }
}
