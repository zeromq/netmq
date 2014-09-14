using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.Actors
{
    /// <summary>
    /// Shim represents one end of the in process pipe, where the Shim expects
    /// to be supplied with a <c>IShimHandlerOfT that it would use for running the pipe
    /// protocol with the original Actor PairSocket at the other end of the pipe 
    /// </summary>
    public class Shim<T>
    {
        public Shim(IShimHandler<T> shimHandler, PairSocket pipe)
        {
            this.Handler = shimHandler;
            this.Pipe = pipe;
        }

        public IShimHandler<T> Handler { get; private set; }
        public PairSocket Pipe { get; private set; }

    }
}
