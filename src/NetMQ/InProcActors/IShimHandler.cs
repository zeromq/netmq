using System;
using NetMQ.Sockets;

namespace NetMQ.InProcActors
{
    /// <summary>
    /// Simple interface that all shims should implement. 
    /// T is the initial state that the <c>Actor</c> will provide
    /// </summary>
    [Obsolete("Use non generic NetMQActor and IShimHandler")]
    public interface IShimHandler<T>
    {
        void Initialise(T state);

        void RunPipeline(PairSocket shim);
    }
}
