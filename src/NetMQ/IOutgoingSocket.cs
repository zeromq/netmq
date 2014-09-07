using NetMQ.zmq;

namespace NetMQ
{
    public interface IOutgoingSocket
    {        
        void Send(ref Msg msg, SendReceiveOptions options);
    }
}
