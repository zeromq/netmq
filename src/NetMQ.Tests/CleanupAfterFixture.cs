using System;
using JetBrains.Annotations;

namespace NetMQ.Tests
{
    [UsedImplicitly]
    public sealed class CleanupAfterFixture : IDisposable
    {
        public void Dispose() => NetMQConfig.Cleanup();
    }
}