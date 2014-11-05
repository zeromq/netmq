namespace NetMQ.Core
{
    interface IMailbox
    {
        void Send(Command command);
        void Close();
    }
}