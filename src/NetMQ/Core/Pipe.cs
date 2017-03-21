/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

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

using System.Diagnostics;
using JetBrains.Annotations;

namespace NetMQ.Core
{
    /// <summary>
    /// A pipe is a ZObject with ..
    /// </summary>
    /// <remarks>
    /// Note that pipe can be stored in three different arrays.
    /// The array of inbound pipes (1), the array of outbound pipes (2) and
    /// the generic array of pipes to deallocate (3).
    /// </remarks>
    internal class Pipe : ZObject
    {
        public interface IPipeEvents
        {
            /// <summary>
            /// Indicate that the given pipe is now ready for reading.
            /// Pipe calls this on it's sink in response to ProcessActivateRead.
            /// When called upon an instance of SocketBase, this simply calls XReadActivated.
            /// </summary>
            /// <param name="pipe">the pipe to indicate is ready for reading</param>
            void ReadActivated([NotNull] Pipe pipe);

            void WriteActivated([NotNull] Pipe pipe);
            void Hiccuped([NotNull] Pipe pipe);

            /// <summary>
            /// This gets called by ProcessPipeTermAck or XTerminated to respond to the termination of the given pipe.
            /// </summary>
            /// <param name="pipe">the pipe that was terminated</param>
            void Terminated([NotNull] Pipe pipe);
        }

        /// <summary>
        /// The underlying pipe for reading from.
        /// </summary>
        private YPipe<Msg> m_inboundPipe;

        /// <summary>
        /// The underlying pipe for writing to.
        /// </summary>
        private YPipe<Msg> m_outboundPipe;

        /// <summary>
        /// This indicates whether this pipe can be read from.
        /// </summary>
        private bool m_inActive;

        /// <summary>
        /// This indicates whether this pipe be written to.
        /// </summary>
        private bool m_outActive;

        /// <summary>
        /// High watermark for the outbound pipe.
        /// </summary>
        private readonly int m_highWatermark;

        /// <summary>
        /// Low watermark for the inbound pipe.
        /// </summary>
        private readonly int m_lowWatermark;

        /// <summary>
        /// Number of messages read so far.
        /// </summary>
        private long m_numberOfMessagesRead;

        /// <summary>
        /// Number of messages written so far.
        /// </summary>
        private long m_numberOfMessagesWritten;

        /// <summary>
        /// Last received peer's msgs_read. The actual number in the peer
        /// can be higher at the moment.
        /// </summary>
        private long m_peersMsgsRead;

        /// <summary>
        /// The pipe object on the other side of the pipe-pair.
        /// </summary>
        private Pipe m_peer;

        /// <summary>
        /// Sink to send events to.
        /// </summary>
        private IPipeEvents m_sink;

        /// <summary>
        /// Specifies the state of the pipe endpoint.
        /// </summary>
        private enum State
        {
            /// <summary> Active is common state before any termination begins. </summary>
            Active,
            /// <summary> Delimited means that delimiter was read from pipe before term command was received. </summary>
            Delimited,
            /// <summary> Pending means that term command was already received from the peer but there are still pending messages to read. </summary>
            Pending,
            /// <summary> Terminating means that all pending messages were already read and all we are waiting for is ack from the peer. </summary>
            Terminating,
            /// <summary> Terminated means that 'terminate' was explicitly called by the user. </summary>
            Terminated,
            /// <summary> Double_terminated means that user called 'terminate' and then we've got term command from the peer as well. </summary>
            DoubleTerminated
        }

        private State m_state;

        /// <summary>
        /// If <c>true</c>, we receive all the pending inbound messages before terminating.
        /// If <c>false</c>, we terminate immediately when the peer asks us to.
        /// </summary>
        private bool m_delay;

        /// <summary>
        /// Identity of the writer. Used uniquely by the reader side.
        /// </summary>
        private readonly ZObject m_parent;

