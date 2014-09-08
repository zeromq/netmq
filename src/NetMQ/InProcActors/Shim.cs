using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.Actors
{
    /// <summary>
    /// Shim represents one end of the in process pipe, where the Shim expects
    /// to be supplied with a <c>IShimHandler</c> that it would use for running the pipe
    /// protocol with the original Actor PairSocket the other end of the pipe 
    /// </summary>
    public class Shim : IDisposable
    {
        public Shim(IShimHandler shimHandler, PairSocket pipe)
        {
            this.Handler = shimHandler;
            this.Pipe = pipe;
        }

        public IShimHandler Handler { get; private set; }
        public PairSocket Pipe { get; private set; }

        ~Shim()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // release disposable objects
                if (Pipe != null) Pipe.Dispose();
                if (Handler != null) Handler.Dispose();
            }
        }
    }
}
