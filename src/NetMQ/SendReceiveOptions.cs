using System;

namespace NetMQ
{
    /// <summary>
    /// This flags enum-type provides a way to specify basic Receive behaviour.
    /// It may be None, or have the DontWait bit (indicating to wait for a message),
    /// or the SendMore bit, set.
    /// </summary>
    [Flags]
    [Obsolete("New Receive/Send API doesn't use SendReceiveOptions")]
    public enum SendReceiveOptions
    {
        /// <summary>
        /// Both bits cleared (neither DontWait nor SendMore are set)
        /// </summary>
        None = 0,

        /// <summary>
        /// Set this flag to specify NOT to block waiting for a message to arrive.
        /// </summary>
        DontWait = 1,

        /// <summary>
        /// Set this (the SendMore bit) to signal more messages beyond the current one, for a given unit of communication.
        /// </summary>
        SendMore = 2,

        // Deprecated aliases
        [Obsolete("Use DontWait instead")]
        NoBlock = DontWait
    }
}