        /// <summary>
        /// Create a new Pipe object with the given parent, and inbound and outbound YPipes.
        /// </summary>
        /// <remarks>
        /// Constructor is private as pipe can only be created using <see cref="PipePair"/> method.
        /// </remarks>
        private Pipe(
            [NotNull] ZObject parent, [NotNull] YPipe<Msg> inboundPipe, [NotNull] YPipe<Msg> outboundPipe,
            int inHighWatermark, int outHighWatermark, int predefinedLowWatermark, bool delay)
            : base(parent)
        {
            m_parent = parent;
            m_inboundPipe = inboundPipe;
            m_outboundPipe = outboundPipe;
            m_inActive = true;
            m_outActive = true;
            m_highWatermark = outHighWatermark;
            m_lowWatermark = ComputeLowWatermark(inHighWatermark, predefinedLowWatermark);
            m_numberOfMessagesRead = 0;
            m_numberOfMessagesWritten = 0;
            m_peersMsgsRead = 0;
            m_peer = null;
            m_sink = null;
            m_state = State.Active;
            m_delay = delay;
        }

        /// <summary>
        /// Create a pipe pair for bi-directional transfer of messages.
        /// </summary>
        /// <param name="parents">The parents.</param>
        /// <param name="highWaterMarks">First HWM is for messages passed from first pipe to the second pipe.
        /// Second HWM is for messages passed from second pipe to the first pipe.</param>
        /// <param name="lowWaterMarks">First LWM is for messages passed from first pipe to the second pipe.
        /// Second LWM is for messages passed from second pipe to the first pipe.</param>
        /// <param name="delays">Delay specifies how the pipe behaves when the peer terminates. If true
        /// pipe receives all the pending messages before terminating, otherwise it
        /// terminates straight away.</param>
        /// <returns>A pipe pair for bi-directional transfer of messages. </returns>
        [NotNull]
        public static Pipe[] PipePair([NotNull] ZObject[] parents, [NotNull] int[] highWaterMarks, [NotNull] int[] lowWaterMarks, [NotNull] bool[] delays)
        {
            // Creates two pipe objects. These objects are connected by two ypipes,
            // each to pass messages in one direction.

            var upipe1 = new YPipe<Msg>(Config.MessagePipeGranularity, "upipe1");
            var upipe2 = new YPipe<Msg>(Config.MessagePipeGranularity, "upipe2");

            var pipes = new[]
            {
                new Pipe(parents[0], upipe1, upipe2, highWaterMarks[1], highWaterMarks[0], lowWaterMarks[1], delays[0]),
                new Pipe(parents[1], upipe2, upipe1, highWaterMarks[0], highWaterMarks[1], lowWaterMarks[0], delays[1])
            };

            pipes[0].SetPeer(pipes[1]);
            pipes[1].SetPeer(pipes[0]);

            return pipes;
        }

        public bool Active => m_state == State.Active;

        /// <summary>
        /// <see cref="PipePair"/> uses this function to let us know about the peer pipe object.
        /// </summary>
        /// <param name="peer">The peer to be assigned.</param>
        private void SetPeer([NotNull] Pipe peer)
        {
            // Peer can be set once only.
            Debug.Assert(peer != null);
            m_peer = peer;
        }

        /// <summary>
        /// Specifies the object to send events to.
        /// </summary>
        /// <param name="sink"> The receiver of the events. </param>
        public void SetEventSink([NotNull] IPipeEvents sink)
        {
            Debug.Assert(m_sink == null);
            m_sink = sink;
        }

        /// <summary>
        /// Get or set the byte-array that comprises the identity of this Pipe.
        /// </summary>
        [CanBeNull]
        public byte[] Identity { get; set; }

