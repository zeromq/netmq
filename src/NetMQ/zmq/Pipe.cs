/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;

//import org.slf4j.Logger;
//import org.slf4j.LoggerFactory;

//  Note that pipe can be stored in three different arrays.
//  The array of inbound pipes (1), the array of outbound pipes (2) and
//  the generic array of pipes to deallocate (3).
namespace NetMQ.zmq
{
	public class Pipe : ZObject {

		//private static Logger LOG = LoggerFactory.getLogger(Pipe.class);
    
		public interface IPipeEvents {

			void ReadActivated(Pipe pipe);
			void WriteActivated(Pipe pipe);
			void Hiccuped(Pipe pipe);
			void Terminated(Pipe pipe);
        

		}
		//  Underlying pipes for both directions.
		private YPipe<Msg> m_inpipe;
		private YPipe<Msg> m_outpipe;

		//  Can the pipe be read from / written to?
		private bool m_inActive;
		private bool m_outActive;

		//  High watermark for the outbound pipe.
		private readonly int m_hwm;

		//  Low watermark for the inbound pipe.
		private readonly int m_lwm;

		//  Number of messages read and written so far.
		private long m_msgsRead;
		private long m_msgsWritten;

		//  Last received peer's msgs_read. The actual number in the peer
		//  can be higher at the moment.
		private long m_peersMsgsRead;

		//  The pipe object on the other side of the pipepair.
		private Pipe m_peer;

		//  Sink to send events to.
		private IPipeEvents m_sink;
    
		//  State of the pipe endpoint. Active is common state before any
		//  termination begins. Delimited means that delimiter was read from
		//  pipe before term command was received. Pending means that term
		//  command was already received from the peer but there are still
		//  pending messages to read. Terminating means that all pending
		//  messages were already read and all we are waiting for is ack from
		//  the peer. Terminated means that 'terminate' was explicitly called
		//  by the user. Double_terminated means that user called 'terminate'
		//  and then we've got term command from the peer as well.
		enum State {
			Active,
			Delimited,
			Pending,
			Terminating,
			Terminated,
			DoubleTerminated
		} ;
		private State m_state;

		//  If true, we receive all the pending inbound messages before
		//  terminating. If false, we terminate immediately when the peer
		//  asks us to.
		private bool m_delay;

		//  Identity of the writer. Used uniquely by the reader side.

		private readonly ZObject m_parent;
    
		//  Constructor is private. Pipe can only be created using
		//  pipepair function.
		private Pipe (ZObject parent, YPipe<Msg> inpipe, YPipe<Msg> outpipe,
		              int inhwm, int outhwm, bool delay) : base(parent)
		{
			m_parent = parent;
			m_inpipe = inpipe;
			m_outpipe = outpipe;
			m_inActive = true;
			m_outActive = true;
			m_hwm = outhwm;
			m_lwm = ComputeLwm (inhwm);
			m_msgsRead = 0;
			m_msgsWritten = 0;
			m_peersMsgsRead = 0;
			m_peer = null ;
			m_sink = null ;
			m_state = State.Active;
			m_delay = delay;					
		}
	
		//  Create a pipepair for bi-directional transfer of messages.
		//  First HWM is for messages passed from first pipe to the second pipe.
		//  Second HWM is for messages passed from second pipe to the first pipe.
		//  Delay specifies how the pipe behaves when the peer terminates. If true
		//  pipe receives all the pending messages before terminating, otherwise it
		//  terminates straight away.
		public static void Pipepair(ZObject[] parents, Pipe[] pipes, int[] hwms,
		                            bool[] delays) {
		
			//   Creates two pipe objects. These objects are connected by two ypipes,
			//   each to pass messages in one direction.
	            
			YPipe<Msg> upipe1 = new YPipe<Msg>(Config.MessagePipeGranularity, "upipe1");
			YPipe<Msg> upipe2 = new YPipe<Msg>(Config.MessagePipeGranularity, "upipe2");
	            
			pipes [0] = new Pipe(parents [0], upipe1, upipe2,
			                      hwms [1], hwms [0], delays [0]);
			pipes [1] = new Pipe(parents [1], upipe2, upipe1,
			                      hwms [0], hwms [1], delays [1]);
	            
			pipes [0].SetPeer (pipes [1]);
			pipes [1].SetPeer (pipes [0]);

		                            }
	
