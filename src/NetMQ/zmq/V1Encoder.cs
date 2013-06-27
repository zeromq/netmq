// Encoder for 0MQ framing protocol. Converts messages into data stream.

namespace NetMQ.zmq
{
	public class V1Encoder : EncoderBase
	{
		private const int SizeReadyState = 0;
		private const int MessageReadyState = 1;
    
		private Msg m_inProgress;
		private readonly ByteArraySegment m_tmpbuf;

		private IMsgSource m_msgSource;
    
		public V1Encoder (int bufsize, IMsgSource session, Endianness endian) : base(bufsize, endian)
		{
			m_tmpbuf = new byte [9];
			m_msgSource = session;

			//  Write 0 bytes to the batch and go to message_ready state.
			NextStep(m_tmpbuf, 0, MessageReadyState, true);
		}


		public override void SetMsgSource (IMsgSource msgSource)
		{
			m_msgSource = msgSource;
		}

		protected override bool Next() 
		{
			switch(State) {
				case SizeReadyState:
					return SizeReady ();
				case MessageReadyState:
					return MessageReady ();
				default:
					return false;
			}
		}

		private bool SizeReady ()
		{      
			//  Write message body into the buffer.
			NextStep(m_inProgress.Data, m_inProgress.Size,
			         MessageReadyState, !m_inProgress.HasMore);
			return true;
		}


		private bool MessageReady()
		{
			m_tmpbuf.Reset();

			//  Read new message. If there is none, return false.
			//  Note that new state is set only if write is successful. That way
			//  unsuccessful write will cause retry on the next state machine
			//  invocation.
        
			if (m_msgSource == null)
				return false;
        
			m_inProgress = m_msgSource.PullMsg ();
			if (m_inProgress == null) {
				return false;
			}

			int protocolFlags = 0;
			if (m_inProgress.HasMore )
				protocolFlags |= V1Protocol.MoreFlag;
			if (m_inProgress.Size > 255)
				protocolFlags |= V1Protocol.LargeFlag;
			m_tmpbuf [0] = (byte) protocolFlags;
        
			//  Encode the message length. For messages less then 256 bytes,
			//  the length is encoded as 8-bit unsigned integer. For larger
			//  messages, 64-bit unsigned integer in network byte order is used.
			int size = m_inProgress.Size;
			if (size > 255) {
				m_tmpbuf.PutLong(Endian, size, 1);

				NextStep(m_tmpbuf, 9, SizeReadyState, false);
			}
			else {
				m_tmpbuf [1] = (byte) (size);
				NextStep(m_tmpbuf, 2, SizeReadyState, false);
			}
			return true;
		}

	}
}
