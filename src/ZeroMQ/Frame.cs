namespace ZeroMQ
{
    using System;

    /// <summary>
    /// A single message-part to be sent or received via a <see cref="ZmqSocket"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Frame"/> class has a one-to-one correspondence with the native <c>zmq_msg_t</c> struct.
    /// </remarks>
    public class Frame : IEquatable<Frame>
    {
        private int _messageSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="Frame"/> class. The contents of <paramref name="buffer"/>
        /// will be copied into the resulting <see cref="Frame"/>.
        /// </summary>
        /// <param name="buffer">A <see cref="byte"/> array containing the frame data to copy.</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        public Frame(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            Buffer = buffer;
            MessageSize = Buffer.Length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Frame"/> class. The resulting <see cref="Frame"/> will have
        /// a buffer size equal to <paramref name="length"/>.
        /// </summary>
        /// <param name="length">An <see cref="int"/> containing the frame buffer length.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
        public Frame(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "A non-negative value is expected.");
            }

            Buffer = new byte[length];
            MessageSize = length;
        }

        internal Frame(Frame frame)
            : this(frame, frame.Buffer)
        {
        }

        internal Frame(Frame frame, byte[] buffer)
        {
            Buffer = buffer;
            HasMore = frame.HasMore;
            MessageSize = frame.MessageSize;
            ReceiveStatus = frame.ReceiveStatus;
        }

        /// <summary>
        /// Gets an empty <see cref="Frame"/> that may be used as message separators.
        /// </summary>
        public static Frame Empty
        {
            get { return new Frame(0); }
        }

        /// <summary>
        /// Gets the underlying frame data buffer.
        /// </summary>
        public byte[] Buffer { get; internal set; }

        /// <summary>
        /// Gets the maximum size of the frame buffer.
        /// </summary>
        public int BufferSize
        {
            get { return Buffer.Length; }
        }

        /// <summary>
        /// Gets or sets the size of the message data contained in the frame.
        /// </summary>
        public int MessageSize
        {
            get
            {
                return _messageSize;
            }

            set
            {
                if (value < 0 || value > BufferSize)
                {
                    throw new ArgumentOutOfRangeException("value", "Expected non-negative value less than or equal to the buffer size.");
                }

                _messageSize = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether more frames will follow in a multi-part message sequence.
        /// </summary>
        public bool HasMore { get; set; }

        /// <summary>
        /// Gets the status of the last Recieve operation.
        /// </summary>
        public ReceiveStatus ReceiveStatus { get; internal set; }

        /// <summary>
        /// Converts a <see cref="Frame"/> to a <see cref="byte"/> array.
        /// </summary>
        /// <param name="frame">The <see cref="Frame"/> to convert.</param>
        /// <returns>The data contained by <paramref name="frame"/>.</returns>
        public static implicit operator byte[](Frame frame)
        {
            return frame.Buffer;
        }

        /// <summary>
        /// Create a copy of the supplied buffer and store it in a <see cref="Frame"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="byte"/> array to copy.</param>
        /// <returns>A <see cref="Frame"/> containing a copy of <paramref name="buffer"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        public static Frame Copy(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            var copy = new Frame(buffer.Length);

            System.Buffer.BlockCopy(buffer, 0, copy.Buffer, 0, buffer.Length);

            return copy;
        }

        /// <summary>
        /// Create a copy of the supplied <see cref="Frame"/>.
        /// </summary>
        /// <param name="frame">The <see cref="Frame"/> to copy.</param>
        /// <returns>A <see cref="Frame"/> containing a copy of <paramref name="frame"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        public static Frame Copy(Frame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            var copy = new Frame(frame, new byte[frame.BufferSize]);

            System.Buffer.BlockCopy(frame.Buffer, 0, copy.Buffer, 0, frame.BufferSize);

            return copy;
        }

        /// <summary>
        /// Determines whether the specified <see cref="Frame"/> is equal to the current <see cref="Frame"/>.
        /// </summary>
        /// <param name="other">The <see cref="Frame"/> to compare with the current <see cref="Frame"/>.</param>
        /// <returns>true if the specified System.Object is equal to the current System.Object; otherwise, false.</returns>
        public bool Equals(Frame other)
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
    }
}
