using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetMQ.zmq;

namespace NetMQ
{
    public class NetMQFrame : IEquatable<NetMQFrame>, IEquatable<byte[]>
    {
        private int m_messageSize;
        private int m_hash = 0;

        public NetMQFrame (byte[] buffer)
        {
            if (buffer == null)
            {
                buffer = new byte[0];
            }

            Buffer = buffer;
            MessageSize = buffer.Length;
        }

        public NetMQFrame (string message)
            : this (Encoding.ASCII.GetBytes (message))
        {

        }

        public NetMQFrame (string message, Encoding encoding)
            : this (encoding.GetBytes (message))
        {

        }

        public NetMQFrame (int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException ("length", "A non-negative value is expected.");
            }

            Buffer = new byte[length];
            MessageSize = length;
        }


        /// <summary>
        /// Gets or sets the size of the message data contained in the frame.
        /// </summary>
        public int MessageSize
        {
            get { return m_messageSize; }
            set
            {
                if (value < 0 || value > BufferSize)
                {
                    throw new ArgumentOutOfRangeException ("value", "Expected non-negative value less than or equal to the buffer size.");
                }

                m_messageSize = value;
            }
        }

        /// <summary>
        /// Gets the underlying frame data buffer.
        /// </summary>       
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Gets the maximum size of the frame buffer.
        /// </summary>
        public int BufferSize
        {
            get { return Buffer == null ? 0 : Buffer.Length; }
        }

        /// <summary>
        /// Gets an empty <see cref="NetMQFrame"/> that may be used as message separators.
        /// </summary>
        public static NetMQFrame Empty
        {
            get { return new NetMQFrame (0); }
        }


        /// <summary>
        /// Create a copy of the supplied buffer and store it in a <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="byte"/> array to copy.</param>
        /// <returns>A <see cref="NetMQFrame"/> containing a copy of <paramref name="buffer"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        public static NetMQFrame Copy (byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException ("buffer");
            }

            var copy = new NetMQFrame (buffer.Length);

            System.Buffer.BlockCopy (buffer, 0, copy.Buffer, 0, buffer.Length);

            return copy;
        }

        public string ConvertToString ()
        {
            return Encoding.ASCII.GetString (Buffer, 0, this.MessageSize);
        }

        public string ConvertToString (Encoding encoding)
        {
            return encoding.GetString (Buffer, 0, this.MessageSize);
        }

        /// <summary>
        /// Convert the buffer to integer in network byte order (Big endian)
        /// </summary>
        /// <returns></returns>
        public int ConvertToInt32 ()
        {
            return NetworkOrderBitsConverter.ToInt32 (Buffer);
        }

        /// <summary>
        /// Convert the buffer to long in network byte order (Big endian)
        /// </summary>
        /// <returns></returns>
        public long ConvertToInt64 ()
        {
            return NetworkOrderBitsConverter.ToInt64 (Buffer);
        }

        /// <summary>
        /// Create a copy of the supplied <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="frame">The <see cref="NetMQFrame"/> to copy.</param>
        /// <returns>A <see cref="NetMQFrame"/> containing a copy of <paramref name="frame"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        public static NetMQFrame Copy (NetMQFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException ("frame");
            }

            var copy = new NetMQFrame (new byte[frame.BufferSize]);
            copy.MessageSize = frame.MessageSize;

            System.Buffer.BlockCopy (frame.Buffer, 0, copy.Buffer, 0, frame.BufferSize);

            return copy;
        }

        public NetMQFrame Duplicate ()
        {
            return Copy (this);
        }

        public bool Equals (byte[] other)
        {
            if (other == null)
                return false;

            if (other.Length != MessageSize)
                return false;

            if (ReferenceEquals (Buffer, other))
                return true;

            for (int i = 0; i < MessageSize; i++)
            {
                if (Buffer[i] != other[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="NetMQFrame"/> is equal to the current <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="other">The <see cref="NetMQFrame"/> to compare with the current <see cref="NetMQFrame"/>.</param>
        /// <returns>true if the specified System.Object is equal to the current System.Object; otherwise, false.</returns>
        public bool Equals (NetMQFrame other)
        {
            if (ReferenceEquals (other, null))
                return false;

            if (ReferenceEquals (this, other))
                return true;

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

        public static bool operator == (NetMQFrame one, NetMQFrame other)
        {
            if (ReferenceEquals (one, null) && ReferenceEquals (other, null))
                return true;

            return !ReferenceEquals (one, null) && one.Equals (other);
        }

        public static bool operator != (NetMQFrame one, NetMQFrame other)
        {
            return !(one == other);
        }

        bool IEquatable<NetMQFrame>.Equals (NetMQFrame other)
        {
            return Equals (other);
        }

        public override bool Equals (object obj)
        {
            return Equals (obj as NetMQFrame);
        }

        public override int GetHashCode ()
        {
            if (m_hash == 0)
            {
                foreach (byte b in Buffer)
                {
                    m_hash = 31 * m_hash + b;
                }
            }

            return m_hash;
        }


        public byte[] ToByteArray (bool copy = false)
        {
            if (!copy || MessageSize == BufferSize)
            {
                return Buffer;
            }
            else
            {
                byte[] byteArray = new byte[MessageSize];

                System.Buffer.BlockCopy (Buffer, 0, byteArray, 0, MessageSize);

                return byteArray;
            }
        }
    }
}
