using JetBrains.Annotations;

namespace NetMQ.Core
{
    internal interface IMailbox
    {
        void Send([NotNull] Command command);

        bool TryRecv(int timeout, out Command command);

        void Close();
    }
}