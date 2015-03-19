namespace NetMQ.Core
{
    /// <summary>
    /// Interface IMsgSink mandates a PushMsg( Msg ) method.
    /// </summary>
    internal interface IMsgSink
    {
        /// <summary>
        /// Deliver a message. Return true if successful; false otherwise.
        /// This function takes ownership of the passed message.
        /// </summary>
        /// <param name="msg">the message (of type Msg) to deliver</param>
        bool PushMsg(ref Msg msg);
    }
}
