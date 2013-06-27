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

using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace NetMQ.zmq
{
	public class StreamEngine : IEngine, IPollEvents, IMsgSink
	{
		//  Size of the greeting message:
		//  Preamble (10 bytes) + version (1 byte) + socket type (1 byte).
		private const int GreetingSize = 12;

		//  Position of the version field in the greeting.
		private const int VersionPos = 10;

		//private IOObject io_object;
		private Socket m_handle;

		private ByteArraySegment m_inpos;
		private int m_insize;
		private DecoderBase m_decoder;
		private bool m_inputError;

		private ByteArraySegment m_outpos;
		private int m_outsize;
		private EncoderBase m_encoder;

		//  When true, we are still trying to determine whether
		//  the peer is using versioned protocol, and if so, which
		//  version.  When false, normal message flow has started.
		private bool m_handshaking;

		//const int greeting_size = 12;

		//  The receive buffer holding the greeting message
		//  that we are receiving from the peer.
		private readonly byte[] m_greeting = new byte[12];

		//  The number of bytes of the greeting message that
		//  we have already received.

		private int m_greetingBytesRead;

		//  The send buffer holding the greeting message
		//  that we are sending to the peer.
		private readonly ByteArraySegment m_greetingOutputBuffer = new byte[12];

		//  The session this engine is attached to.
		private SessionBase m_session;

		//  Detached transient session.
		//private SessionBase leftover_session;

		private readonly Options m_options;

		// String representation of endpoint
		private readonly String m_endpoint;

		private bool m_plugged;

		// Socket
		private SocketBase m_socket;

		private IOObject m_ioObject;


		public StreamEngine(Socket fd, Options options, String endpoint)
		{
			m_handle = fd;
			//      inbuf = null;
			m_insize = 0;
			m_inputError = false;
			//        outbuf = null;
			m_outsize = 0;
			m_handshaking = true;
			m_session = null;
			m_options = options;
			m_plugged = false;
			m_endpoint = endpoint;
			m_socket = null;
			m_encoder = null;
			m_decoder = null;

			//  Put the socket into non-blocking mode.
			Utils.UnblockSocket(m_handle);

			//  Set the socket buffer limits for the underlying socket.
			if (m_options.SendBuffer != 0)
			{
				m_handle.SendBufferSize = m_options.SendBuffer;
			}
			if (m_options.ReceiveBuffer != 0)
			{
				m_handle.ReceiveBufferSize = m_options.ReceiveBuffer;
			}
		}

		public void Destroy()
		{
			Debug.Assert(!m_plugged);

			if (m_handle != null)
			{
				try
				{
					m_handle.Close();
				}
				catch (SocketException)
				{
				}
				m_handle = null;
			}
		}

		public void Plug(IOThread ioThread,
										 SessionBase session)
		{
			Debug.Assert(!m_plugged);
			m_plugged = true;

			//  Connect to session object.
			Debug.Assert(m_session == null);
			Debug.Assert(session != null);
			m_session = session;
			m_socket = m_session.Socket;

			m_ioObject = new IOObject(null);
			m_ioObject.SetHandler(this);
			//  Connect to I/O threads poller object.
			m_ioObject.Plug(ioThread);
			m_ioObject.AddFd(m_handle);

			//  Send the 'length' and 'flags' fields of the identity message.
			//  The 'length' field is encoded in the long format.

			m_greetingOutputBuffer[m_outsize++] = ((byte)0xff);
			m_greetingOutputBuffer.PutLong(m_options.Endian, (long)m_options.IdentitySize + 1, 1);
			m_outsize += 8;
			m_greetingOutputBuffer[m_outsize++] = ((byte)0x7f);

			m_outpos = new ByteArraySegment(m_greetingOutputBuffer);

			m_ioObject.SetPollin(m_handle);
			m_ioObject.SetPollout(m_handle);

			//  Flush all the data that may have been already received downstream.
			InEvent();
		}

		private void Unplug()
		{
			Debug.Assert(m_plugged);
			m_plugged = false;

			//  Cancel all fd subscriptions.
			if (!m_inputError)
				m_ioObject.RmFd(m_handle);

			//  Disconnect from I/O threads poller object.
			m_ioObject.Unplug();

			//  Disconnect from session object.
			if (m_encoder != null)
				m_encoder.SetMsgSource(null);
			if (m_decoder != null)
				m_decoder.SetMsgSink(null);
			m_session = null;
		}

		public void Terminate()
		{
			Unplug();
			Destroy();
		}

		public void InEvent()
		{
			//  If still handshaking, receive and process the greeting message.
			if (m_handshaking)
				if (!Handshake())
					return;

			Debug.Assert(m_decoder != null);
			bool disconnection = false;

			//  If there's no data to process in the buffer...
			if (m_insize == 0)
			{
				//  Retrieve the buffer and read as much data as possible.
				//  Note that buffer can be arbitrarily large. However, we assume
				//  the underlying TCP layer has fixed buffer size and thus the
				//  number of bytes read will be always limited.
				m_decoder.GetBuffer(ref m_inpos, ref m_insize);
				m_insize = Read(m_inpos, m_insize);

				//  Check whether the peer has closed the connection.
				if (m_insize == -1)
				{
					m_insize = 0;
					disconnection = true;
				}
			}

			//  Push the data to the decoder.
			int processed = m_decoder.ProcessBuffer(m_inpos, m_insize);

			if (processed == -1)
			{
				disconnection = true;
			}
			else
			{
				//  Stop polling for input if we got stuck.
				if (processed < m_insize)
					m_ioObject.ResetPollin(m_handle);

				m_inpos.AdvanceOffset(processed);
				m_insize -= processed;
			}

			//  Flush all messages the decoder may have produced.
			m_session.Flush();

			//  An input error has occurred. If the last decoded message
			//  has already been accepted, we terminate the engine immediately.
			//  Otherwise, we stop waiting for socket events and postpone
			//  the termination until after the message is accepted.
			if (disconnection)
			{
				if (m_decoder.Stalled())
				{
					m_ioObject.RmFd(m_handle);
					m_inputError = true;
				}
				else
					Error();
			}
		}

		public void OutEvent()
		{
			//  If write buffer is empty, try to read new data from the encoder.
			if (m_outsize == 0)
			{
				m_outpos = null;
				m_encoder.GetData(ref m_outpos, ref m_outsize);

				//  If there is no data to send, stop polling for output.
				if (m_outsize == 0)
				{
					m_ioObject.ResetPollout(m_handle);

					return;
				}
			}

			//  If there are any data to write in write buffer, write as much as
			//  possible to the socket. Note that amount of data to write can be
			//  arbitratily large. However, we assume that underlying TCP layer has
			//  limited transmission buffer and thus the actual number of bytes
			//  written should be reasonably modest.
			int nbytes = Write(m_outpos, m_outsize);

			//  IO error has occurred. We stop waiting for output events.
			//  The engine is not terminated until we detect input error;
			//  this is necessary to prevent losing incomming messages.
			if (nbytes == -1)
			{
				m_ioObject.ResetPollout(m_handle);
				return;
			}

			m_outpos.AdvanceOffset(nbytes);
			m_outsize -= nbytes;

			//  If we are still handshaking and there are no data
			//  to send, stop polling for output.
			if (m_handshaking)
				if (m_outsize == 0)
					m_ioObject.ResetPollout(m_handle);
		}

		public void TimerEvent(int id)
		{
			throw new NotSupportedException();
		}

		public void ActivateOut()
		{
			m_ioObject.SetPollout(m_handle);

			//  Speculative write: The assumption is that at the moment new message
			//  was sent by the user the socket is probably available for writing.
			//  Thus we try to write the data to socket avoiding polling for POLLOUT.
			//  Consequently, the latency should be better in request/reply scenarios.
			OutEvent();
		}

		public void ActivateIn()
		{
			if (m_inputError)
			{
				//  There was an input error but the engine could not
				//  be terminated (due to the stalled decoder).
				//  Flush the pending message and terminate the engine now.
				m_decoder.ProcessBuffer(m_inpos, 0);
				Debug.Assert(!m_decoder.Stalled());
				m_session.Flush();
				Error();
				return;
			}

			m_ioObject.SetPollin(m_handle);

			//  Speculative read.
			m_ioObject.InEvent();
		}

		private bool Handshake()
		{
			Debug.Assert(m_handshaking);

			//  Receive the greeting.
			while (m_greetingBytesRead < GreetingSize)
			{
				ByteArraySegment greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);

				int n = Read(greetingSegment, GreetingSize - m_greetingBytesRead);
				if (n == -1)
				{
					Error();
					return false;
				}

				if (n == 0)
					return false;

				m_greetingBytesRead += n;

				//  We have received at least one byte from the peer.
				//  If the first byte is not 0xff, we know that the
				//  peer is using unversioned protocol.
				if (m_greeting[0] != 0xff)
					break;

				if (m_greetingBytesRead < 10)
					continue;

				//  Inspect the right-most bit of the 10th byte (which coincides
				//  with the 'flags' field if a regular message was sent).
				//  Zero indicates this is a header of identity message
				//  (i.e. the peer is using the unversioned protocol).
				if ((m_greeting[9] & 0x01) == 0)
					break;

				//  The peer is using versioned protocol.
				//  Send the rest of the greeting, if necessary.
				if (!(((byte[])m_outpos) == ((byte[])m_greetingOutputBuffer) &&
							m_outpos.Offset + m_outsize == GreetingSize))
				{
					if (m_outsize == 0)
						m_ioObject.SetPollout(m_handle);

					m_outpos[m_outsize++] = 1; // Protocol version
					m_outpos[m_outsize++] = (byte)m_options.SocketType;
				}
			}

			//  Is the peer using the unversioned protocol?
			//  If so, we send and receive rests of identity
			//  messages.
			if (m_greeting[0] != 0xff || (m_greeting[9] & 0x01) == 0)
			{
				m_encoder = new Encoder(Config.OutBatchSize, m_options.Endian);
				m_encoder.SetMsgSource(m_session);

				m_decoder = new Decoder(Config.InBatchSize, m_options.Maxmsgsize, m_options.Endian);

				m_decoder.SetMsgSink(m_session);

				//  We have already sent the message header.
				//  Since there is no way to tell the encoder to
				//  skip the message header, we simply throw that
				//  header data away.
				int headerSize = m_options.IdentitySize + 1 >= 255 ? 10 : 2;
				byte[] tmp = new byte[10];
				ByteArraySegment bufferp = new ByteArraySegment(tmp);

				int bufferSize = headerSize;

				m_encoder.GetData(ref bufferp, ref bufferSize);

				Debug.Assert(bufferSize == headerSize);

				//  Make sure the decoder sees the data we have already received.
				m_inpos = new ByteArraySegment(m_greeting);
				m_insize = m_greetingBytesRead;

				//  To allow for interoperability with peers that do not forward
				//  their subscriptions, we inject a phony subsription
				//  message into the incomming message stream. To put this
				//  message right after the identity message, we temporarily
				//  divert the message stream from session to ourselves.
				if (m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub)
					m_decoder.SetMsgSink(this);
			}
			else if (m_greeting[VersionPos] == 0)
			{
				//  ZMTP/1.0 framing.
				m_encoder = new Encoder(Config.OutBatchSize, m_options.Endian);
				m_encoder.SetMsgSource(m_session);

				m_decoder = new Decoder(Config.InBatchSize, m_options.Maxmsgsize, m_options.Endian);
				m_decoder.SetMsgSink(m_session);
			}
			else
			{
				//  v1 framing protocol.
				m_encoder = new V1Encoder(Config.OutBatchSize, m_session, m_options.Endian);

				m_decoder = new V1Decoder(Config.InBatchSize, m_options.Maxmsgsize, m_session, m_options.Endian);
			}
			// Start polling for output if necessary.
			if (m_outsize == 0)
				m_ioObject.SetPollout(m_handle);

			//  Handshaking was successful.
			//  Switch into the normal message flow.
			m_handshaking = false;

			return true;
		}

		public void PushMsg(Msg msg)
		{
			Debug.Assert(m_options.SocketType == ZmqSocketType.Pub || m_options.SocketType == ZmqSocketType.Xpub);

			//  The first message is identity.
			//  Let the session process it.

			m_session.PushMsg(msg);

			//  Inject the subscription message so that the ZMQ 2.x peer
			//  receives our messages.
			msg = new Msg(1);
			msg.Put((byte)1);

			m_session.PushMsg(msg);

			m_session.Flush();

			//  Once we have injected the subscription message, we can
			//  Divert the message flow back to the session.
			Debug.Assert(m_decoder != null);
			m_decoder.SetMsgSink(m_session);
		}

		private void Error()
		{
			Debug.Assert(m_session != null);
			m_socket.EventDisconnected(m_endpoint, m_handle);
			m_session.Detach();
			Unplug();
			Destroy();
		}

		private int Write(ByteArraySegment data, int size)
		{
			int nbytes = 0;
			try
			{
				nbytes = m_handle.Send((byte[])data, data.Offset, size, SocketFlags.None);
			}
			catch (SocketException ex)
			{
				//  If not a single byte can be written to the socket in non-blocking mode
				//  we'll get an error (this may happen during the speculative write).
				if (ex.SocketErrorCode == SocketError.WouldBlock)
				{
					return 0;
				}
				else if ((
									ex.SocketErrorCode == SocketError.NetworkDown ||
									ex.SocketErrorCode == SocketError.NetworkReset ||
									ex.SocketErrorCode == SocketError.HostUnreachable ||
									ex.SocketErrorCode == SocketError.ConnectionAborted ||
									ex.SocketErrorCode == SocketError.TimedOut ||
									ex.SocketErrorCode == SocketError.ConnectionReset))
				{
					return -1;
				}
				else
				{
					Debug.Assert(false);
				}
			}

			return nbytes;
		}

		private int Read(ByteArraySegment data, int size)
		{
			int nbytes = 0;
			try
			{
				nbytes = m_handle.Receive((byte[])data, data.Offset, size, SocketFlags.None);
			}
			catch (SocketException ex)
			{
				//  If not a single byte can be written to the socket in non-blocking mode
				//  we'll get an error (this may happen during the speculative write).
				if (ex.SocketErrorCode == SocketError.WouldBlock)
				{
					return 0;
				}
				else if ((
									ex.SocketErrorCode == SocketError.NetworkDown ||
									ex.SocketErrorCode == SocketError.NetworkReset ||
									ex.SocketErrorCode == SocketError.HostUnreachable ||
									ex.SocketErrorCode == SocketError.ConnectionAborted ||
									ex.SocketErrorCode == SocketError.TimedOut ||
									ex.SocketErrorCode == SocketError.ConnectionReset))
				{
					return -1;
				}
				else
				{
					Debug.Assert(false);
				}
			}

			if (nbytes == 0)
			{
				return -1;
			}

			return nbytes;
		}
	}
}