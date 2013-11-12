using System;
using System.Text;

namespace NetMQ
{
    public class NetMQFrame : IEquatable<NetMQFrame>
    {
        /// <summary>
        /// This is the length of the byte-array data buffer.
        /// </summary>
        private int m_messageSize;

        /// <summary>
        /// This empty NetMQFrame is created once here so that the Empty property does not need to create new such values.
        /// </summary>
        private static readonly NetMQFrame s_EmptyFrame = new NetMQFrame(0);

        /// <summary>
        /// Create a new NetMQFrame containing the given byte-array data.
        /// </summary>
        /// <param name="buffer">a byte-array to hold as the frame's data</param>
        public NetMQFrame(byte[] buffer)
        {
            if (buffer == null)
            {
                buffer = new byte[0];
            }

            Buffer = buffer;
            MessageSize = buffer.Length;
        }

        /// <summary>
        /// Create a new NetMQFrame containing the given string-message.
        /// </summary>
        /// <param name="message">a string containing the message-data of the frame</param>
        public NetMQFrame(string message)
            : this(Encoding.ASCII.GetBytes(message))
        {
        }

        /// <summary>
        /// Create a new NetMQFrame with a data-buffer pre-sized to the given length.
        /// </summary>
        /// <param name="length">the number of bytes to allocate for the data-buffer</param>
        public NetMQFrame(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "A non-negative value is expected.");
            }

            Buffer = new byte[length];
            MessageSize = length;
        }


        /// <summary>
        /// Get or set the size of the message data contained in the frame, which here represents the number of bytes.
        /// </summary>
        public int MessageSize
        {
            get { return m_messageSize; }
            set
            {
                if (value < 0 || value > BufferSize)
                {
                    throw new ArgumentOutOfRangeException("value", "Expected non-negative value less than or equal to the buffer size.");
                }

                m_messageSize = value;
            }
        }

        /// <summary>
        /// Get the underlying frame-data buffer, which is an array of bytes.
        /// </summary>       
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Get the maximum size of the frame-data buffer (ie, the number of bytes of the array).
        /// </summary>
        public int BufferSize
        {
            get { return Buffer == null ? 0 : Buffer.Length; }
        }

        /// <summary>
        /// Get an empty <see cref="NetMQFrame"/> that may be used as message separators.
        /// </summary>
        public static NetMQFrame Empty
        {
            get { return s_EmptyFrame; }
        }

        /// <summary>
        /// Create a copy of the supplied buffer and store it in a new <see cref="NetMQFrame"/>, and return that NetMQFrame.
        /// </summary>
        /// <param name="buffer">The <see cref="byte"/> array to copy into a new NetMQFrame</param>
        /// <returns>a new <see cref="NetMQFrame"/> containing a copy of <paramref name="buffer"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        public static NetMQFrame Copy(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            var copy = new NetMQFrame(buffer.Length);

            System.Buffer.BlockCopy(buffer, 0, copy.Buffer, 0, buffer.Length);

            return copy;
        }

        /// <summary>
        /// Return a string representation of this frame's data-buffer.
        /// </summary>
        /// <returns></returns>
        public string ConvertToString()
        {
            return Encoding.ASCII.GetString(Buffer, 0, this.MessageSize);
        }

        /// <summary>
        /// Create a deep-copy of the supplied <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="frame">the <see cref="NetMQFrame"/> to copy</param>
        /// <returns>a <see cref="NetMQFrame"/> containing a copy of <paramref name="frame"/></returns>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        public static NetMQFrame Copy(NetMQFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            var copy = new NetMQFrame(new byte[frame.BufferSize]);
            copy.MessageSize = frame.MessageSize;

            System.Buffer.BlockCopy(frame.Buffer, 0, copy.Buffer, 0, frame.BufferSize);

            return copy;
        }

        /// <summary>
        /// Determine whether the specified <see cref="NetMQFrame"/> is equal to the current <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="other">the <see cref="NetMQFrame"/> to compare with the current <see cref="NetMQFrame"/>.</param>
        /// <returns>true if the specified NetMQFrame is equal to this one; otherwise, false.</returns>
        public bool Equals(NetMQFrame other)
        {
            if (MessageSize > other.BufferSize || MessageSize != other.MessageSize)
            {
                return false;
            }

            for (int i = 0; i < MessageSize; i++)
            {
                if (Buffer[i] != other.Buffer[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return an array of bytes that carries the content of this NetMQFrames Buffer.
        /// </summary>
        /// <returns>Buffers bytes as a byte-array, either newly-allocated or else simply a reference to the actual Buffer</returns>
        public byte[] ToByteArray()
        {
            return ToByteArray(false);
        }

        /// <summary>
        /// Return an array of bytes that carries the content of this NetMQFrames Buffer.
        /// </summary>
        /// <param name="copy">if this argument is true - a new copy is made if BufferSize is equal to MessageSize</param>
        /// <returns>Buffers bytes as a byte-array, either newly-allocated or else (if copy is false) simply a reference to the actual Buffer</returns>
        public byte[] ToByteArray(bool copy)
        {
            if (!copy || MessageSize == BufferSize)
            {
                return Buffer;
            }
            else
            {
                byte[] byteArray = new byte[MessageSize];

                System.Buffer.BlockCopy(Buffer, 0, byteArray, 0, MessageSize);

                return byteArray;
            }
        }
    }
}
