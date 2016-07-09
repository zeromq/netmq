namespace NetMQ.Core
{
    /// <summary>
    /// Enumeration of possible <see cref="Command"/> types.
    /// </summary>
    /// <remarks>
    /// The value of <see cref="Command.CommandType"/> denotes what action
    /// the command will perform.
    /// </remarks>
    internal enum CommandType
    {
        None = 0,

        /// <summary>
        /// Sent to I/O thread to let it know that it should
        /// terminate itself.
        /// </summary>
        Stop,

        /// <summary>
        /// Sent to I/O object to make it register with its I/O thread
        /// </summary>
        Plug,

        /// <summary>
        /// Sent to socket to let it know about the newly created object.
        /// </summary>
        Own,

        /// <summary>
        /// Attach the engine to the session. If engine is NULL, it informs
        /// session that the connection has failed.
        /// </summary>
        Attach,

        /// <summary>
        /// Sent from session to socket to establish pipe(s) between them.
        /// Caller must have used inc_seqnum before sending the command.
        /// </summary>
        Bind,

        /// <summary>
        /// Sent by pipe writer to inform dormant pipe reader that there
        /// are messages in the pipe.
        /// </summary>
        ActivateRead,

        /// <summary>
        /// Sent by pipe reader to inform pipe writer how many
        /// messages it has read so far.
        /// </summary>
        ActivateWrite,

        /// <summary>
        /// Sent by pipe reader to writer after creating a new inpipe.
        /// The parameter is actually of type pipe_t::upipe_t, however,
        /// its definition is private so we'll have to do with void*.
        /// </summary>
        Hiccup,

        /// <summary>
        /// Sent by pipe reader to pipe writer to ask it to terminate
        /// its end of the pipe.
        /// </summary>
        PipeTerm,

        /// <summary>
        /// Pipe writer acknowledges pipe_term command.
        /// </summary>
        PipeTermAck,

        /// <summary>
        /// Sent by I/O object to the socket to request the shutdown of
        /// the I/O object.
        /// </summary>
        TermReq,

        /// <summary>
        /// Sent by socket to I/O object to start its shutdown.
        /// </summary>
        Term,

        /// <summary>
        /// Sent by I/O object to the socket to acknowledge it has
        /// shut down.
        /// </summary>
        TermAck,

        /// <summary>
        /// Transfers the ownership of the closed socket
        /// to the reaper thread.
        /// </summary>
        Reap,

        /// <summary>
        /// Closed socket notifies the reaper that it's already deallocated.
        /// </summary>
        Reaped,

        /// <summary>
        /// Sent by reaper thread to the term thread when all the sockets
        /// have successfully been deallocated.
        /// </summary>
        Done,

        /// <summary>
        /// Send to reaper to stop the reaper immediatly
        /// </summary>
        ForceStop
    }
}