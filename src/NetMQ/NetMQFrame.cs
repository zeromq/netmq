using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
	public class NetMQFrame : IEquatable<NetMQFrame>
	{
		private int m_messageSize;

		public NetMQFrame(byte[] buffer)
		{
            if (buffer == null)
            {
                buffer = new byte[0];
            }

			Buffer = buffer;
			MessageSize = buffer.Length;
		}

		public NetMQFrame(string message) : this(Encoding.ASCII.GetBytes(message))
		{
			
		}

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
		/// Gets or sets the size of the message data contained in the frame.
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
			get { return new NetMQFrame(0); }
		}


		/// <summary>
		/// Create a copy of the supplied buffer and store it in a <see cref="NetMQFrame"/>.
		/// </summary>
		/// <param name="buffer">The <see cref="byte"/> array to copy.</param>
		/// <returns>A <see cref="NetMQFrame"/> containing a copy of <paramref name="buffer"/>.</returns>
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

		public string ConvertToString()
		{
			return Encoding.ASCII.GetString(Buffer, 0, this.MessageSize);
		}

		/// <summary>
		/// Create a copy of the supplied <see cref="NetMQFrame"/>.
		/// </summary>
		/// <param name="frame">The <see cref="NetMQFrame"/> to copy.</param>
		/// <returns>A <see cref="NetMQFrame"/> containing a copy of <paramref name="frame"/>.</returns>
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
		/// Determines whether the specified <see cref="NetMQFrame"/> is equal to the current <see cref="NetMQFrame"/>.
		/// </summary>
		/// <param name="other">The <see cref="NetMQFrame"/> to compare with the current <see cref="NetMQFrame"/>.</param>
		/// <returns>true if the specified System.Object is equal to the current System.Object; otherwise, false.</returns>
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

    public byte[] ToByteArray(bool copy = false)
    {
      if (!copy || MessageSize == BufferSize)
      {
        return Buffer;
      }
      else
      {
        byte[] byteArray = new byte[MessageSize];

        System.Buffer.BlockCopy(Buffer,0, byteArray, 0, MessageSize);

        return byteArray;
      }
    }
	}
}
