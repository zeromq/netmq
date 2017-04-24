using System;
using JetBrains.Annotations;

namespace NetMQ.Tests
{
    [UsedImplicitly]
    public sealed class NetMQConfigFixture : IDisposable
    {
        public NetMQConfigFixture()
        {
            // Doing cleanup once so tests will not be affected by already ran tests
            NetMQConfig.Cleanup(false);
        }

        public void Dispose()
        {
            // In CI it takes time for the background threads to complete the socket async dispose
            // cleanup the library to avoid the issue
            NetMQConfig.Cleanup();
        }
    }
}