		//  Pipepair uses this function to let us know about
		//  the peer pipe object.
		private void SetPeer (Pipe peer)
		{
			//  Peer can be set once only.
			Debug.Assert(peer != null);
			m_peer = peer;
		}
    
		//  Specifies the object to send events to.
		public void SetEventSink(IPipeEvents sink) {
			Debug.Assert(m_sink == null);
			m_sink = sink;
		}

		public Blob Identity { get; set; }

		//  Returns true if there is at least one message to read in the pipe.
		public bool CheckRead() {
			if (!m_inActive || (m_state != State.Active && m_state != State.Pending))
				return false;

			//  Check if there's an item in the pipe.
			if (!m_inpipe.CheckRead ()) {
				m_inActive = false;
				return false;
			}

			//  If the next item in the pipe is message delimiter,
			//  initiate termination process.
			if (IsDelimiter(m_inpipe.Probe ())) {
				Msg msg = m_inpipe.Read ();
				Debug.Assert(msg != null);
				Delimit ();
				return false;
			}

			return true;
		}
    

		//  Reads a message to the underlying pipe.
		public Msg Read()
		{
			if (!m_inActive || (m_state != State.Active && m_state != State.Pending))
				return null;

			Msg msg = m_inpipe.Read ();

			if (msg == null) {
				m_inActive = false;
				return null;
			}

			//  If delimiter was read, start termination process of the pipe.
			if (msg.IsDelimiter ) {
				Delimit ();
				return null;
			}

			if (!msg.HasMore)
				m_msgsRead++;

			if (m_lwm > 0 && m_msgsRead % m_lwm == 0)
				SendActivateWrite (m_peer, m_msgsRead);

			return msg;
		}
    
		//  Checks whether messages can be written to the pipe. If writing
		//  the message would cause high watermark the function returns false.
		public bool CheckWrite ()
		{
			if (!m_outActive || m_state != State.Active)
				return false;

			bool full = m_hwm > 0 && m_msgsWritten - m_peersMsgsRead == (long) (m_hwm);

			if (full) {
				m_outActive = false;
				return false;
			}

			return true;
		}

		//  Writes a message to the underlying pipe. Returns false if the
		//  message cannot be written because high watermark was reached.
		public bool Write (Msg msg)
		{
			if (!CheckWrite ())
				return false;

			bool more = msg.HasMore;
			m_outpipe.Write (msg, more);
			//if (LOG.isDebugEnabled()) {
			//    LOG.debug(parent.ToString() + " write " + msg_);
			//}

			if (!more)
				m_msgsWritten++;

			return true;
		}


		//  Remove unfinished parts of the outbound message from the pipe.
		public void Rollback ()
		{
			//  Remove incomplete message from the outbound pipe.
			Msg msg;
			if (m_outpipe!= null) {
				while ((msg = m_outpipe.Unwrite ()) != null) {
					Debug.Assert((msg.Flags & MsgFlags.More) != 0);
					//msg.close ();
				}
			}
		}
    
		//  Flush the messages downsteam.
		public void Flush ()
		{
			//  The peer does not exist anymore at this point.
			if (m_state == State.Terminating)
				return;

			if (m_outpipe != null && !m_outpipe.Flush ()) {
				SendActivateRead (m_peer);
			} 
		}


		override
			protected void ProcessActivateRead ()
		{
			if (!m_inActive && (m_state == State.Active || m_state == State.Pending)) {
				m_inActive = true;
				m_sink.ReadActivated (this);
			}
		}

