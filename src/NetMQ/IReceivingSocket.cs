using NetMQ.Core;


namespace NetMQ
{
    public interface IReceivingSocket
    {        
        void Receive(ref Msg msg, SendReceiveOptions options);
    }
}