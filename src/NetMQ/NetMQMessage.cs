using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
    public class NetMQMessage : IEnumerable<NetMQFrame>
    {
        private List<NetMQFrame> m_frames;

        public NetMQMessage()
        {
            m_frames = new List<NetMQFrame>();
        }

        public NetMQMessage(IEnumerable<NetMQFrame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException("frames");
            }

            m_frames = new List<NetMQFrame>(frames);
        }

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

        public void Append(NetMQFrame frame)
        {
            m_frames.Add(frame);
        }

        public void Append(byte[] buffer)
        {
            m_frames.Add(new NetMQFrame(buffer));
        }

        public void Append(string message)
        {
            m_frames.Add(new NetMQFrame(message));
        }

        public void Append(string message, Encoding encoding)
        {
            m_frames.Add(new NetMQFrame(message, encoding));
        }

        public void Append(int value)
        {            
            Append(NetworkOrderBitsConverter.GetBytes(value));
        }

        public void Append(long value)
        {
            Append(NetworkOrderBitsConverter.GetBytes(value));
        }

        [Obsolete("Use NetMQFrame instead of blobs")]
        public void Append(Blob blob)
        {
            Append(blob.Data);
        }

        public void AppendEmptyFrame()
        {
            m_frames.Add(NetMQFrame.Empty);
        }

        public void Push(NetMQFrame frame)
        {
            m_frames.Insert(0, frame);
        }

        public void Push(byte[] buffer)
        {
            m_frames.Insert(0, new NetMQFrame(buffer));
        }

        public void Push(string message)
        {
            m_frames.Insert(0, new NetMQFrame(message));
        }

        public void Push(string message, Encoding encoding)
        {
            m_frames.Insert(0, new NetMQFrame(message, encoding));
        }

        public void Push(int value)
        {
            Push(NetworkOrderBitsConverter.GetBytes(value));
        }

        public void Push(long value)
        {
            Push(NetworkOrderBitsConverter.GetBytes(value));
        }

        [Obsolete("Use NetMQFrame instead of blobs")]
        public void Push(Blob blob)
        {
            Push(blob.Data);
        }

        /// <summary>
        /// Remove the first frame
        /// </summary>
        /// <returns></returns>
        public NetMQFrame Pop()
        {
            NetMQFrame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return frame;
        }

        public void RemoveFrame(NetMQFrame frame)
        {
            m_frames.Remove(frame);
        }

        public void PushEmptyFrame()
        {
            m_frames.Insert(0, NetMQFrame.Empty);
        }

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

        /// <summary>
        /// Returns a string showing the frame contents.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (m_frames.Count == 0)
                return "NetMQMessage[<no frames>]";
            StringBuilder sb = new StringBuilder("NetMQMessage[");
            bool first = true;
            foreach (NetMQFrame f in m_frames)
            {
                if (!first)
                    sb.Append(",");
                sb.Append(f.ConvertToString());
                first = false;
            }
            return sb.Append("]").ToString();
        }
    }
}
