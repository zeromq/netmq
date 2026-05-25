using System;

namespace NetMQ.Tests
{
    public sealed class CleanupAfterFixture : IDisposable
    {
        public void Dispose() => NetMQConfig.Cleanup();
    }
}