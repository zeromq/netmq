using System;
using NetMQ.Sockets;

namespace NetMQ.InProcActors
{
    /// <summary>
    /// Shim represents one end of the in-process pipe, where the Shim expects
    /// to be supplied with a IShimHandlerOfT that it would use for running the pipe
    /// protocol with the original Actor PairSocket at the other end of the pipe 
    /// </summary>
    [Obsolete("Use non generic NetMQActor and IShimHandler")]
    public class Shim<T>
    {
        /// <summary>
        /// Create a new Shim-object to encapsulate the given pipe
        /// and to use the given IShimHandler to service that pipe.
        /// </summary>
        /// <param name="shimHandler">the IShimHandler to initialize and run the pipe</param>
        /// <param name="pipe">the PairSocket that will be our pipe</param>
        public Shim(IShimHandler<T> shimHandler, PairSocket pipe)
        {
            this.Handler = shimHandler;
            this.Pipe = pipe;
        }

        /// <summary>
        /// Get the object that implements IShimHandlerOfT,
        /// which provides the methods to initialize and run the pipeline.
        /// </summary>
        public IShimHandler<T> Handler { get; private set; }

        /// <summary>
        /// Get the Pipe (a PairSocket) that this Shim serves.
        /// </summary>
        public PairSocket Pipe { get; private set; }
    }
}
