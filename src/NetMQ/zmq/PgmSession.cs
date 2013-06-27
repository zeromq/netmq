using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace NetMQ.zmq
{
	class PgmSession : IEngine, IPollEvents
	{
		private Socket m_handle;
		private readonly PgmSocket m_pgmSocket;
		private readonly Options m_options;
		private IOObject m_ioObject;
		private SessionBase m_session;
		private SocketBase m_socket;
		private Decoder m_decoder;
		private bool m_joined;

		private int m_pendingBytes;
		private ByteArraySegment m_pendingData;

		private readonly ByteArraySegment data;


		public PgmSession(PgmSocket pgmSocket, Options options)
		{
			m_handle = pgmSocket.FD;
			m_pgmSocket = pgmSocket;
			m_options = options;
			data = new byte[Config.PgmMaxTPDU];
		}

		public void Plug(IOThread ioThread, SessionBase session)
		{
			m_session = session;

			m_socket = session.Socket;

			m_ioObject = new IOObject(null);
			m_ioObject.SetHandler(this);
			m_ioObject.Plug(ioThread);
			m_ioObject.AddFd(m_handle);
			m_ioObject.SetPollin(m_handle);

			DropSubscriptions();

			// push message to the session because there is no identity message with pgm
			session.PushMsg(new Msg());
		}

		public void Terminate()
		{
		}

		public void ActivateIn()
		{
			//  It is possible that the most recently used decoder
			//  processed the whole buffer but failed to write
			//  the last message into the pipe.
			if (m_pendingBytes == 0)
			{
				if (m_decoder != null)
				{
					m_decoder.ProcessBuffer(null, 0);
					m_session.Flush();
				}

				m_ioObject.SetPollin(m_handle);

				return;
			}

			Debug.Assert(m_decoder != null);
			Debug.Assert(m_pendingData != null);

			//  Ask the decoder to process remaining data.
			int n = m_decoder.ProcessBuffer(m_pendingData, m_pendingBytes);
			m_pendingBytes -= n;
			m_session.Flush(); ;

			if (m_pendingBytes > 0)
				return;

			//  Resume polling.
			m_ioObject.SetPollin(m_handle);

			InEvent();
		}

		public void InEvent()
		{
			if (m_pendingBytes > 0)
				return;

			//  Get new batch of data.
			//  Note the workaround made not to break strict-aliasing rules.
			data.Reset();

			int received = 0;

			try
			{
				received = m_handle.Receive((byte[])data);
			}
			catch (SocketException ex)
			{
				if (ex.SocketErrorCode == SocketError.WouldBlock)
				{
					return;
					//break;
				}
				else
				{
					m_joined = false;
					Error();
					return;
				}
			}

			//  No data to process. This may happen if the packet received is
			//  neither ODATA nor ODATA.
			if (received == 0)
			{
				return;
			}

			//  Read the offset of the fist message in the current packet.
			Debug.Assert(received >= sizeof(ushort));
			ushort offset = data.GetUnsignedShort(m_options.Endian, 0);
			data.AdvanceOffset(sizeof(ushort));
			received -= sizeof(ushort);

			//  Join the stream if needed.
			if (!m_joined)
			{
				//  There is no beginning of the message in current packet.
				//  Ignore the data.
				if (offset == 0xffff)
					return;

				Debug.Assert(offset <= received);
				Debug.Assert(m_decoder == null);

				//  We have to move data to the begining of the first message.
				data.AdvanceOffset(offset);
				received -= offset;

				//  Mark the stream as joined.
				m_joined = true;

				//  Create and connect decoder for the peer.
				m_decoder = new Decoder(0, m_options.Maxmsgsize, m_options.Endian);
				m_decoder.SetMsgSink(m_session);
			}

			//  Push all the data to the decoder.
			int processed = m_decoder.ProcessBuffer(data, received);
			if (processed < received)
			{
				//  Save some state so we can resume the decoding process later.
				m_pendingBytes = received - processed;
				m_pendingData = new ByteArraySegment(data, processed);

				//  Stop polling.
				m_ioObject.ResetPollin(m_handle);

				return;
			}

			m_session.Flush();
		}

		private void Error()
		{
			Debug.Assert(m_session != null);
			//m_socket.EventDisconnected(m_endpoint, m_handle);
			m_session.Detach();

			//  Cancel all fd subscriptions.
			m_ioObject.RmFd(m_handle);

			//  Disconnect from I/O threads poller object.
			m_ioObject.Unplug();

			//  Disconnect from session object.
			if (m_decoder != null)
				m_decoder.SetMsgSink(null);

			m_session = null;

			Destroy();
		}

		public void Destroy()
		{
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

		public void OutEvent()
		{
		}

		public void TimerEvent(int id)
		{
		}

		private void DropSubscriptions()
		{
			while (m_session.PullMsg() != null)
			{
			}
		}

		public void ActivateOut()
		{
			DropSubscriptions();
		}
	}
}