        /// <summary>
        /// Checks if there is at least one message to read in the pipe.
        /// </summary>
        /// <returns> Returns <c>true</c> if there is at least one message to read in the pipe; <c>false</c> otherwise. </returns>
        public bool CheckRead()
        {
            if (!m_inActive || (m_state != State.Active && m_state != State.Pending))
                return false;

            // Check if there's an item in the pipe.
            if (!m_inboundPipe.CheckRead())
            {
                m_inActive = false;
                return false;
            }

            // If the next item in the pipe is message delimiter,
            // initiate termination process.
            if (m_inboundPipe.Probe().IsDelimiter)
            {
                var msg = new Msg();
                bool ok = m_inboundPipe.TryRead(out msg);
                Debug.Assert(ok);
                Delimit();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read a message from the underlying inbound pipe, and write it to the provided Msg argument.
        /// </summary>
        /// <returns>true if a message is read from the pipe, false if pipe is terminated or no messages are available</returns>
        public bool Read(ref Msg msg)
        {
            if (!m_inActive || (m_state != State.Active && m_state != State.Pending))
                return false;

            if (!m_inboundPipe.TryRead(out msg))
            {
                m_inActive = false;
                return false;
            }

            // If delimiter was read, start termination process of the pipe.
            if (msg.IsDelimiter)
            {
                Delimit();
                return false;
            }

            if (!msg.HasMore)
                m_numberOfMessagesRead++;

            if (m_lowWatermark > 0 && m_numberOfMessagesRead%m_lowWatermark == 0)
                SendActivateWrite(m_peer, m_numberOfMessagesRead);

            return true;
        }

        /// <summary>
        /// Check whether messages can be written to the pipe. If writing
        /// the message would cause high watermark the function returns false.
        /// </summary>
        /// <returns><c>true</c> if message can be written to the pipe; <c>false</c> otherwise. </returns>
        public bool CheckWrite()
        {
            if (!m_outActive || m_state != State.Active)
                return false;

            bool full = m_highWatermark > 0 && m_numberOfMessagesWritten - m_peersMsgsRead == m_highWatermark;

            if (full)
            {
                m_outActive = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Write a message to the underlying pipe.
        /// </summary>
        /// <param name="msg">The message to write.</param>
        /// <returns>false if the message cannot be written because high watermark was reached.</returns>
        public bool Write(ref Msg msg)
        {
            if (!CheckWrite())
                return false;

            bool more = msg.HasMore;
            m_outboundPipe.Write(ref msg, more);

            if (!more)
                m_numberOfMessagesWritten++;

            return true;
        }

        /// <summary>
        /// Remove unfinished parts of the outbound message from the pipe.
        /// </summary>
        public void Rollback()
        {
            // Remove incomplete message from the outbound pipe.
            if (m_outboundPipe != null)
            {
                var msg = new Msg();
                while (m_outboundPipe.Unwrite(ref msg))
                {
                    Debug.Assert(msg.HasMore);
                    msg.Close();
                }
            }
        }

        /// <summary>
        /// Flush the messages downstream.
        /// </summary>
        public void Flush()
        {
            // The peer does not exist anymore at this point.
            if (m_state == State.Terminating)
                return;

            if (m_outboundPipe != null && !m_outboundPipe.Flush())
                SendActivateRead(m_peer);
        }

        protected override void ProcessActivateRead()
        {
            if (m_inActive || (m_state != State.Active && m_state != State.Pending))
                return;
            m_inActive = true;
            m_sink.ReadActivated(this);
        }

        protected override void ProcessActivateWrite(long msgsRead)
        {
            // Remember the peer's message sequence number.
            m_peersMsgsRead = msgsRead;

            if (m_outActive || m_state != State.Active)
                return;
            m_outActive = true;
            m_sink.WriteActivated(this);
        }

        /// <summary>
        /// This method is called to assign the specified pipe as a replacement for the outbound pipe that was being used.
        /// </summary>
        /// <param name="pipe">the pipe to use for writing</param>
        /// <remarks>
        /// A "Hiccup" occurs when an outbound pipe experiences something like a transient disconnect or for whatever other reason
        /// is no longer available for writing to.
        /// </remarks>
        protected override void ProcessHiccup(object pipe)
        {
            // Destroy old out-pipe. Note that the read end of the pipe was already
            // migrated to this thread.
            Debug.Assert(m_outboundPipe != null);
            m_outboundPipe.Flush();
            var msg = new Msg();
            while (m_outboundPipe.TryRead(out msg))
            {
                msg.Close();
            }

            // Plug in the new out-pipe.
            Debug.Assert(pipe != null);
            m_outboundPipe = (YPipe<Msg>)pipe;
            m_outActive = true;

            // If appropriate, notify the user about the hiccup.
            if (m_state == State.Active)
                m_sink.Hiccuped(this);
        }

        protected override void ProcessPipeTerm()
        {
            // This is the simple case of peer-induced termination. If there are no
            // more pending messages to read, or if the pipe was configured to drop
            // pending messages, we can move directly to the terminating state.
            // Otherwise we'll hang up in pending state till all the pending messages
            // are sent.
            if (m_state == State.Active)
            {
                if (!m_delay)
                {
                    m_state = State.Terminating;
                    m_outboundPipe = null;
                    SendPipeTermAck(m_peer);
                }
                else
                    m_state = State.Pending;
                return;
            }

            // Delimiter happened to arrive before the term command. Now we have the
            // term command as well, so we can move straight to terminating state.
            if (m_state == State.Delimited)
            {
                m_state = State.Terminating;
                m_outboundPipe = null;
                SendPipeTermAck(m_peer);
                return;
            }

            // This is the case where both ends of the pipe are closed in parallel.
            // We simply reply to the request by ack and continue waiting for our
            // own ack.
            if (m_state == State.Terminated)
            {
                m_state = State.DoubleTerminated;
                m_outboundPipe = null;
                SendPipeTermAck(m_peer);
                return;
            }

            // pipe_term is invalid in other states.
            Debug.Assert(false);
        }

        /// <summary>
        /// Process the pipe-termination ack.
        /// </summary>
        protected override void ProcessPipeTermAck()
        {
            // Notify the user that all the references to the pipe should be dropped.
            Debug.Assert(m_sink != null);
            m_sink.Terminated(this);

            // In terminating and double_terminated states there's nothing to do.
            // Simply deallocate the pipe. In terminated state we have to ack the
            // peer before deallocating this side of the pipe. All the other states
            // are invalid.
            if (m_state == State.Terminated)
            {
                m_outboundPipe = null;
                SendPipeTermAck(m_peer);
            }
            else
                Debug.Assert(m_state == State.Terminating || m_state == State.DoubleTerminated);

            // We'll deallocate the inbound pipe, the peer will deallocate the outbound
            // pipe (which is an inbound pipe from its point of view).
            // First, delete all the unread messages in the pipe. We have to do it by
            // hand because msg_t doesn't have automatic destructor. Then deallocate
            // the ypipe itself.
            var msg = new Msg();
            while (m_inboundPipe.TryRead(out msg))
            {
                msg.Close();
            }

            m_inboundPipe = null;
        }

        /// <summary>
        /// Ask pipe to terminate. The termination will happen asynchronously
        /// and user will be notified about actual deallocation by 'terminated'
        /// event.
        /// </summary>
        /// <param name="delay">if set to <c>true</c>, the pending messages will be processed
        /// before actual shutdown. </param>
        public void Terminate(bool delay)
        {
            // Overload the value specified at pipe creation.
            m_delay = delay;

            // If terminate was already called, we can ignore the duplicate invocation.
            if (m_state == State.Terminated || m_state == State.DoubleTerminated)
                return;

            // If the pipe is in the phase of async termination, it's going to
            // closed anyway. No need to do anything special here.
            if (m_state == State.Terminating)
                return;

            if (m_state == State.Active)
            {
                // The simple sync termination case. Ask the peer to terminate and wait
                // for the ack.
                SendPipeTerm(m_peer);
                m_state = State.Terminated;
            }
            else if (m_state == State.Pending && !m_delay)
            {
                // There are still pending messages available, but the user calls
                // 'terminate'. We can act as if all the pending messages were read.
                m_outboundPipe = null;
                SendPipeTermAck(m_peer);
                m_state = State.Terminating;
            }
            else if (m_state == State.Pending)
            {
                // If there are pending messages still available, do nothing.
            }
            else if (m_state == State.Delimited)
            {
                // We've already got delimiter, but not term command yet. We can ignore
                // the delimiter and ack synchronously terminate as if we were in
                // active state.
                SendPipeTerm(m_peer);
                m_state = State.Terminated;
            }
            else
            {
                // There are no other states.
                Debug.Assert(false);
            }

            // Stop outbound flow of messages.
            m_outActive = false;

            if (m_outboundPipe != null)
            {
                // Drop any unfinished outbound messages.
                Rollback();

                // Write the delimiter into the pipe. Note that watermarks are not
                // checked; thus the delimiter can be written even when the pipe is full.

                var msg = new Msg();
                msg.InitDelimiter();
                m_outboundPipe.Write(ref msg, false);
                Flush();
            }
        }

        /// <summary>
        /// Compute an appropriate low watermark from the given high-watermark.
        /// </summary>
        /// <param name="highWatermark">the given high-watermark value to compute it from</param>
        /// <param name="predefinedLowWatermark">predefined low watermark value coming from configuration</param>
        /// <returns>the computed low-watermark</returns>
        private static int ComputeLowWatermark(int highWatermark, int predefinedLowWatermark)
        {
            if (predefinedLowWatermark > 0 && predefinedLowWatermark < highWatermark)
                return predefinedLowWatermark;

            // Compute the low water mark. Following point should be taken
            // into consideration:
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
            // Given the 3. it would be good to keep HWM and LWM as far apart as
            // possible to reduce the thread switching overhead to almost zero,
            // say HWM-LWM should be max_wm_delta.
            //
            // That done, we still we have to account for the cases where
            // HWM < max_wm_delta thus driving LWM to negative numbers.
            // Let's make LWM 1/2 of HWM in such cases.
            int result = (highWatermark > Config.MaxWatermarkDelta*2)
                ? highWatermark - Config.MaxWatermarkDelta
                : (highWatermark + 1)/2;

            return result;
        }

        /// <summary>
        /// Handles the delimiter read from the pipe.
        /// </summary>
        private void Delimit()
        {
            if (m_state == State.Active)
            {
                m_state = State.Delimited;
                return;
            }

            if (m_state == State.Pending)
            {
                m_outboundPipe = null;
                SendPipeTermAck(m_peer);
                m_state = State.Terminating;
                return;
            }

            // Delimiter in any other state is invalid.
            Debug.Assert(false);
        }

        /// <summary>
        /// Temporarily disconnects the inbound message stream and drops
        /// all the messages on the fly. Causes 'hiccuped' event to be generated in the peer.
        /// </summary>
        public void Hiccup()
        {
            // If termination is already under way do nothing.
            if (m_state != State.Active)
                return;

            // We'll drop the pointer to the in-pipe. From now on, the peer is
            // responsible for deallocating it.
            m_inboundPipe = null;

            // Create new in-pipe.
            m_inboundPipe = new YPipe<Msg>(Config.MessagePipeGranularity, "inpipe");
            m_inActive = true;

            // Notify the peer about the hiccup.
            SendHiccup(m_peer, m_inboundPipe);
        }

        /// <summary>
        /// Override the ToString method to return a string denoting the type and the parent.
        /// </summary>
        /// <returns>a string containing this type and also the value of the parent-property</returns>
        public override string ToString()
        {
            return base.ToString() + "[" + m_parent + "]";
        }
    }
}