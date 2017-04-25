using System;

namespace NetMQ
{
    public interface INetMQPoller : IDisposable
    {
        void Run();
        void RunAsync();
        void Stop();
        void StopAsync();

        bool IsRunning { get; }

        void Add(ISocketPollable socket);
    }
}
