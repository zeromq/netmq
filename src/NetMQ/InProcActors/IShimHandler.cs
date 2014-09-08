using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Sockets;

namespace NetMQ.Actors
{
    /// <summary>
    /// Simple interface that all shims should implement
    /// </summary>
    public interface IShimHandler : IDisposable
    {
        void Run(PairSocket shim, object[] args, CancellationToken token);
    }
}