		override
			protected void ProcessActivateWrite (long msgsRead)
		{
			//  Remember the peers's message sequence number.
			m_peersMsgsRead = msgsRead;

			if (!m_outActive && m_state == State.Active) {
				m_outActive = true;
				m_sink.WriteActivated (this);
			}
		}


		protected override void ProcessHiccup(Object pipe)
		{
			//  Destroy old outpipe. Note that the read end of the pipe was already
			//  migrated to this thread.
			Debug.Assert(m_outpipe != null);
			m_outpipe.Flush ();
			while (m_outpipe.Read () !=null) {
			}

			//  Plug in the new outpipe.
			Debug.Assert(pipe != null);
			m_outpipe = (YPipe<Msg>) pipe;
			m_outActive = true;

			//  If appropriate, notify the user about the hiccup.
			if (m_state == State.Active)
				m_sink.Hiccuped (this);
		}
    
		override
			protected void ProcessPipeTerm ()
		{
			//  This is the simple case of peer-induced termination. If there are no
			//  more pending messages to read, or if the pipe was configured to drop
			//  pending messages, we can move directly to the terminating state.
			//  Otherwise we'll hang up in pending state till all the pending messages
			//  are sent.
			if (m_state == State.Active) {
				if (!m_delay) {
					m_state = State.Terminating;
					m_outpipe = null;
					SendPipeTermAck (m_peer);
				}
				else
					m_state = State.Pending;
				return;
			}

			//  Delimiter happened to arrive before the term command. Now we have the
			//  term command as well, so we can move straight to terminating state.
			if (m_state == State.Delimited) {
				m_state = State.Terminating;
				m_outpipe = null;
				SendPipeTermAck (m_peer);
				return;
			}

			//  This is the case where both ends of the pipe are closed in parallel.
			//  We simply reply to the request by ack and continue waiting for our
			//  own ack.
			if (m_state == State.Terminated) {
				m_state = State.DoubleTerminated;
				m_outpipe = null;
				SendPipeTermAck (m_peer);
				return;
			}

			//  pipe_term is invalid in other states.
			Debug.Assert(false);
		}
    
		override
			protected void ProcessPipeTermAck ()
		{
			//  Notify the user that all the references to the pipe should be dropped.
			Debug.Assert(m_sink!=null);
			m_sink.Terminated (this);

			//  In terminating and double_terminated states there's nothing to do.
			//  Simply deallocate the pipe. In terminated state we have to ack the
			//  peer before deallocating this side of the pipe. All the other states
			//  are invalid.
			if (m_state == State.Terminated) {
				m_outpipe = null;
				SendPipeTermAck (m_peer);
			}
			else
				Debug.Assert(m_state == State.Terminating || m_state == State.DoubleTerminated);

			//  We'll deallocate the inbound pipe, the peer will deallocate the outbound
			//  pipe (which is an inbound pipe from its point of view).
			//  First, delete all the unread messages in the pipe. We have to do it by
			//  hand because msg_t doesn't have automatic destructor. Then deallocate
			//  the ypipe itself.
			while (m_inpipe.Read () != null) {
			}
        
			m_inpipe = null;

			//  Deallocate the pipe object
		}

