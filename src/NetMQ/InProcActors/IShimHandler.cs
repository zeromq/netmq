using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Sockets;

namespace NetMQ.Actors
{
    /// <summary>
    /// Simple interface that all shims should implement. 
    /// T is the initial state that the <c>Actor</c> will provide
    /// </summary>
    public interface IShimHandler<T>
    {
        void Initialise(T state);

        void RunPipeline(PairSocket shim);
    }
}
