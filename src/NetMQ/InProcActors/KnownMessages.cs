using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.InProcActors
{
    public class ActorKnownMessages
    {
        [Obsolete("Use NetMQActor.EndShimMessage")]
        public const string END_PIPE = "endPipe";
    }
}
