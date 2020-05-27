namespace NetMQ.Core.Transports
{
    internal class V2Decoder : DecoderBase
    {
        private const int OneByteSizeReadyState = 0;
        private const int EightByteSizeReadyState = 1;
        private const int FlagsReadyState = 2;
        private const int MessageReadyState = 3;

        private readonly ByteArraySegment m_tmpbuf;
        private Msg m_inProgress;
        private readonly long m_maxmsgsize;
        private MsgFlags m_msgFlags;

        public V2Decoder(int bufsize, long maxmsgsize, Endianness endian)
            : base(bufsize, endian)
        {
            m_maxmsgsize = maxmsgsize;

            m_tmpbuf = new byte[8];

            // At the beginning, read one byte and go to one_byte_size_ready state.
            NextStep(m_tmpbuf, 1, FlagsReadyState);

            m_inProgress = new Msg();
            m_inProgress.InitEmpty();
        }
        
        protected override DecodeResult Next()
        {
            switch (State)
            {
                case OneByteSizeReadyState:
                    return OneByteSizeReady();
                case EightByteSizeReadyState:
                    return EightByteSizeReady();
                case FlagsReadyState:
                    return FlagsReady();
                case MessageReadyState:
                    return MessageReady();
                default:
                    return DecodeResult.Error;
            }
        }
        
        private DecodeResult FlagsReady()
        {
            m_tmpbuf.Reset();

            // Store the flags from the wire into the message structure.
            m_msgFlags = 0;
            int first = m_tmpbuf[0];
            if ((first & V2Protocol.MoreFlag) > 0)
                m_msgFlags |= MsgFlags.More;
            if ((first & V2Protocol.CommandFlag) > 0)
                m_msgFlags |= MsgFlags.Command;

            // The payload length is either one or eight bytes,
            // depending on whether the 'large' bit is set.
            if ((first & V2Protocol.LargeFlag) > 0)
                NextStep(m_tmpbuf, 8, EightByteSizeReadyState);
            else
                NextStep(m_tmpbuf, 1, OneByteSizeReadyState);

            return DecodeResult.Processing;
        }
        
        private DecodeResult OneByteSizeReady()
        {
            m_tmpbuf.Reset();

            // Message size must not exceed the maximum allowed size.
            if (m_maxmsgsize >= 0 && m_tmpbuf[0] > m_maxmsgsize)
                return DecodeResult.Error;

            // in_progress is initialised at this point so in theory we should
            // close it before calling zmq_msg_init_size, however, it's a 0-byte
            // message and thus we can treat it as uninitialised...
            m_inProgress.InitPool(m_tmpbuf[0]);

            m_inProgress.SetFlags(m_msgFlags);

            Assumes.NotNull(m_inProgress.UnsafeData);

            NextStep(new ByteArraySegment(m_inProgress.UnsafeData, m_inProgress.UnsafeOffset),
                m_inProgress.Size, MessageReadyState);

            return DecodeResult.Processing;
        }

        private DecodeResult EightByteSizeReady()
        {
            m_tmpbuf.Reset();

            // The payload size is encoded as 64-bit unsigned integer.
            // The most significant byte comes first.
            ulong msgSize = m_tmpbuf.GetUnsignedLong(Endian, 0);

            // Message size must not exceed the maximum allowed size.
            if (m_maxmsgsize >= 0 && msgSize > (ulong)m_maxmsgsize)
                return DecodeResult.Error;
            
            // TODO: move this constant to a good place (0x7FFFFFC7)
            // Message size must fit within range of size_t data type.
            if (msgSize > 0x7FFFFFC7)
                return DecodeResult.Error;

            // in_progress is initialised at this point so in theory we should
            // close it before calling init_size, however, it's a 0-byte
            // message and thus we can treat it as uninitialised.
            m_inProgress.InitPool((int)msgSize);

            m_inProgress.SetFlags(m_msgFlags);

            Assumes.NotNull(m_inProgress.UnsafeData);

            NextStep(new ByteArraySegment(m_inProgress.UnsafeData, m_inProgress.UnsafeOffset),
                m_inProgress.Size, MessageReadyState);

            return DecodeResult.Processing;
        }
        
        private DecodeResult MessageReady()
        {
            m_tmpbuf.Reset();

            // Message is completely read. Push it further and start reading
            // new message. (in_progress is a 0-byte message after this point.)
            NextStep(m_tmpbuf, 1, FlagsReadyState);
            return DecodeResult.MessageReady;
        }

        public override PushMsgResult PushMsg(ProcessMsgDelegate sink) => sink(ref m_inProgress);
    }
}
