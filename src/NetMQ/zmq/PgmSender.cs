using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Net;

namespace NetMQ.zmq
{
  class PgmSender : IOObject, IEngine, IPollEvents
  {
    private readonly Options m_options;
    private readonly Address m_addr;
    private Encoder m_encoder;

    private Socket m_socket;
    private PgmSocket m_pgmSocket;

    private ByteArraySegment m_outBuffer;
    private int m_outBufferSize;

    private int m_writeSize;

    public PgmSender(IOThread ioThread, Options options, Address addr)
      : base(ioThread)
    {
      m_options = options;
      m_addr = addr;
      m_encoder = null;
      m_outBuffer = null;
      m_outBufferSize = 0;
      m_writeSize = 0;
      m_encoder = new Encoder(0, m_options.Endian);
    }

    public void Init(PgmAddress pgmAddress)
    {
      m_pgmSocket = new PgmSocket(m_options, PgmSocketType.Publisher, m_addr.Resolved as PgmAddress);
      m_pgmSocket.Init();

      m_socket = m_pgmSocket.FD;

      IPEndPoint localEndpoint = new IPEndPoint(IPAddress.Any, 0);

      m_socket.Bind(localEndpoint);

      m_pgmSocket.InitOptions();

      m_socket.Connect(pgmAddress.Address);
      m_socket.Blocking = false;

      m_outBufferSize = Config.PgmMaxTPDU;
      m_outBuffer = new ByteArraySegment(new byte[m_outBufferSize]);
    }

    public void Plug(IOThread ioThread, SessionBase session)
    {
      m_encoder.SetMsgSource(session);

      AddFd(m_socket);
      SetPollout(m_socket);

      // get the first message from the session because we don't want to send identities
      session.PullMsg();
    }

    public void Terminate()
    {
      RmFd(m_socket);
      m_encoder.SetMsgSource(null);
    }


    public void ActivateOut()
    {
      SetPollout(m_socket);
      OutEvent();
    }

    public void ActivateIn()
    {
      Debug.Assert(false);
    }

    public override void OutEvent()
    {
      //  POLLOUT event from send socket. If write buffer is empty, 
      //  try to read new data from the encoder.
      if (m_writeSize == 0)
      {

        //  First two bytes (sizeof uint16_t) are used to store message 
        //  offset in following steps. Note that by passing our buffer to
        //  the get data function we prevent it from returning its own buffer.
        ByteArraySegment bf = new ByteArraySegment(m_outBuffer, sizeof(ushort));
        int bfsz = m_outBufferSize - sizeof(ushort);
        int offset = -1;
        m_encoder.GetData(ref bf, ref bfsz, ref offset);

        //  If there are no data to write stop polling for output.
        if (bfsz == 0)
        {
          ResetPollout(m_socket);
          return;
        }

        //  Put offset information in the buffer.
        m_writeSize = bfsz + sizeof(ushort);

        m_outBuffer.PutUnsingedShort(m_options.Endian, offset == -1 ? (ushort)0xffff : (ushort)offset, 0);
      }

      int nbytes = 0;
      try
      {
        //  Send the data.
        nbytes = m_socket.Send((byte[]) m_outBuffer, m_outBuffer.Offset, m_writeSize, SocketFlags.None);
      }
      catch (SocketException ex)
      {
        //  If not a single byte can be written to the socket in non-blocking mode
        //  we'll get an error (this may happen during the speculative write).
        if (ex.SocketErrorCode == SocketError.WouldBlock)
        {
          return;
        }

        Debug.Assert(false);
      }

      //  We can write either all data or 0 which means rate limit reached.
      if (nbytes == m_writeSize)
      {
        m_writeSize = 0;
      }
      else
      {
        Debug.Assert(false);

        throw NetMQException.Create(ErrorCode.ESOCKET);
      }
    }

    public override void InEvent()
    {
      throw new NotImplementedException();
    }

    public override void TimerEvent(int id)
    {
      throw new NotImplementedException();
    }

  }
}
