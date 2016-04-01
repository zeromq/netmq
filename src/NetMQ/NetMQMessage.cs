using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace NetMQ
{
    #region Doc-stuff
    /// <summary>
    /// This namespace encompasses the NetMQ message-queueing library
    /// and attendant utility software.
    /// </summary>
    public static class NamespaceDoc
    {
        // This class exists only to provide a hook for adding summary documentation
        // for the NetMQ namespace.
    }
    #endregion

    /// <summary>
    /// A NetMQMessage is basically a collection of NetMQFrames, and is the basic message-unit that is sent and received
    /// across this message-queueing subsystem.
    /// </summary>
    public class NetMQMessage : IEnumerable<NetMQFrame>
    {
        /// <summary>
        /// This is the frame-stack that comprises the message-content of this message.
        /// </summary>
        private readonly List<NetMQFrame> m_frames;

        #region Constructors

        /// <summary>
        /// The default-constructor for NetMQMessage: create a new instance of NetMQMessage
        /// with an empty frame-stack.
        /// </summary>
        public NetMQMessage(int expectedFrameCount = 4)
        {
            m_frames = new List<NetMQFrame>(expectedFrameCount);
        }

        /// <summary>
        /// Create a new instance of a NetMQMessage that contains the given collection of NetMQFrames as its frame-stack.
        /// </summary>
        /// <param name="frames">a collection of NetMQFrames, to form the frame-stack</param>
        /// <exception cref="ArgumentNullException">The value of 'frames' cannot be null. </exception>
        public NetMQMessage([NotNull] IEnumerable<NetMQFrame> frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));

            m_frames = new List<NetMQFrame>(frames);
        }

        /// <summary>
        /// Create a new instance of a NetMQMessage that contains the given collection of byte-arrays as its frame-stack.
        /// </summary>
        /// <param name="buffers">a collection of byte-array buffers, to form the frame-stack</param>
        /// <exception cref="ArgumentNullException">The value of 'buffers' cannot be null. </exception>
        public NetMQMessage([NotNull] IEnumerable<byte[]> buffers)
        {
            if (buffers == null)
                throw new ArgumentNullException(nameof(buffers));

            m_frames = buffers.Select(buf => new NetMQFrame(buf)).ToList();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the first frame in the current message.
        /// </summary>
        [NotNull]
        public NetMQFrame First => m_frames[0];

        /// <summary>
        /// Gets the last frame in the current message.
        /// </summary>
        [NotNull]
        public NetMQFrame Last => m_frames[m_frames.Count - 1];

        /// <summary>
        /// Gets a value indicating whether the current message is empty.
        /// </summary>
        public bool IsEmpty => m_frames.Count == 0;

        /// <summary>
        /// Gets the number of <see cref="NetMQFrame"/> objects contained by this message.
        /// </summary>
        public int FrameCount => m_frames.Count;

        /// <summary>
        /// Gets the <see cref="NetMQFrame"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the <see cref="NetMQFrame"/> to get.</param>
        /// <returns>The <see cref="NetMQFrame"/> at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/>is less than 0 -or- <paramref name="index"/> is equal to or greater than <see cref="FrameCount"/>.
        /// </exception>
        [NotNull]
        public NetMQFrame this[int index] => m_frames[index];

        #endregion

        #region Appending frames

        /// <summary>
        /// Add the given NetMQFrame to this NetMQMessage, at the highest-indexed position of the frame-stack.
        /// </summary>
        /// <param name="frame">a NetMQFrame object comprising the frame to be appended onto the frame-stack</param>
        public void Append([NotNull] NetMQFrame frame)
        {
            m_frames.Add(frame);
        }

        /// <summary>
        /// Add the given data (in this case a byte-array) to this NetMQMessage, at the highest-indexed position of the frame-stack.
        /// Data is not copied.
        /// </summary>
        /// <param name="buffer">a byte-array containing the message to append onto the frame-stack of this NetMQMessage</param>
        public void Append([NotNull] byte[] buffer)
        {
            m_frames.Add(new NetMQFrame(buffer));
        }

        /// <summary>
        /// Add the given string - which gets converted into a NetMQFrame - onto
        /// the highest-indexed position of the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="message">a string containing the message to append onto the frame-stack of this NetMQMessage</param>
        public void Append([NotNull] string message)
        {
            m_frames.Add(new NetMQFrame(message));
        }

        /// <summary>
        /// Add the given string - which gets converted into a NetMQFrame - onto
        /// the highest-indexed position of the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="message">a string containing the message to append onto the frame-stack of this NetMQMessage</param>
        /// <param name="encoding">an Encoding that specifies how to convert the string into bytes</param>
        public void Append([NotNull] string message, [NotNull] Encoding encoding)
        {
            m_frames.Add(new NetMQFrame(message, encoding));
        }

        /// <summary>
        /// Convert the given integer value into an array of bytes and add it as a new frame onto this NetMQMessage.
        /// </summary>
        /// <param name="value">a 32-bit integer value that is to be converted into bytes and added to this message</param>
        public void Append(int value)
        {
            Append(NetworkOrderBitsConverter.GetBytes(value));
        }

        /// <summary>
        /// Convert the given long value into an array of bytes and add it as a new frame onto this NetMQMessage.
        /// </summary>
        /// <param name="value">a 64-bit number that is to be converted into bytes and added to this message</param>
        public void Append(long value)
        {
            Append(NetworkOrderBitsConverter.GetBytes(value));
        }

        /// <summary>
        /// Add an empty frame to this NetMQMessage.
        /// </summary>
        public void AppendEmptyFrame()
        {
            m_frames.Add(NetMQFrame.Empty);
        }

        #endregion

        #region Pushing frames

        /// <summary>
        /// Push the given NetMQFrame into the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="frame">the NetMQFrame to be inserted into the frame-stack</param>
        /// <remarks>
        /// The concept is the same as pushing an element onto a stack.
        /// This inserts the given NetMQFrame into the lowest-indexed position of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// </remarks>
        public void Push([NotNull] NetMQFrame frame)
        {
            m_frames.Insert(0, frame);
        }

        /// <summary>
        /// Push a new frame containing the given byte-array into the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="buffer">the byte-array to create a new frame from</param>
        /// <remarks>
        /// The concept is the same as pushing an element onto a stack.
        /// This creates a new frame from the given data (in this case a byte-array) and inserts it into the lowest-indexed position of 
        /// the collection of frames of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// </remarks>
        public void Push([NotNull] byte[] buffer)
        {
            m_frames.Insert(0, new NetMQFrame(buffer));
        }

        /// <summary>
        /// Push a new frame containing the given string (converted into a byte-array) into the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="message">the string to create a new frame from</param>
        /// <remarks>
        /// The concept is the same as pushing an element onto a stack.
        /// This creates a new frame from the given data (in this case a string which gets converted into a byte-array using the default ASCII encoding) and inserts it into the lowest-indexed position of 
        /// the collection of frames of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// </remarks>
        public void Push([NotNull] string message)
        {
            m_frames.Insert(0, new NetMQFrame(message));
        }

        /// <summary>
        /// Push a new frame containing the given string (converted into a byte-array) into the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="message">the string to create a new frame from</param>
        /// <param name="encoding">the Encoding that dictates how to encode the string into bytes</param>
        /// <remarks>
        /// The concept is the same as pushing an element onto a stack.
        /// This creates a new frame from the given data (in this case a string which gets converted into a byte-array using the given Encoding) and inserts it into the lowest-indexed position of 
        /// the collection of frames of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// </remarks>
        public void Push([NotNull] string message, [NotNull] Encoding encoding)
        {
            m_frames.Insert(0, new NetMQFrame(message, encoding));
        }

        /// <summary>
        /// Push a new frame containing the given integer (converted into a byte-array) into the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="value">the integer to create a new frame from</param>
        /// <remarks>
        /// The concept is the same as pushing an element onto a stack.
        /// This creates a new frame from the given data (in this case a 32-bit integer which gets converted into a byte-array in big-endian order) and inserts it into the lowest-indexed position of 
        /// the collection of frames of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// </remarks>
        public void Push(int value)
        {
            Push(NetworkOrderBitsConverter.GetBytes(value));
        }

        /// <summary>
        /// Push a new frame containing the given long (converted into a byte-array) into the frame-stack of this NetMQMessage.
        /// </summary>
        /// <param name="value">the 64-bit number to create a new frame from</param>
        /// <remarks>
        /// The concept is the same as pushing an element onto a stack.
        /// This creates a new frame from the given data (in this case a 64-bit long which gets converted into a byte-array in big-endian order) and inserts it into the lowest-indexed position of 
        /// the collection of frames of this NetMQMessage,
        /// pushing all of the other frames upward in index-position.
        /// </remarks>
        public void Push(long value)
        {
            Push(NetworkOrderBitsConverter.GetBytes(value));
        }

        /// <summary>
        /// Push an empty frame (a NetMQFrame.Empty) onto the frame-stack.
        /// </summary>
        public void PushEmptyFrame()
        {
            m_frames.Insert(0, NetMQFrame.Empty);
        }

        #endregion

        /// <summary>
        /// Remove and return the first frame.
        /// </summary>
        /// <returns>the first frame, which was popped - which is the frame from the lowest-indexed position</returns>
        [NotNull]
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
        /// <returns><c>true</c> if removed, otherwise <c>false</c>.</returns>
        public bool RemoveFrame([NotNull] NetMQFrame frame)
        {
            return m_frames.Remove(frame);
        }

        /// <summary>
        /// Clear (empty) the frame-stack, so that it no longer contains any frames.
        /// </summary>
        public void Clear()
        {
            m_frames.Clear();
        }

        #region IEnumerable

        /// <summary>
        /// Return an enumerator over the frames contained within this message.
        /// </summary>
        /// <returns>an IEnumerator over the frames in this message</returns>
        public IEnumerator<NetMQFrame> GetEnumerator()
        {
            return m_frames.GetEnumerator();
        }

        /// <summary>
        /// Return an enumerator over the frames contained within this message.
        /// </summary>
        /// <returns>an IEnumerator over the frames in this message</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Formatting

        /// <summary>
        /// Returns a string showing the frame contents.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (m_frames.Count == 0)
                return "NetMQMessage[<no frames>]";
            var sb = new StringBuilder("NetMQMessage[");
            bool first = true;
            foreach (var f in m_frames)
            {
                if (!first)
                    sb.Append(",");
                sb.Append(f.ConvertToString());
                first = false;
            }
            return sb.Append("]").ToString();
        }

        #endregion
    }
}
