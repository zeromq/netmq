namespace NetMQ.Core
{
    internal interface IMailbox
    {
        void Send(Command command);

        bool TryRecv(int timeout, out Command command);

        void Close();
    }
}