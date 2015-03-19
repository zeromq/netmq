using System;

namespace NetMQ.InProcActors
{
    /// <summary>
    /// This was a class intended to hold constant strings that represented fixed, standard Actor messages.
    /// </summary>
    [Obsolete("Use NetMQActor.EndShimMessage")]
    public class ActorKnownMessages
    {
        /// <summary>
        /// This known-actor message was a signal to terminate the pipe.
        /// </summary>
        [Obsolete("Use NetMQActor.EndShimMessage")]
        public const string END_PIPE = "endPipe";
    }
}
