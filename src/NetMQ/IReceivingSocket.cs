using NetMQ.zmq;

namespace NetMQ
{
    public interface IReceivingSocket
    {
        void Receive(ref Msg msg, SendReceiveOptions options);
    }
}