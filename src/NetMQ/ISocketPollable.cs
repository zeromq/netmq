using System;
using System.Collections.Generic;
using System.Deployment.Internal;
using System.Linq;
using System.Text;

namespace NetMQ
{
    public interface ISocketPollable
    {
        NetMQSocket Socket { get; }        
    }
}
