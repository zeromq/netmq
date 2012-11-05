namespace ZeroMQ
{
    /// <summary>
    /// Specifies possible results for socket receive operations.
    /// </summary>
    public enum ReceiveStatus
    {
        /// <summary>
        /// No receive operation has been performed.
        /// </summary>
        None,

        /// <summary>
        /// The receive operation returned a message that contains data.
        /// </summary>
        Received,

        /// <summary>
        /// Non-blocking mode was requested and no messages are available at the moment.
        /// </summary>
        TryAgain,

        /// <summary>
        /// The receive operation was interrupted, likely by terminating the containing context.
        /// </summary>
        Interrupted
    }
}
