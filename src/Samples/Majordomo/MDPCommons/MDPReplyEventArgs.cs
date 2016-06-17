using NetMQ;
using System;

namespace MDPCommons
{
    public class MDPReplyEventArgs : EventArgs
    {
        public NetMQMessage Reply { get; private set; }

        public Exception Exception { get; private set; }

        public MDPReplyEventArgs(NetMQMessage reply)
        {
            Reply = reply;
        }

        public MDPReplyEventArgs(NetMQMessage reply, Exception exception) 
            : this(reply)
        {
            Exception = exception;
        }

        public bool HasError()
        {
            return (Exception != null ? true : false);
        }
    }
}
