using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NetMQ
{
    public class NetMQMessage : IEnumerable<NetMQFrame>
    {
        /// <summary>
        /// This is the frame-stack that comprises the message-content of this message.
        /// </summary>
        private List<NetMQFrame> m_frames;

        /// <summary>
        /// The default-constructor for NetMQMessage: create a new instance of NetMQMessage
        /// with an empty frame-stack.
        /// </summary>
        public NetMQMessage()
        {
            m_frames = new List<NetMQFrame>();
        }

        /// <summary>
        /// Create a new instance of a NetMQMessage that contains the given collection of NetMQFrames as its frame-stack.
        /// </summary>
        /// <param name="frames">a collection of NetMQFrames, to form the frame-stack</param>
        public NetMQMessage(IEnumerable<NetMQFrame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException("frames");
            }

            m_frames = new List<NetMQFrame>(frames);
        }

        /// <summary>
        /// Create a new instance of a NetMQMessage that contains the given collection of byte-arrays as its frame-stack.
        /// </summary>
        /// <param name="buffers">a collection of byte-array buffers, to form the frame-stack</param>
        public NetMQMessage(IEnumerable<byte[]> buffers)
        {
            if (buffers == null)
            {
                throw new ArgumentNullException("buffers");
            }

            m_frames = buffers.Select(buf => new NetMQFrame(buf)).ToList();
        }

        /// <summary>
        /// Gets the first frame in the current message.
        /// </summary>
        public NetMQFrame First
        {
            get { return m_frames[0]; }
        }

        /// <summary>
        /// Gets the last frame in the current message.
        /// </summary>
        public NetMQFrame Last
        {
            get { return m_frames[m_frames.Count - 1]; }
        }

        /// <summary>
        /// Gets a value indicating whether the current message is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return m_frames.Count == 0; }
        }

        /// <summary>
        /// Gets the number of <see cref="NetMQFrame"/> objects contained by this message.
        /// </summary>
        public int FrameCount
        {
            get { return m_frames.Count; }
        }

        /// <summary>
        /// Gets the <see cref="NetMQFrame"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the <see cref="NetMQFrame"/> to get.</param>
        /// <returns>The <see cref="NetMQFrame"/> at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/>is less than 0 -or- <paramref name="index"/> is equal to or greater than <see cref="FrameCount"/>.
        /// </exception>
        public NetMQFrame this[int index]
        {
            get { return m_frames[index]; }
        }

        /// <summary>
        /// Add the given NetMQFrame to this NetMQMessage, at the highest-indexed position of the frame-stack.
        /// </summary>
        /// <param name="frame">a NetMQFrame object comprising the frame to be appended onto the frame-stack</param>
        public void Append(NetMQFrame frame)
        {
            m_frames.Add(frame);
        }

        /// <summary>
        /// Add the given message (in this case a byte-array) to this NetMQMessage, at the highest-indexed position of the frame-stack.
        /// </summary>
        /// <param name="buffer">a byte-array containing the message to append onto the frame-stack of this NetMQMessage</param>
        public void Append(byte[] buffer)
        {
            m_frames.Add(new NetMQFrame(buffer));
        }

        /// <summary>
        /// Add the given message - which gets converted into a NetMQFrame - onto
        /// the highest-indexed position of the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="message">a string containing the message to append onto the frame-stack of this NetMQMessage</param>
        public void Append(string message)
        {
            m_frames.Add(new NetMQFrame(message));
        }

        public void AppendEmptyFrame()
        {
            m_frames.Add(NetMQFrame.Empty);
        }

        /// <summary>
        /// Insert the given NetMQFrame into the lowest-indexed position of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// The concept is the same as pushing an element onto a stack.
        /// </summary>
        /// <param name="frame">a NetMQFrame object comprising the frame to be pushed onto the frame-stack</param>
        public void Push(NetMQFrame frame)
        {
            m_frames.Insert(0, frame);
        }

        /// <summary>
        /// Insert the given message (in this case a byte-array) into the lowest-indexed position of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// The concept is the same as pushing an element onto a stack.
        /// </summary>
        /// <param name="buffer">a byte-array containing the message to push onto the NetMQMessage</param>
        public void Push(byte[] buffer)
        {
            m_frames.Insert(0, new NetMQFrame(buffer));
        }

        /// <summary>
        /// Insert the given message - which gets converted into a NetMQFrame - into
        /// the lowest-indexed position of the frame-stack of this NetMQMessage,
        /// pushing all of the other content upward in index-position.
        /// The concept is the same as pushing an element onto a stack.
        /// </summary>
        /// <param name="message">a string containing the message to push onto the frame-stack of this NetMQMessage</param>
        public void Push(string message)
        {
            m_frames.Insert(0, new NetMQFrame(message));
        }

        /// <summary>
        /// Remove the first frame
        /// </summary>
        /// <returns>the first frame, which was popped - which is the frame from the lowest-indexed position</returns>
        public NetMQFrame Pop()
        {
            NetMQFrame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return frame;
        }

        /// <summary>
        /// Delete the given frame from the frame-stack.
        /// </summary>
        /// <param name="frame">the frame to remove</param>
        public void RemoveFrame(NetMQFrame frame)
        {
            m_frames.Remove(frame);
        }

        /// <summary>
        /// Push an empty frame (a NetMQFrame.Empty) onto the frame-stack.
        /// </summary>
        public void PushEmptyFrame()
        {
            m_frames.Insert(0, NetMQFrame.Empty);
        }

        /// <summary>
        /// Clear (empty) the frame-stack, so that it no longer contains any frames.
        /// </summary>
        public void Clear()
        {
            m_frames.Clear();
        }

        public IEnumerator<NetMQFrame> GetEnumerator()
        {
            return m_frames.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
