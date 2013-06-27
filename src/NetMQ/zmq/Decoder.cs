/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

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

//  Helper base class for decoders that know the amount of data to read
//  in advance at any moment. Knowing the amount in advance is a property
//  of the protocol used. 0MQ framing protocol is based size-prefixed
//  paradigm, which qualifies it to be parsed by this class.
//  On the other hand, XML-based transports (like XMPP or SOAP) don't allow
//  for knowing the size of data to read in advance and should use different
//  decoding algorithms.
//
//  This class , the state machine that parses the incoming buffer.
//  Derived class should implement individual state machine actions.

using System;

namespace NetMQ.zmq
{
	public class Decoder : DecoderBase {
    
		private const int OneByteSizeReadyState = 0;
		private const int EightByteSizeReadyState = 1;
		private const int FlagsReadyState = 2;
		private const int MessageReadyState = 3;
    
		private readonly ByteArraySegment m_tmpbuf;
    
		private Msg m_inProgress;
		private readonly long m_maxmsgsize;
		private IMsgSink m_msgSink;

		public Decoder(int bufsize, long maxmsgsize, Endianness endian)
			: base(bufsize, endian)
		{
			m_maxmsgsize = maxmsgsize;
			m_tmpbuf = new ByteArraySegment(new byte[8]);
           
			//  At the beginning, read one byte and go to one_byte_size_ready state.
			NextStep (m_tmpbuf, 1, OneByteSizeReadyState);
		}
    
		//  Set the receiver of decoded messages.    
		public override void SetMsgSink(IMsgSink msgSink) 
		{
			m_msgSink = msgSink;
		}

		protected override bool Next() {
			switch(State) {
				case OneByteSizeReadyState:
					return OneByteSizeReady ();
				case EightByteSizeReadyState:
					return EightByteSizeReady ();
				case FlagsReadyState:
					return FlagsReady ();
				case MessageReadyState:
					return MessageReady ();
				default:
					return false;
			}
		}

		private bool OneByteSizeReady() {
			m_tmpbuf.Reset();
			
			//  First byte of size is read. If it is 0xff read 8-byte size.
			//  Otherwise allocate the buffer for message data and read the
			//  message data into it.
			byte first = m_tmpbuf[0];
			if (first == 0xff) {
				NextStep (m_tmpbuf, 8, EightByteSizeReadyState);
			} else {

				//  There has to be at least one byte (the flags) in the message).
				if (first == 0) {
					DecodingError ();
					return false;
				}                       

				//  in_progress is initialised at this point so in theory we should
				//  close it before calling zmq_msg_init_size, however, it's a 0-byte
				//  message and thus we can treat it as uninitialised...
				if (m_maxmsgsize >= 0 && (long) (first -1) > m_maxmsgsize) {
					DecodingError ();
					return false;

				}
				else {
					m_inProgress = new Msg(first-1);
				}

				NextStep (m_tmpbuf,1, FlagsReadyState);
			}
			return true;
		}
    
		private bool EightByteSizeReady() {
			m_tmpbuf.Reset();

			//  8-byte payload length is read. Allocate the buffer
			//  for message body and read the message data into it.
			long payloadLength = m_tmpbuf.GetLong(Endian, 0);
        
			//  There has to be at least one byte (the flags) in the message).
			if (payloadLength == 0) {
				DecodingError ();
				return false;
			}

			//  Message size must not exceed the maximum allowed size.
			if (m_maxmsgsize >= 0 && payloadLength - 1 > m_maxmsgsize) {
				DecodingError ();
				return false;
			}

			//  Message size must fit within range of size_t data type.
			if (payloadLength - 1 > int.MaxValue) {
				DecodingError ();
				return false;
			}

			int msgSize =  (int)(payloadLength - 1);
			//  in_progress is initialized at this point so in theory we should
			//  close it before calling init_size, however, it's a 0-byte
			//  message and thus we can treat it as uninitialized...
			m_inProgress = new Msg(msgSize);
        
			NextStep (m_tmpbuf, 1, FlagsReadyState);
        
			return true;

		}
    
		private bool FlagsReady() {
			m_tmpbuf.Reset();

			//  Store the flags from the wire into the message structure.
        
			int first = m_tmpbuf[0];
        
			m_inProgress.SetFlags ((MsgFlags)first & MsgFlags.More);

			NextStep (m_inProgress.Data,m_inProgress.Size,MessageReadyState);

			return true;
		}
    
		private bool MessageReady() {
			m_tmpbuf.Reset();

			//  Message is completely read. Push it further and start reading
			//  new message. (in_progress is a 0-byte message after this point.)
        
			if (m_msgSink == null)
				return false;

			try
			{
				m_msgSink.PushMsg(m_inProgress);
			
			}
			catch (NetMQException)
			{
				return false;
			}
        
			NextStep (m_tmpbuf, 1, OneByteSizeReadyState);
        
			return true;
		}


		//  Returns true if there is a decoded message
		//  waiting to be delivered to the session.
    
		public override bool  Stalled ()
		{
			return State == MessageReadyState;
		}


	}
}
