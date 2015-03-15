#if NET35
using System.Net;

namespace NetMQ
{
    /// <summary>
    /// Placeholder for System.Net.DnsEndPoint, introduced in .NET 4.0.
    /// This code is enabled for .NET 3.5 builds only, allowing compilation.
    /// </summary>
    internal class DnsEndPoint : EndPoint
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
}
#endif