		//  Ask pipe to terminate. The termination will happen asynchronously
		//  and user will be notified about actual deallocation by 'terminated'
		//  event. If delay is true, the pending messages will be processed
		//  before actual shutdown.
		public void Terminate (bool delay)
		{
			//  Overload the value specified at pipe creation.
			m_delay = delay;

			//  If terminate was already called, we can ignore the duplicit invocation.
			if (m_state == State.Terminated || m_state == State.DoubleTerminated)
				return;

				//  If the pipe is in the phase of async termination, it's going to
				//  closed anyway. No need to do anything special here.
			else if (m_state == State.Terminating)
				return;

				//  The simple sync termination case. Ask the peer to terminate and wait
				//  for the ack.
			else if (m_state == State.Active) {
				SendPipeTerm (m_peer);
				m_state = State.Terminated;
			}

				//  There are still pending messages available, but the user calls
				//  'terminate'. We can act as if all the pending messages were read.
			else if (m_state == State.Pending && !m_delay) {
				m_outpipe = null;
				SendPipeTermAck (m_peer);
				m_state = State.Terminating;
			}

				//  If there are pending messages still availabe, do nothing.
			else if (m_state == State.Pending) {
			}

				//  We've already got delimiter, but not term command yet. We can ignore
				//  the delimiter and ack synchronously terminate as if we were in
				//  active state.
			else if (m_state == State.Delimited) {
				SendPipeTerm (m_peer);
				m_state = State.Terminated;
			}

				//  There are no other states.
			else
				Debug.Assert(false);

			//  Stop outbound flow of messages.
			m_outActive = false;

			if (m_outpipe != null) {

				//  Drop any unfinished outbound messages.
				Rollback ();

				//  Write the delimiter into the pipe. Note that watermarks are not
				//  checked; thus the delimiter can be written even when the pipe is full.
            
				Msg msg = new Msg();
				msg.InitDelimiter ();
				m_outpipe.Write (msg, false);
				Flush ();
            
			}
		}
    

		//  Returns true if the message is delimiter; false otherwise.
		private static bool IsDelimiter(Msg msg) {
			return msg.IsDelimiter ;
		}

		//  Computes appropriate low watermark from the given high watermark.
		private static int ComputeLwm (int hwm)
		{
			//  Compute the low water mark. Following point should be taken
			//  into consideration:
			//
			//  1. LWM has to be less than HWM.
			//  2. LWM cannot be set to very low value (such as zero) as after filling
			//     the queue it would start to refill only after all the messages are
			//     read from it and thus unnecessarily hold the progress back.
			//  3. LWM cannot be set to very high value (such as HWM-1) as it would
			//     result in lock-step filling of the queue - if a single message is
			//     read from a full queue, writer thread is resumed to write exactly one
			//     message to the queue and go back to sleep immediately. This would
			//     result in low performance.
			//
			//  Given the 3. it would be good to keep HWM and LWM as far apart as
			//  possible to reduce the thread switching overhead to almost zero,
			//  say HWM-LWM should be max_wm_delta.
			//
			//  That done, we still we have to account for the cases where
			//  HWM < max_wm_delta thus driving LWM to negative numbers.
			//  Let's make LWM 1/2 of HWM in such cases.
			int result = (hwm > Config.MaxWatermarkDelta * 2) ?
			                                                   	hwm - Config.MaxWatermarkDelta : (hwm + 1) / 2;

			return result;
		}
	

		//  Handler for delimiter read from the pipe.
		private void Delimit ()
		{
			if (m_state == State.Active) {
				m_state = State.Delimited;
				return;
			}

			if (m_state == State.Pending) {
				m_outpipe = null;
				SendPipeTermAck (m_peer);
				m_state = State.Terminating;
				return;
			}

			//  Delimiter in any other state is invalid.
			Debug.Assert(false);
		}

 
		//  Temporaraily disconnects the inbound message stream and drops
		//  all the messages on the fly. Causes 'hiccuped' event to be generated
		//  in the peer.
		public void Hiccup() {
			//  If termination is already under way do nothing.
			if (m_state != State.Active)
				return;

			//  We'll drop the pointer to the inpipe. From now on, the peer is
			//  responsible for deallocating it.
			m_inpipe = null;

			//  Create new inpipe.
			m_inpipe = new YPipe<Msg>(Config.MessagePipeGranularity, "inpipe");
			m_inActive = true;

			//  Notify the peer about the hiccup.
			SendHiccup (m_peer, m_inpipe);

		}



    
		public override String ToString() {
			return base.ToString() + "[" + m_parent + "]";
		}


	}
}
