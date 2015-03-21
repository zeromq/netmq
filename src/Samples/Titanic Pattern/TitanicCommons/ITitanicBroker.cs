using System;
using MajordomoProtocol;

namespace TitanicCommons
{
    public interface ITitanicBroker : IDisposable
    {
        event EventHandler<LogInfoEventArgs> LogInfoReady;

        void Run ();

    }
}
