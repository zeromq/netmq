namespace NetMQ.zmq
{
	public enum CommandType {
		//  Sent to I/O thread to let it know that it should
		//  terminate itself.
		Stop,
		//  Sent to I/O object to make it register with its I/O thread
		Plug,
		//  Sent to socket to let it know about the newly created object.
		Own,
		//  Attach the engine to the session. If engine is NULL, it informs
		//  session that the connection have failed.
		Attach,
		//  Sent from session to socket to establish pipe(s) between them.
		//  Caller have used inc_seqnum beforehand sending the command.
		Bind,
		//  Sent by pipe writer to inform dormant pipe reader that there
		//  are messages in the pipe.
		ActivateRead,
		//  Sent by pipe reader to inform pipe writer about how many
		//  messages it has read so far.
		ActivateWrite,
		//  Sent by pipe reader to writer after creating a new inpipe.
		//  The parameter is actually of type pipe_t::upipe_t, however,
		//  its definition is private so we'll have to do with void*.
		Hiccup,
		//  Sent by pipe reader to pipe writer to ask it to terminate
		//  its end of the pipe.
		PipeTerm,
		//  Pipe writer acknowledges pipe_term command.
		PipeTermAck,
		//  Sent by I/O object ot the socket to request the shutdown of
		//  the I/O object.
		TermReq,
		//  Sent by socket to I/O object to start its shutdown.
		Term,
		//  Sent by I/O object to the socket to acknowledge it has
		//  shut down.
		TermAck,
		//  Transfers the ownership of the closed socket
		//  to the reaper thread.
		Reap,
		//  Closed socket notifies the reaper that it's already deallocated.
		Reaped,
		//  Sent by reaper thread to the term thread when all the sockets
		//  are successfully deallocated.
		Done        
	}
}