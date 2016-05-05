using System;

namespace NetMQ
{
    /// <summary>
    /// </summary>
    public interface INetMQPoller: IDisposable
    {
        /// <summary>
        /// </summary>
        void Run();
        /// <summary>
        /// </summary>
        void RunAsync();
        /// <summary>
        /// </summary>
        void Stop();
        /// <summary>
        /// </summary>
        void StopAsync();

        /// <summary>
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// </summary>
        void Add(ISocketPollable socket);
    }
}