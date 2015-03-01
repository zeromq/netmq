using System;
using JetBrains.Annotations;
using NetMQ.Sockets;

namespace NetMQ.InProcActors
{
    /// <summary>
    /// Simple interface that all shims should implement. 
    /// T is the initial state that the <c>Actor</c> will provide.
    /// This interface specifies the methods Initialize and RunPipeline.
    /// </summary>
    [Obsolete("Use non generic NetMQActor and IShimHandler")]
    public interface IShimHandler<in T>
    {
        /// <summary>
        /// Initialize the shim to the given state - which is of type T.
        /// </summary>
        /// <param name="state">an object of type T to specify the initial state</param>
        void Initialise(T state);

        void RunPipeline([NotNull] PairSocket shim);
    }
}
