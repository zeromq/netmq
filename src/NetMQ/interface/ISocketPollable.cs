using System;
using System.Collections.Generic;
using System.Deployment.Internal;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public interface ISocketPollable
    {
		INetMQSocket Socket { get; }        
    }
}
