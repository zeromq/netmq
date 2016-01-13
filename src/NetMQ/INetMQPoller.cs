namespace NetMQ
{
    public interface INetMQPoller
    {
        void Run();
        void RunAsync();
        void Stop();
        void StopAsync();

        bool IsRunning { get; }

        void Add(ISocketPollable socket);
    }
}